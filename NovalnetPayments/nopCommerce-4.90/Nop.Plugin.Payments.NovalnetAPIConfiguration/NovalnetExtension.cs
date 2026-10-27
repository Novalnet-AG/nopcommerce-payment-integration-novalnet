using Microsoft.AspNetCore.Http;
using Nop.Core.Domain.Common;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Common;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Services.Directory;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using Nop.Plugin.Payments.NovalnetAPIConfiguration.Services;
using Nop.Plugin.Payments.NovalnetAPIConfiguration.Domain;
using Nop.Plugin.Payments.NovalnetAPIConfiguration;
using Nop.Core.Domain.Directory;

namespace Nop.Plugin.Payments.NovalnetAPIConfiguration;

public class NovalnetExtension
{
    #region Ctor
    
    public static async Task<CapturePaymentResult> ProcessCapture(CapturePaymentRequest capturePaymentRequest, string paymentAccessKey, IWorkContext _workContext, IOrderService _orderService, INovalnetTransactionDetailsService _novalnetTransactionDetailsService, ILocalizationService _localizationService, IOrderProcessingService _orderProcessingService)
    {
        var result = new CapturePaymentResult();

        var tid = capturePaymentRequest.Order.AuthorizationTransactionId;
        Order order = capturePaymentRequest.Order;

        if (string.IsNullOrWhiteSpace(tid))
        {
            result.AddError("Authorization transaction ID is missing.");
            return result;
        }

        var transaction = await _novalnetTransactionDetailsService.GetByTIDAsync(tid);
        if (transaction.TidStatus != "ON_HOLD")
        {
            result.AddError("Transaction has been already captured.");
            return result;
        }

        var requestPayload = new
        {
            transaction = new
            {
                tid = tid
            },
            custom = new
            {
                lang = await NovalnetHelper.GetLangCodeAsync(_workContext),
                shop_invoked = 1
            }
        };

        string jsonPayload = NovalnetHelper.SerializeToJson(requestPayload);

        var response = await NovalnetHelper.SendRequestAsync("/v2/transaction/capture", jsonPayload, paymentAccessKey);

        if (response != null && response.HasValues)
        {
            string status = response["result"]?["status"].ToString();
            if (status == "SUCCESS")
            {
                StringBuilder orderNoteString = new StringBuilder();

                orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.TransactionConfirmed"), NovalnetHelper.GetCurrentDate()));

                JObject bankDetails = await NovalnetHelper.GetNovalnetTransactionDetails(tid, paymentAccessKey, _workContext);

                await NovalnetHelper.GetPaymentNotes(bankDetails, orderNoteString, _localizationService);

                await NovalnetHelper.InsertTransactionNotes(orderNoteString, capturePaymentRequest.Order.Id, _orderService);

                string nnTransactionStatus = response["transaction"]["status"].ToString();
                if (nnTransactionStatus == "CONFIRMED")
                {
                    if (response["transaction"]["payment_type"].ToString() == "INVOICE")
                    {
                        result.NewPaymentStatus = PaymentStatus.Pending;
                    }
                    else
                    {
                        result.NewPaymentStatus = PaymentStatus.Paid;
                        await _orderProcessingService.MarkOrderAsPaidAsync(order);
                    }
                    transaction.TidStatus = nnTransactionStatus;
                    await _novalnetTransactionDetailsService.UpdateAsync(transaction);
                }
            }
            else
            {
                string errorMessage = response["result"]?["status_text"]?.ToString() ?? "Failed";
                result.AddError(errorMessage);
            }
        }
        return result;
    }

    public static async Task<VoidPaymentResult> ProcessVoid(VoidPaymentRequest voidPaymentRequest, string paymentAccessKey, IWorkContext _workContext, IOrderService _orderService, INovalnetTransactionDetailsService _novalnetTransactionDetailsService, ILocalizationService _localizationService, IOrderProcessingService _orderProcessingService)
    {
        var result = new VoidPaymentResult();

        var tid = voidPaymentRequest.Order.AuthorizationTransactionId;
        Order order = voidPaymentRequest.Order;

        if (string.IsNullOrWhiteSpace(tid))
        {
            result.AddError("Authorization transaction ID is missing.");
            return result;
        }

        var transaction = await _novalnetTransactionDetailsService.GetByTIDAsync(tid);
        if (transaction.TidStatus != "ON_HOLD")
        {
            result.AddError("Unable to perform the request.");
            return result;
        }

        var requestPayload = new
        {
            transaction = new
            {
                tid = tid
            },
            custom = new
            {
                lang = await NovalnetHelper.GetLangCodeAsync(_workContext),
                shop_invoked = 1
            }
        };

        string jsonPayload = NovalnetHelper.SerializeToJson(requestPayload);

        var response = await NovalnetHelper.SendRequestAsync("/v2/transaction/cancel", jsonPayload, paymentAccessKey);

        if (response != null && response.HasValues)
        {
            string status = response["result"]?["status"].ToString();
            if (status == "SUCCESS")
            {
                StringBuilder orderNoteString = new StringBuilder();

                orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.TransactionCancelled"), NovalnetHelper.GetCurrentDate()));

                await NovalnetHelper.InsertTransactionNotes(orderNoteString, voidPaymentRequest.Order.Id, _orderService);

                string nnTransactionStatus = response["transaction"]["status"].ToString();
                if (nnTransactionStatus == "DEACTIVATED")
                {
                    result.NewPaymentStatus = PaymentStatus.Voided;
                    transaction.TidStatus = nnTransactionStatus;
                    await _novalnetTransactionDetailsService.UpdateAsync(transaction);

                    await _orderProcessingService.CancelOrderAsync(order, true);
                }
            }
            else
            {
                string errorMessage = response["result"]?["status_text"]?.ToString() ?? "Failed";
                result.AddError(errorMessage);
            }
        }
        return result;
    }

    public static async Task<RefundPaymentResult> ProcessRefund(RefundPaymentRequest refundPaymentRequest, string paymentAccessKey, IWorkContext _workContext, IOrderService _orderService, INovalnetTransactionDetailsService _novalnetTransactionDetailsService, ILocalizationService _localizationService, ICurrencyService _currencyService)
    {
        var result = new RefundPaymentResult();

        string paymentType = NovalnetHelper.GetNovalnetPaymentType(refundPaymentRequest.Order.PaymentMethodSystemName);
        var tid = refundPaymentRequest.Order.AuthorizationTransactionId;

        if (string.IsNullOrWhiteSpace(tid))
        {
            result.AddError("Transaction ID not found for this order.");
            return result;
        }
        
        var transaction = await _novalnetTransactionDetailsService.GetByTIDAsync(tid);

        string[] pendingPayments = ["INVOICE", "PREPAYMENT", "MULTIBANCO"];

        Currency currency = await _currencyService.GetCurrencyByCodeAsync(refundPaymentRequest.Order.CustomerCurrencyCode);
        int refundAmountInMinorUnits = await NovalnetHelper.GetConvertedAmountCents(refundPaymentRequest.AmountToRefund, _currencyService, currency);

        int orderAmount = transaction.OrderAmount;

        if (transaction.TidStatus == "CONFIRMED" || (pendingPayments.Contains(paymentType) && transaction.TidStatus == "PENDING")) {
            var refundRequest = new
            {
                transaction = new
                {
                    tid = tid,
                    amount = refundAmountInMinorUnits
                },
                custom = new
                {
                    lang = await NovalnetHelper.GetLangCodeAsync(_workContext),
                    shop_invoked = 1
                }
            };

            string jsonPayload = NovalnetHelper.SerializeToJson(refundRequest);

            var response = await NovalnetHelper.SendRequestAsync("/v2/transaction/refund", jsonPayload, paymentAccessKey);

            if (response != null && response.HasValues)
            {
                string status = response["result"]?["status"].ToString();
                if (status == "SUCCESS")
                {
                    string currencyCode = response["transaction"]["currency"]?.ToString();
                    int refundAmountCents = response["transaction"]["refund"]?["amount"]?.Value<int>() ?? 0;
                    string refundAmount = NovalnetHelper.FormatAmount(refundAmountCents, currencyCode);

                    StringBuilder orderNoteString = new StringBuilder();
                    orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.RefundExecuted"), tid, refundAmount));

                    if (!string.IsNullOrEmpty(response["transaction"]["refund"]?["tid"]?.ToString()))
                    {
                        orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.RefundedTid"), response["transaction"]["refund"]["tid"].ToString()));
                    }

                    await NovalnetHelper.InsertTransactionNotes(orderNoteString, refundPaymentRequest.Order.Id, _orderService);

                    int totalRefundedAmount = response["transaction"]?["refunded_amount"]?.Value<int?>() ?? response["transaction"]?["refund"]?["amount"]?.Value<int?>() ?? 0;

                    if (orderAmount > totalRefundedAmount)
                    {
                        result.NewPaymentStatus = PaymentStatus.PartiallyRefunded;
                    }
                    else
                    {
                        result.NewPaymentStatus = PaymentStatus.Refunded;
                    }
                }
                else
                {
                    string errorMessage = response["result"]?["status_text"]?.ToString() ?? "Failed";
                    result.AddError(errorMessage);
                }
            }
        }
        else {
            result.AddError("Refund is currently not possible.");
            return result;
        }      

        return result;
    }

    #endregion
}

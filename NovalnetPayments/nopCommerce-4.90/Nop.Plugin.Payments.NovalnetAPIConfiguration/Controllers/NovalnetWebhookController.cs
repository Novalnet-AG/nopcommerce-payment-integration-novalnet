using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.NovalnetAPIConfiguration.Models;
using Nop.Plugin.Payments.NovalnetAPIConfiguration.Services;
using Nop.Plugin.Payments.NovalnetAPIConfiguration.Domain;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Payments;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using Nop.Core.Domain.Payments;
using System.Text.Json;
using System.Net;
using System.Text.RegularExpressions;
using Nop.Services.Stores;
using Nop.Services.Common;
using Nop.Services.Directory;
using Nop.Core.Domain.Directory;


namespace Nop.Plugin.Payments.NovalnetAPIConfiguration.Controllers;

public class NovalnetWebhookController : Controller
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly IPermissionService _permissionService;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;
    private readonly IPaymentPluginManager _paymentPluginManager;
    private readonly IWebHelper _webHelper;
    private readonly IOrderService _orderService;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly NovalnetAPIConfigurationPaymentSettings _novalnetAPIConfigurationPaymentSettings;
    private readonly INovalnetTransactionDetailsService _novalnetTransactionDetailsService;
    private readonly IWorkContext _workContext;
    private readonly IStoreService _storeService;
    private readonly IEmailSender _emailSender;
    protected readonly IEmailAccountService _emailAccountService;
    protected NovalnetTransactionDetails _novalnetTransactionDetails;
    private readonly ICurrencyService _currencyService;
    protected string _tid;
    protected string _parentTid;
    protected string _eventType;

    #endregion

    #region Ctor

    public NovalnetWebhookController(ILocalizationService localizationService,
        INotificationService notificationService,
        IPermissionService permissionService,
        ISettingService settingService,
        IStoreContext storeContext,
        IWebHelper webHelper,
        IPaymentPluginManager paymentPluginManager,
        IOrderService orderService,
        IOrderProcessingService orderProcessingService,
        IHttpContextAccessor httpContextAccessor,
        NovalnetAPIConfigurationPaymentSettings novalnetAPIConfigurationPaymentSettings,
        INovalnetTransactionDetailsService novalnetTransactionDetailsService,
        IWorkContext workContext,
        IStoreService storeService,
        IEmailSender emailSender,
        IEmailAccountService emailAccountService,
        ICurrencyService currencyService)
    {
        _localizationService = localizationService;
        _notificationService = notificationService;
        _permissionService = permissionService;
        _settingService = settingService;
        _storeContext = storeContext;
        _webHelper = webHelper;
        _paymentPluginManager = paymentPluginManager;
        _orderService = orderService;
        _orderProcessingService = orderProcessingService;
        _httpContextAccessor = httpContextAccessor;
        _novalnetAPIConfigurationPaymentSettings = novalnetAPIConfigurationPaymentSettings;
        _novalnetTransactionDetailsService = novalnetTransactionDetailsService;
        _workContext = workContext;
        _storeService = storeService;
        _emailSender = emailSender;
        _emailAccountService = emailAccountService;
        _currencyService = currencyService;
    }

    #endregion

    #region Methods

    [HttpPost]
    [HttpGet]
    public async Task<IActionResult> WebhookHandler()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var requestBody = await reader.ReadToEndAsync();

        string result = await AuthenticateEventData(Request);
        if (result != "OK")
        {
            return Unauthorized(result);
        }

        JObject eventData;
        try
        {
            eventData = JsonConvert.DeserializeObject<JObject>(requestBody);
        }
        catch (JsonReaderException ex)
        {
            return BadRequest(new { meesage = "Malformed JSON: " + ex.Message });
        }
        catch (Exception ex)
        {
            return Ok(new { meesage = "Unexpected error: " + ex.Message });
        }

        result = ValidateEventData(eventData);
        if (result != "OK")
        {
            return Ok(new { message = result });
        }

        _tid = eventData["event"]?["tid"]?.ToString();
        _eventType = eventData["event"]?["type"]?.ToString();
        _parentTid = !string.IsNullOrEmpty(eventData["event"]?["parent_tid"]?.ToString()) ? eventData["event"]?["parent_tid"]?.ToString() : _tid;

        result = ValidateChecksum(eventData);
        if (result != "OK")
            return BadRequest(new { message = result });

        
        string orderNo = eventData["transaction"]?["order_no"]?.ToString() ?? string.Empty;
        string orderGuid = eventData["custom"]?["inputval1"]?.ToString();

        if (string.IsNullOrEmpty(orderGuid))
        {
            return Ok(new { message = "Order reference not found" });
        }

        Order order = await _orderService.GetOrderByGuidAsync(new Guid(orderGuid));
        if (order == null)
        {
            return Ok(new { message = "Order not found" });
        }

        _novalnetTransactionDetails = await _novalnetTransactionDetailsService.GetByTIDAsync(_parentTid);
        
        if (_eventType == "PAYMENT")
        {
            if (_novalnetTransactionDetails == null)
            {
                return await HandleCommunicationFailureAsync(order, eventData);
            }
            else
            {
                return Ok(new { message = "Novalnet Callback received." });
            }
        }

        if (_novalnetTransactionDetails == null)
        {
            return Ok(new { message = "Order reference not found" });
        }

        if (order.AuthorizationTransactionId != _parentTid)
        {
            return Ok(new { message = "Order reference not matching." });
        }


        string status = eventData["result"]?["status"]?.ToString();
        if (!string.IsNullOrEmpty(status) && status == "SUCCESS")
        {

            switch (_eventType)
            {
                case "TRANSACTION_CAPTURE":
                case "TRANSACTION_CANCEL":
                    return await HandleTransactionCaptureCancelAsync(order, eventData);

                case "TRANSACTION_REFUND":
                    return await HandleTransactionRefundAsync(order, eventData);

                case "TRANSACTION_UPDATE":
                    return await HandleTransactionUpdateAsync(order, eventData);

                case "CREDIT":
                    return await HandleCreditAsync(order, eventData);

                case "CHARGEBACK":
                    return await HandleChargebackAsync(order, eventData);

                case "PAYMENT_REMINDER_1":
                case "PAYMENT_REMINDER_2":
                    return await HandlePaymentReminderAsync(order, eventData);

                case "SUBMISSION_TO_COLLECTION_AGENCY":
                    return await HandleCollectionAsync(order, eventData);

                default:
                    return Ok(new { message = $"Unhandled webhook event type: {_eventType}" });
            }
        }

        return Ok(new { message = "Novalnet Callback received." });
    }
    

    public async Task<IActionResult> HandleCommunicationFailureAsync(Order order, JObject eventData)
    {
        try
        {
            string status = eventData["result"]?["status"]?.ToString() ?? "";
            string transactionStatus = eventData["transaction"]?["status"]?.ToString() ?? "";
            string paymentType = eventData["transaction"]?["payment_type"]?.ToString() ?? "";

            if (status == "FAILURE" || new[] { "FAILURE", "DEACTIVATED" }.Contains(transactionStatus))
            {
                order.PaymentStatus = PaymentStatus.Voided;
                await _orderService.UpdateOrderAsync(order);
                await _orderProcessingService.CancelOrderAsync(order, true);
            }
            else
            {
                await NovalnetHelper.SetOrderPaymentStatus(order, _orderService, transactionStatus, paymentType, _orderProcessingService);
                order.AuthorizationTransactionId = _tid;
                await _orderService.UpdateOrderAsync(order);
            }

            StringBuilder orderNoteString = await NovalnetHelper.GetTransactionNotes(eventData, _localizationService);
            await NovalnetHelper.InsertTransactionNotes(orderNoteString, order.Id, _orderService);

            await NovalnetHelper.InsertTransactionDetails(eventData, _novalnetTransactionDetailsService);

            await SendWebhookNotificationEmailAsync(orderNoteString.ToString(), order.StoreId, order.Id);
            return Ok(new { message = "Novalnet callback script executed successfully" });
        }
        catch
        {
            return Ok(new { message = "An error occured while updating the transaction details." });
        }
    }


    public async Task<IActionResult> HandleTransactionUpdateAsync(Order order, JObject eventData)
    {
        var transaction = eventData["transaction"];
        string status = transaction?["status"]?.ToString();
        string paymentType = transaction?["payment_type"]?.ToString();
        int amountInCents = transaction?["amount"]?.Value<int>() ?? 0;
        string currency = transaction?["currency"]?.ToString();
        string updateType = transaction?["update_type"]?.ToString();
        string dueDate = transaction?["due_date"]?.ToString();
        string formattedAmount = NovalnetHelper.FormatAmount(amountInCents, currency); // implement this helper

        StringBuilder orderNoteString = new StringBuilder();

        if (new[] { "DEACTIVATED", "FAILURE" }.Contains(status))
        {
            orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.TransactionCancelled"), NovalnetHelper.GetCurrentDate()));

            order.PaymentStatus = PaymentStatus.Voided;
            await _orderService.UpdateOrderAsync(order);
            await _orderProcessingService.CancelOrderAsync(order, true);
        }
        else if (_novalnetTransactionDetails.TidStatus == "PENDING" && status == "ON_HOLD")
        {
            orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.TransactionStatusChanged"), _tid, NovalnetHelper.GetCurrentDate()));

            if (paymentType == "GUARANTEED_INVOICE" && transaction["bank_details"] != null)
            {
                await NovalnetHelper.GetPaymentNotes(eventData, orderNoteString, _localizationService);
            }

            await _orderProcessingService.MarkAsAuthorizedAsync(order);
        }
        else if (new[] { "PENDING", "ON_HOLD" }.Contains(_novalnetTransactionDetails.TidStatus))
        {
            if (new[] { "INVOICE", "GUARANTEED_INVOICE", "PREPAYMENT" }.Contains(paymentType))
            {
                await NovalnetHelper.GetPaymentNotes(eventData, orderNoteString, _localizationService);
            }

            if (!string.IsNullOrEmpty(dueDate))
            {
                orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.TransactionUpdatedWithDueDate"), formattedAmount, dueDate));
            }
            else
            {
                orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.TransactionUpdated"), _tid, formattedAmount, NovalnetHelper.GetCurrentDate()));
            }

            if (status == "CONFIRMED" && _orderProcessingService.CanMarkOrderAsPaid(order))
            {
                await _orderProcessingService.MarkOrderAsPaidAsync(order);
            }
        }        

        if (orderNoteString.Length > 0)
        {
            await NovalnetHelper.InsertTransactionNotes(orderNoteString, order.Id, _orderService);
        }

        if (amountInCents > 0 && !string.IsNullOrEmpty(updateType) &&
            new[] { "AMOUNT", "AMOUNT_DUE_DATE" }.Contains(updateType))
        {
            _novalnetTransactionDetails.OrderAmount = (int)amountInCents;
        }

        if (status != _novalnetTransactionDetails.TidStatus)
            _novalnetTransactionDetails.TidStatus = status;

        await _novalnetTransactionDetailsService.UpdateAsync(_novalnetTransactionDetails);
        
        await SendWebhookNotificationEmailAsync(orderNoteString.ToString(), order.StoreId, order.Id);
        
        
        return Ok(new { message = "Novalnet callback script executed successfully" });
    }



    public async Task<IActionResult> HandleCreditAsync(Order order, JObject eventData)
    {
        var transactionData = eventData["transaction"];

        int amountInCents = transactionData?["amount"]?.Value<int>() ?? 0;
        string currency = transactionData?["currency"]?.ToString() ?? "EUR";
        string amountFormatted = NovalnetHelper.FormatAmount(amountInCents, currency);

        StringBuilder orderNoteString = new StringBuilder();
        orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.CreditExecuted"), _parentTid, amountFormatted, _tid));

        var creditTypes = new[] { "INVOICE_CREDIT", "MULTIBANCO_CREDIT" };
        string paymentType = transactionData["payment_type"].ToString();

        if (creditTypes.Contains(paymentType))
        {
            int paidAmount = _novalnetTransactionDetails.CallbackAmount;
            int orderAmount = _novalnetTransactionDetails.OrderAmount;

            int newPaidAmount = paidAmount + amountInCents;

            if (newPaidAmount >= orderAmount)
            {
                if (_orderProcessingService.CanMarkOrderAsPaid(order))
                {
                    await _orderProcessingService.MarkOrderAsPaidAsync(order);
                }

                order.PaymentStatus = PaymentStatus.Paid;
                await _orderService.UpdateOrderAsync(order);
            }

            _novalnetTransactionDetails.CallbackAmount = newPaidAmount;
            await _novalnetTransactionDetailsService.UpdateAsync(_novalnetTransactionDetails);
        }

        await NovalnetHelper.InsertTransactionNotes(orderNoteString, order.Id, _orderService);

        await SendWebhookNotificationEmailAsync(orderNoteString.ToString(), order.StoreId, order.Id);
        return Ok(new { message = orderNoteString.ToString() });
    }


    public async Task<IActionResult> HandleCollectionAsync(Order order, JObject eventData)
    {
        string collectionReference = eventData["collection"]?["reference"]?.ToString() ?? "";

        StringBuilder orderNoteString = new StringBuilder();
        orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.SubmittedToCollection"), collectionReference));

        await NovalnetHelper.InsertTransactionNotes(orderNoteString, order.Id, _orderService);

        await SendWebhookNotificationEmailAsync(orderNoteString.ToString(), order.StoreId, order.Id);
        return Ok(new { message = orderNoteString.ToString() });
    }

    public async Task<IActionResult> HandlePaymentReminderAsync(Order order, JObject eventData)
    {
        string noOfRemainder = Regex.Replace(_eventType, @"[^\d]", "");

        StringBuilder orderNoteString = new StringBuilder();
        orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.PaymentReminderSent"), noOfRemainder.ToString()));

        await NovalnetHelper.InsertTransactionNotes(orderNoteString, order.Id, _orderService);

        await SendWebhookNotificationEmailAsync(orderNoteString.ToString(), order.StoreId, order.Id);
        return Ok(new { message = orderNoteString.ToString() });
    }

    public async Task<IActionResult> HandleChargebackAsync(Order order, JObject eventData)
    {
        var transactionData = eventData["transaction"];
        int amount = transactionData["amount"].Value<int>();
        string currency = transactionData?["currency"]?.ToString();

        string formattedAmount = NovalnetHelper.FormatAmount(amount, currency);

        StringBuilder orderNoteString = new StringBuilder();
        orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.ChargebackExecuted"), _parentTid, formattedAmount, NovalnetHelper.GetCurrentDate(), _tid));

        await NovalnetHelper.InsertTransactionNotes(orderNoteString, order.Id, _orderService);

        await SendWebhookNotificationEmailAsync(orderNoteString.ToString(), order.StoreId, order.Id);
        return Ok(new { message = orderNoteString.ToString() });
    }

    public async Task<IActionResult> HandleTransactionRefundAsync(Order order, JObject eventData)
    {
        var transactionData = eventData["transaction"];
        int refundAmountCents = transactionData?["refund"]?["amount"]?.Value<int>() ?? 0;
        int totalRefundedAmountCents = transactionData["refunded_amount"]?.Value<int?>() ?? refundAmountCents;

        string currency = transactionData?["currency"]?.ToString();

        string refundAmount = NovalnetHelper.FormatAmount(refundAmountCents, currency);

        StringBuilder orderNoteString = new StringBuilder();
        orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.RefundExecuted"), _parentTid, refundAmount));

        if (!string.IsNullOrEmpty(transactionData?["refund"]?["tid"]?.ToString()))
        {
            orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.RefundedTid"), transactionData["refund"]["tid"].ToString()));
        }

        await NovalnetHelper.InsertTransactionNotes(orderNoteString, order.Id, _orderService);

        Currency orderCurrency = await _currencyService.GetCurrencyByCodeAsync(order.CustomerCurrencyCode);

        decimal refundedAmountDecimal = totalRefundedAmountCents / 100m;
        // Convert to primary store currency
        decimal refundAmountPrimaryCurrency = await _currencyService.ConvertToPrimaryStoreCurrencyAsync(refundedAmountDecimal, orderCurrency);

        int refundAmountPrimaryCurrencyInt =  (int)Math.Round(refundAmountPrimaryCurrency * 100, MidpointRounding.AwayFromZero);
        int orderTotalInt = (int)Math.Round(order.OrderTotal * 100, MidpointRounding.AwayFromZero);

        // Update order refund amount
        order.RefundedAmount = refundAmountPrimaryCurrency;

        // Update payment status
        if (refundAmountPrimaryCurrencyInt >= orderTotalInt)
            order.PaymentStatus = PaymentStatus.Refunded;
        else
            order.PaymentStatus = PaymentStatus.PartiallyRefunded;

        await _orderService.UpdateOrderAsync(order);

        await SendWebhookNotificationEmailAsync(orderNoteString.ToString(), order.StoreId, order.Id);
        return Ok(new { message = orderNoteString.ToString() });
    }

    public async Task<IActionResult> HandleTransactionCaptureCancelAsync(Order order, JObject eventData)
    {
        var transactionData = eventData["transaction"];
        var status = transactionData?["status"]?.ToString();

        if (!new[] { "ON_HOLD", "PENDING" }.Contains(_novalnetTransactionDetails.TidStatus))
        {
            return Ok(new { message = "Transaction already executed" });
        }

        StringBuilder orderNoteString = new StringBuilder();

        if (_eventType == "TRANSACTION_CAPTURE")
        {
            orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.TransactionConfirmed"), NovalnetHelper.GetCurrentDate()));
            
            if(eventData["transaction"]["bank_details"] == null)
            {
                JObject bankDetails = await NovalnetHelper.GetNovalnetTransactionDetails(eventData["transaction"]["tid"].ToString(), _novalnetAPIConfigurationPaymentSettings.PaymentAccessKey, _workContext);    
                await NovalnetHelper.GetPaymentNotes(bankDetails, orderNoteString, _localizationService);   
            }
            else
            {
                await NovalnetHelper.GetPaymentNotes(eventData, orderNoteString, _localizationService);   
            }
        }
        else
        {
            orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.TransactionCancelled"), NovalnetHelper.GetCurrentDate()));
        }

        _novalnetTransactionDetails.TidStatus = status;
        await _novalnetTransactionDetailsService.UpdateAsync(_novalnetTransactionDetails);

        await NovalnetHelper.InsertTransactionNotes(orderNoteString, order.Id, _orderService);

        string nnPaymentType = NovalnetHelper.GetNovalnetPaymentType(order.PaymentMethodSystemName);
        string[] pendingPayments = ["INVOICE", "PREPAYMENT"];

        if (_eventType == "TRANSACTION_CAPTURE" && status == "CONFIRMED") {

            if(pendingPayments.Contains(nnPaymentType))
            {
              order.PaymentStatus = PaymentStatus.Pending;
              await _orderService.UpdateOrderAsync(order);
            }
            else if (_orderProcessingService.CanMarkOrderAsPaid(order))
            {
                await _orderProcessingService.MarkOrderAsPaidAsync(order);
            }
        }
        else if (_eventType == "TRANSACTION_CANCEL")
        {
			order.PaymentStatus = PaymentStatus.Voided;
            await _orderService.UpdateOrderAsync(order);
            
            if (_orderProcessingService.CanCancelOrder(order))
            {
                await _orderProcessingService.CancelOrderAsync(order, true);
            }
        }

        await SendWebhookNotificationEmailAsync(orderNoteString.ToString(), order.StoreId, order.Id);
        return Ok(new { message = orderNoteString.ToString() });
    }

    public async Task SendWebhookNotificationEmailAsync(string body, int storeId, int orderNo)
    {
        var emailAccount = (await _emailAccountService.GetAllEmailAccountsAsync()).FirstOrDefault();
        string toEmailAddress = _novalnetAPIConfigurationPaymentSettings.WebhookEmailTo;

        if (emailAccount != null && !string.IsNullOrEmpty(toEmailAddress))
        {
            string subject = "Novalnet Callback script notification - Order No : " + orderNo;

            try
            {
                await _emailSender.SendEmailAsync(
                    emailAccount,
                    subject,
                    body,
                    emailAccount.Email,
                    emailAccount.DisplayName,
                    toEmailAddress,
                    string.Empty
                );
            }
            catch
            {
                return;
            }
        }
    }


    private async Task<string> AuthenticateEventData(HttpRequest request)
    {
        bool webhookTestMode = _novalnetAPIConfigurationPaymentSettings.WebhookTestMode;
        if (!webhookTestMode)
        {
            string novalnetHostIp = await NovalnetHelper.GetIpAddressByHostnameAsync("pay-nn.de");
            var remoteIp = request.HttpContext.Connection.RemoteIpAddress?.ToString();

            if (!string.IsNullOrEmpty(remoteIp) && !string.IsNullOrEmpty(novalnetHostIp))
            {
                if (novalnetHostIp != remoteIp)
                {
                    return $"Unauthorised access from the IP {remoteIp}";
                }
            }
            else
            {
                return "Unauthorised access from the IP. Host/received IP is empty";
            }
        }
        return "OK";
    }

    public string ValidateEventData(JObject eventDataJson)
    {
        if (eventDataJson == null || !eventDataJson.HasValues) {
            return $"Required parameters are not received";
        }
        var mandatory = new Dictionary<string, List<string>>
        {
            { "event", new List<string> { "type", "checksum", "tid" } },
            { "merchant", new List<string> { "vendor", "project" } },
            { "result", new List<string> { "status" } },
            { "transaction", new List<string> { "tid", "payment_type", "status" } }
        };

        foreach (var category in mandatory)
        {
            if (!eventDataJson.TryGetValue(category.Key, out var categoryElement) || categoryElement.Type != JTokenType.Object)
            {
                return $"Required parameter category ({category.Key}) not received";
            }

            foreach (var param in category.Value)
            {
                var paramToken = categoryElement[param];
                if (paramToken == null || paramToken.Type == JTokenType.Null)
                {
                    return $"Required parameter ({param}) in the category ({category.Key}) not received";
                }

                if ((param == "tid" || param == "parent_tid") && !Regex.IsMatch(paramToken.ToString(), @"^\d{17}$"))
                {
                    return $"Invalid TID received in the category ({category.Key}): {param}";
                }
            }
        }

        return "OK";
    }
    
    public string ValidateChecksum(JObject eventData)
    {
        if (eventData == null)
            return "Invalid event data";

        string paymentAccessKey = _novalnetAPIConfigurationPaymentSettings.PaymentAccessKey;
        var status = eventData["result"]?["status"]?.ToString();

        if (string.IsNullOrEmpty(_tid) || string.IsNullOrEmpty(_eventType) || string.IsNullOrEmpty(status))
            return "Required checksum parameters missing";

        var tokenString = _tid + _eventType + status;

        var amount = eventData["transaction"]?["amount"]?.ToString();
        if (!string.IsNullOrEmpty(amount))
            tokenString += amount;

        var currency = eventData["transaction"]?["currency"]?.ToString();
        if (!string.IsNullOrEmpty(currency))
            tokenString += currency;

        if (!string.IsNullOrEmpty(paymentAccessKey))
        {
            var reversedKey = new string(paymentAccessKey.Reverse().ToArray());
            tokenString += reversedKey;
        }

        // Compute SHA-256 hash
        var generatedChecksum = NovalnetHelper.GenerateSha256Hash(tokenString);

        var providedChecksum = eventData["event"]?["checksum"]?.ToString()?.ToLower();

        if (generatedChecksum != providedChecksum)
            return "While notifying some data has been changed. The hash check failed";

        // Optional: Check if event already processed
        var shopInvoked = eventData["custom"]?["shop_invoked"]?.ToString();
        if (!string.IsNullOrEmpty(shopInvoked))
            return "Process already handled in the shop";

        return "OK";
    }



    #endregion
}

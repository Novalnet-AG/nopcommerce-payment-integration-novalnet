using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.NovalnetAPIConfiguration.Models;
using Nop.Plugin.Payments.NovalnetAPIConfiguration.Services;
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

namespace Nop.Plugin.Payments.NovalnetAPIConfiguration.Controllers;

public class NovalnetPaymentController : Controller
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
    private readonly IWorkContext _workContext;
    private readonly INovalnetTransactionDetailsService _novalnetTransactionDetailsService;

    #endregion

    #region Ctor

    public NovalnetPaymentController(ILocalizationService localizationService,
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
        IWorkContext workContext,
        INovalnetTransactionDetailsService novalnetTransactionDetailsService)
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
        _workContext = workContext;
        _novalnetTransactionDetailsService = novalnetTransactionDetailsService;
    }

    #endregion

    #region Methods

    public async Task<IActionResult> RedirectResponseHandler()
    {
        var tid = _webHelper.QueryString<string>("tid");
        if (string.IsNullOrEmpty(tid))
        {
            return RedirectToAction("Index", "Home", new { area = string.Empty });
        }

        var checksum = _webHelper.QueryString<string>("checksum");
        var status = _webHelper.QueryString<string>("status");
        var txnSecret = _httpContextAccessor.HttpContext.Session.GetString("nn_txn_secret");
        if (string.IsNullOrEmpty(txnSecret))
        {
            txnSecret = _webHelper.QueryString<string>("txn_secret");
        }

        string paymentAccessKey = _novalnetAPIConfigurationPaymentSettings.PaymentAccessKey;

        if (!string.IsNullOrEmpty(checksum) &&
            !string.IsNullOrEmpty(tid) &&
            !string.IsNullOrEmpty(txnSecret) &&
            !string.IsNullOrEmpty(status))
        {
            var reversedKey = new string(paymentAccessKey.Reverse().ToArray());
            var tokenString = $"{tid}{txnSecret}{status}{reversedKey}";

            var generatedChecksum = NovalnetHelper.GenerateSha256Hash(tokenString);

            if (!string.Equals(generatedChecksum, checksum, StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Home", new { area = string.Empty });
            }
        }

        JObject response = await NovalnetHelper.GetNovalnetTransactionDetails(tid, paymentAccessKey, _workContext);

        bool isSuccess = false;
        
        if (response != null && response.HasValues)
        {
            string apiStatus = response["result"]?["status"].ToString();
            string[] failureStatus = ["FAILURE", "DEACTIVATED"];
            string transactionStatus = response["transaction"]?["status"].ToString();
            string paymentType = response["transaction"]?["payment_type"].ToString();

            if (apiStatus == "SUCCESS" && !failureStatus.Contains(transactionStatus))
            {
                isSuccess = true;
            }

            var orderNumberGuid = Guid.Empty;
            string orderGuid = response["custom"]?["inputval1"].ToString();
            if (!string.IsNullOrEmpty(orderGuid))
            {
                orderNumberGuid = new Guid(orderGuid);
            }

            Order order = await _orderService.GetOrderByGuidAsync(orderNumberGuid);
            if (order == null && !string.IsNullOrEmpty(response["transaction"]?["order_number"]?.ToString()))
            {
                string orderNumber = response["transaction"]["order_number"].ToString();
                order = await _orderService.GetOrderByCustomOrderNumberAsync(orderNumber);
            }

            if (order != null)
            { 
                order.AuthorizationTransactionId = tid;
                await _orderService.UpdateOrderAsync(order);
                await NovalnetHelper.SetOrderPaymentStatus(order, _orderService, transactionStatus, paymentType, _orderProcessingService);

                StringBuilder orderNoteString = await NovalnetHelper.GetTransactionNotes(response, _localizationService);
                await NovalnetHelper.InsertTransactionNotes(orderNoteString, order.Id, _orderService);

                await NovalnetHelper.InsertTransactionDetails(response, _novalnetTransactionDetailsService);

                if (isSuccess == true)
                {
                    return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
                }
                else
                {
                    return RedirectToRoute("OrderDetails", new { orderId = order.Id }); ;
                }
            }
        }

        return RedirectToAction("Index", "Home", new { area = string.Empty });
    }

    #endregion
}

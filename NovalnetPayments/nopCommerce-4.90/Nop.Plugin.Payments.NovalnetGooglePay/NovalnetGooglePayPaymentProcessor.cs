using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.NovalnetGooglePay.Components;
using Nop.Plugin.Payments.NovalnetGooglePay.Domain;
using Nop.Plugin.Payments.NovalnetGooglePay.Models;
using Nop.Plugin.Payments.NovalnetAPIConfiguration;
using Nop.Plugin.Payments.NovalnetAPIConfiguration.Services;
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
using System.Xml;
using System.Xml.Serialization;
using Nop.Services.Cms;
using Nop.Web.Framework.Infrastructure;
using Nop.Services.Logging;
using Nop.Core.Domain.Cms;

namespace Nop.Plugin.Payments.NovalnetGooglePay;

/// <summary>
/// NovalnetGooglePayPayment payment processor
/// </summary>
public class NovalnetGooglePayPaymentProcessor : BasePlugin, IPaymentMethod, IWidgetPlugin
{
    #region Fields

    //set (in HttpContext.Items) by the express PlaceExpressOrder AJAX request: PostProcessPaymentAsync then puts the
    //Novalnet redirect URL here instead of calling Response.Redirect, which an XHR can't follow cross-origin (CORS)
    public const string ExpressRedirectUrlItemKey = "nn_googlepay_express_redirect_url";

     public bool HideInWidgetList => false;
    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderTotalCalculationService _orderTotalCalculationService;
    protected readonly ISettingService _settingService;
    protected readonly IWebHelper _webHelper;
    protected readonly NovalnetNovalnetGooglePayPaymentSettings _novalnetGooglePayPaymentSettings;
    private readonly NovalnetAPIConfigurationPaymentSettings _novalnetAPIConfigurationPaymentSettings;
    private readonly IOrderService _orderService;
    private readonly ICustomerService _customerService;
    private readonly IAddressService _addressService;
    private readonly ICountryService _countryService;
    private readonly ICurrencyService _currencyService;
    private readonly IWorkContext _workContext;
    private readonly IStateProvinceService _stateProvinceService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPaymentService _paymentService;
    private readonly INovalnetTransactionDetailsService _novalnetTransactionDetailsService;
    private readonly IOrderProcessingService _orderProcessingService;
    protected readonly ILanguageService _languageService;
    private readonly WidgetSettings _widgetSettings;

    #endregion

    #region Ctor

    public NovalnetGooglePayPaymentProcessor(ILocalizationService localizationService,
        IOrderTotalCalculationService orderTotalCalculationService,
        ISettingService settingService,
        IWebHelper webHelper,
        NovalnetNovalnetGooglePayPaymentSettings novalnetGooglePayPaymentSettings,
        NovalnetAPIConfigurationPaymentSettings novalnetAPIConfigurationPaymentSettings,
        IOrderService orderService,
        ICustomerService customerService,
        IAddressService addressService,
        ICurrencyService currencyService,
        IWorkContext workContext,
        ICountryService countryService,
        IStateProvinceService stateProvinceService,
        IHttpContextAccessor httpContextAccessor,
        IPaymentService paymentService,
        INovalnetTransactionDetailsService novalnetTransactionDetailsService,
        IOrderProcessingService orderProcessingService,
        ILanguageService languageService,
        WidgetSettings widgetSettings)
    {
        _localizationService = localizationService;
        _orderTotalCalculationService = orderTotalCalculationService;
        _settingService = settingService;
        _webHelper = webHelper;
        _novalnetGooglePayPaymentSettings = novalnetGooglePayPaymentSettings;
        _novalnetAPIConfigurationPaymentSettings = novalnetAPIConfigurationPaymentSettings;
        _orderService = orderService;
        _customerService = customerService;
        _addressService = addressService;
        _currencyService = currencyService;
        _workContext = workContext;
        _countryService = countryService;
        _stateProvinceService = stateProvinceService;
        _httpContextAccessor = httpContextAccessor;
        _paymentService = paymentService;
        _novalnetTransactionDetailsService = novalnetTransactionDetailsService;
        _orderProcessingService = orderProcessingService;
        _languageService = languageService;
        _widgetSettings = widgetSettings;
    }


    /// <summary>
    /// Gets widget zones where this widget should be rendered
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the widget zones
    /// </returns>
    public Task<IList<string>> GetWidgetZonesAsync()
    {
        //keep this list in sync with GetWidgetViewComponent below - a zone declared here with no
        //matching component makes WidgetModelFactory hand Component.InvokeAsync a null componentType,
        //which throws ArgumentNullException and 500s every page that renders that zone
        return Task.FromResult<IList<string>>(new List<string>
        {
            PublicWidgetZones.ProductDetailsAddInfo,
            PublicWidgetZones.OrderSummaryContentBefore,
        });
    }

    /// <summary>
    /// Gets a type of a view component for displaying widget
    /// </summary>
    /// <param name="widgetZone">Name of the widget zone</param>
    /// <returns>View component type</returns>
    public Type GetWidgetViewComponent(string widgetZone)
    {
        ArgumentNullException.ThrowIfNull(widgetZone);

        if (widgetZone.Equals(PublicWidgetZones.ProductDetailsAddInfo) || widgetZone.Equals(PublicWidgetZones.OrderSummaryContentBefore))
            return typeof(NovalnetGooglePayExpressViewComponent);

        return null;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Process a payment
    /// </summary>
    /// <param name="processPaymentRequest">Payment info required for an order processing</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the process payment result
    /// </returns>
    public async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        string nnDoRedirect = session.GetString("nnGooglePayDoRedirect");

        if (nnDoRedirect == "1")
        {
            return new ProcessPaymentResult();
        }
        else
        {
            NovalnetPayload nnPayload = await NovalnetHelper.GetPaymentRequestParams(processPaymentRequest, _novalnetAPIConfigurationPaymentSettings, _customerService, _addressService, _workContext, _stateProvinceService, _currencyService, _countryService, _webHelper, _httpContextAccessor);

            var response = await SendPaymentRequest(nnPayload, false);
            var result = await NovalnetHelper.SetDirectPaymentResult(response, new ProcessPaymentResult());
            
            return result;
        }
    }

    /// <summary>
    /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
    /// </summary>
    /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
    {
        string tid = postProcessPaymentRequest.Order.AuthorizationTransactionId;
        Order order = postProcessPaymentRequest.Order;
        
        if (string.IsNullOrEmpty(tid))
        {
            NovalnetPayload nnPayload = await NovalnetHelper.GetPaymentRequestParams(postProcessPaymentRequest, _novalnetAPIConfigurationPaymentSettings, _customerService, _addressService, _workContext, _stateProvinceService, _currencyService, _countryService, _webHelper, _httpContextAccessor);

            var response = await SendPaymentRequest(nnPayload, true);

			string redirectUrl = await NovalnetHelper.GetNovalnetRedirectUrl(response, postProcessPaymentRequest, _orderService, _orderProcessingService, _httpContextAccessor);

			NovalnetHelper.ClearSessionKeys(_httpContextAccessor, new[] { "nnGooglePayToken", "nnGooglePayDoRedirect" });

			var httpContext = _httpContextAccessor.HttpContext;
			if (httpContext.Items.ContainsKey(ExpressRedirectUrlItemKey))
			{
				httpContext.Items[ExpressRedirectUrlItemKey] = redirectUrl;
				return;
			}
			
			httpContext.Response.Redirect(redirectUrl);
			return;
        }
        else
        {
            NovalnetHelper.ClearSessionKeys(_httpContextAccessor, new[] { "nnGooglePayToken", "nnGooglePayDoRedirect" });

            string paymentAccessKey = _novalnetAPIConfigurationPaymentSettings.PaymentAccessKey;
            await NovalnetHelper.UpdateOrderNumber(postProcessPaymentRequest.Order.CustomOrderNumber, tid, paymentAccessKey);

            JObject nnTransactionDetails = await NovalnetHelper.GetNovalnetTransactionDetails(tid, paymentAccessKey, _workContext);

            StringBuilder orderNoteString = await NovalnetHelper.GetTransactionNotes(nnTransactionDetails, _localizationService);
            await NovalnetHelper.InsertTransactionNotes(orderNoteString, postProcessPaymentRequest.Order.Id, _orderService);

            await NovalnetHelper.SetOrderPaymentStatus(order, _orderService, nnTransactionDetails["transaction"]["status"].ToString(), 
            nnTransactionDetails["transaction"]["payment_type"].ToString(), _orderProcessingService);
            
            await NovalnetHelper.InsertTransactionDetails(nnTransactionDetails, _novalnetTransactionDetailsService);
        }
    }

    private async Task<JObject> SendPaymentRequest(NovalnetPayload nnPayload, bool redirectPayment)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        string nnGooglePayToken = session.GetString("nnGooglePayToken");

        PaymentInfo paymentInfo = new PaymentInfo
        {
            wallet_token = nnGooglePayToken,
        };

        nnPayload.transaction.payment_data = paymentInfo;
        nnPayload.transaction.test_mode = _novalnetGooglePayPaymentSettings.TestMode == true ? 1 : 0;

        if (redirectPayment == true)
        {
            string storeLocation = _webHelper.GetStoreLocation();
            NovalnetHelper.GetRedirectPaymentParams(nnPayload, storeLocation);
        }

        PaymentAction paymentAction = _novalnetGooglePayPaymentSettings.PaymentAction;
        int onHoldMinAmount = _novalnetGooglePayPaymentSettings.OnholdMinAmount;

        string endpoint = NovalnetHelper.GetNovalnetEndPoint(paymentAction, onHoldMinAmount, nnPayload.transaction.amount);

		string jsonPayload = NovalnetHelper.SerializeToJson(nnPayload);
		
        var response = await NovalnetHelper.SendRequestAsync(endpoint, jsonPayload, _novalnetAPIConfigurationPaymentSettings.PaymentAccessKey);

        return response;
    }

    /// <summary>
    /// Returns a value indicating whether payment method should be hidden during checkout
    /// </summary>
    /// <param name="cart">Shopping cart</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains true - hide; false - display.
    /// </returns>
    public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
    {
        bool isNovalnetAPICredentialsAvailable = NovalnetHelper.CheckNovalnetAPICredentials(_novalnetAPIConfigurationPaymentSettings);
        if (!isNovalnetAPICredentialsAvailable || string.IsNullOrWhiteSpace(_novalnetAPIConfigurationPaymentSettings.ClientKey))
        {
            return Task.FromResult(true);
        }

        //you can put any logic here
        //for example, hide this payment method if all products in the cart are downloadable
        //or hide this payment method if current customer is from certain country
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets additional handling fee
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the additional handling fee
    /// </returns>
    public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
    {
        return await _orderTotalCalculationService.CalculatePaymentAdditionalFeeAsync(cart,
            _novalnetGooglePayPaymentSettings.AdditionalFee, _novalnetGooglePayPaymentSettings.AdditionalFeePercentage);
    }

    /// <summary>
    /// Captures payment
    /// </summary>
    /// <param name="capturePaymentRequest">Capture payment request</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the capture payment result
    /// </returns>
    public async Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
    {
        CapturePaymentResult result = await NovalnetExtension.ProcessCapture(capturePaymentRequest, _novalnetAPIConfigurationPaymentSettings.PaymentAccessKey, _workContext, _orderService, _novalnetTransactionDetailsService, _localizationService, _orderProcessingService);
        return result;
    }

    /// <summary>
    /// Refunds a payment
    /// </summary>
    /// <param name="refundPaymentRequest">Request</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public async Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
    {
        RefundPaymentResult result = await NovalnetExtension.ProcessRefund(refundPaymentRequest, _novalnetAPIConfigurationPaymentSettings.PaymentAccessKey, _workContext, _orderService, _novalnetTransactionDetailsService, _localizationService, _currencyService);
        return result;
    }

    /// <summary>
    /// Voids a payment
    /// </summary>
    /// <param name="voidPaymentRequest">Request</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public async Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
    {
        VoidPaymentResult result = await NovalnetExtension.ProcessVoid(voidPaymentRequest, _novalnetAPIConfigurationPaymentSettings.PaymentAccessKey, _workContext, _orderService, _novalnetTransactionDetailsService, _localizationService, _orderProcessingService);
        return result;
    }

    /// <summary>
    /// Process recurring payment
    /// </summary>
    /// <param name="processPaymentRequest">Payment info required for an order processing</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the process payment result
    /// </returns>
    public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        var result = new ProcessPaymentResult{};
        result.AddError("Not supported transaction type");        
        return Task.FromResult(result);
    }

    /// <summary>
    /// Cancels a recurring payment
    /// </summary>
    /// <param name="cancelPaymentRequest">Request</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
    {
        //always success
        return Task.FromResult(new CancelRecurringPaymentResult());
    }

    /// <summary>
    /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
    /// </summary>
    /// <param name="order">Order</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public Task<bool> CanRePostProcessPaymentAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        //it's not a redirection payment method. So we always return false
        return Task.FromResult(false);
    }

    /// <summary>
    /// Validate payment form
    /// </summary>
    /// <param name="form">The parsed form values</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the list of validating errors
    /// </returns>
    public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
    {
        return Task.FromResult<IList<string>>(new List<string>());
    }

    /// <summary>
    /// Get payment information
    /// </summary>
    /// <param name="form">The parsed form values</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the payment info holder
    /// </returns>
    public async Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
    {

        var paymentRequest = new ProcessPaymentRequest();

        var session = _httpContextAccessor.HttpContext?.Session;

        // Extract a custom field from the form
        var keys = new[] { "nnGooglePayToken", "nnGooglePayDoRedirect" };

        foreach (var key in keys)
        {
            if (form.TryGetValue(key, out var value))
            {
                session.SetString(key, value.ToString());
            }
        }

        // return Task.FromResult(paymentRequest);
        return paymentRequest;
    }

    /// <summary>
    /// Gets a configuration page URL
    /// </summary>
    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/NovalnetGooglePay/Configure";
    }

    /// <summary>
    /// Gets a type of a view component for displaying plugin in public store ("payment info" checkout step)
    /// </summary>
    /// <returns>View component type</returns>
    public Type GetPublicViewComponent()
    {
        return typeof(NovalnetGooglePayViewComponent);
    }

    /// <summary>
    /// Install the plugin
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task InstallAsync()
    {
        //settings
        var settings = new NovalnetNovalnetGooglePayPaymentSettings
        {
            ExpressCheckoutPages = "cart,product",
            ButtonHeight = GooglePayButtonDefaults.DefaultHeight,
            ButtonType = GooglePayButtonDefaults.DefaultType
        };
        await _settingService.SaveSettingAsync(settings);

        if (!_widgetSettings.ActiveWidgetSystemNames.Contains("Payments.NovalnetGooglePay"))
        {
            _widgetSettings.ActiveWidgetSystemNames.Add("Payments.NovalnetGooglePay");
            await _settingService.SaveSettingAsync(_widgetSettings);
        }

        var allLanguages = await _languageService.GetAllLanguagesAsync();

        var englishLanguage = allLanguages.FirstOrDefault(lang => lang.UniqueSeoCode.Equals("en", StringComparison.InvariantCultureIgnoreCase));
        if (englishLanguage != null)
        {
            await _localizationService.SaveLocalizedFriendlyNameAsync(this, englishLanguage.Id, "Google Pay");
        }
        
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Payments.NovalnetGooglePay.Fields.AdditionalFee"] = "Additional fee",
            ["Plugins.Payments.NovalnetGooglePay.Fields.AdditionalFee.Hint"] = "Enter additional fee to charge your customers.",
            ["Plugins.Payments.NovalnetGooglePay.Fields.AdditionalFeePercentage"] = "Additional fee. Use percentage",
            ["Plugins.Payments.NovalnetGooglePay.Fields.AdditionalFeePercentage.Hint"] = "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.",
            ["Plugins.Payments.NovalnetGooglePay.PaymentMethodDescription"] = "Amount will be booked from your card after successful authentication",
            ["Plugins.Payments.NovalnetGooglePay.Fields.DescriptionText"] = "Description",
            ["Plugins.Payments.NovalnetGooglePay.Fields.DescriptionText.Hint"] = "Enter info that will be shown to customers during checkout",
            ["Plugins.Payments.NovalnetGooglePay.Fields.TestMode"] = "Enable test mode",
            ["Plugins.Payments.NovalnetGooglePay.Fields.TestMode.Hint"] = "The payment will be processed in the test mode therefore amount for this transaction will not be charged",
            ["Plugins.Payments.NovalnetGooglePay.Fields.PaymentAction"] = "Payment Action",
            ["Plugins.Payments.NovalnetGooglePay.Fields.PaymentAction.Hint"] = "Choose whether or not the payment should be charged immediately. Capture completes the transaction by transferring the funds from buyer account to merchant account. Authorize verifies payment details and reserves funds to capture it later, giving time for the merchant to decide on the order.",
            ["Plugins.Payments.NovalnetGooglePay.Fields.OnholdMinAmount"] = "Minimum transaction amount for authorization (in minimum unit of currency. E.g. enter 100 which is equal to 1.00)",
            ["Plugins.Payments.NovalnetGooglePay.Fields.OnholdMinAmount.Hint"] = "In case the order amount exceeds the mentioned limit, the transaction will be set on-hold till your confirmation of the transaction. You can leave the field empty if you wish to process all the transactions as on-hold.",
            ["Plugins.Payments.NovalnetGooglePay.Fields.BusinessName"] = "Business name",
            ["Plugins.Payments.NovalnetGooglePay.Fields.BusinessName.Hint"] = "The business name is rendered in the Google Pay payment sheet, and this text will appear as PAY 'BUSINESS NAME' so that the customer knows where he is paying to",
            ["Plugins.Payments.NovalnetGooglePay.Fields.GoogleMerchantId"] = "Google Merchant ID",
            ["Plugins.Payments.NovalnetGooglePay.Fields.GoogleMerchantId.Required"] = "Google Merchant ID is required.",
            ["Plugins.Payments.NovalnetGooglePay.OrderProcessedWithCard"] = "Your order was successfully processed using Google Pay ({0} ****{1})",
            ["Plugins.Payments.NovalnetGooglePay.Fields.GoogleMerchantId.Hint"] = "Please note that Google's merchant identifier is required for processing the payment method in the live environment. Google's merchant identifier is issued after registration with the <a href=\"https://pay.google.com/business/console/\" target=\"_blank\">Google Pay and Wallet Console</a>. See <a href=\"https://developers.google.com/pay/api/web/guides/test-and-deploy/request-prod-access\" target=\"_blank\">Request production access</a> for more information about the approval process and obtaining a Google merchant identifier. The registration also involves submitting the integration with sufficient screen-shots, so collect this information by enabling the payment method in test mode. To suppress the validation of this field while saving the configuration, use this test identifier BCR2DN4XXXTN7FSI for testing and submission of your integration to Google.",
            ["Plugins.Payments.NovalnetGooglePay.Fields.ExpressCheckoutPages"] = "Display the Google Pay Button on",
            ["Plugins.Payments.NovalnetGooglePay.Fields.ExpressCheckoutPages.Hint"] = "The selected pages will display the Google Pay button to pay instantly as an express checkout option",
            ["Plugins.Payments.NovalnetGooglePay.ExpressCheckoutPages.Cart"] = "Shopping cart page",
            ["Plugins.Payments.NovalnetGooglePay.ExpressCheckoutPages.Product"] = "Product page",
            ["Plugins.Payments.NovalnetGooglePay.LineItems.Shipping"] = "Shipping: {0}",
            ["Plugins.Payments.NovalnetGooglePay.Fields.ButtonHeight"] = "Button Height",
            ["Plugins.Payments.NovalnetGooglePay.Fields.ButtonHeight.Hint"] = "Range from 40 to 100 pixels",
            ["Plugins.Payments.NovalnetGooglePay.Fields.ButtonType"] = "Button Type",
            ["Plugins.Payments.NovalnetGooglePay.Fields.ButtonType.Hint"] = "Select the type of the Google Pay button, which determines the text shown on it",
            ["Plugins.Payments.NovalnetGooglePay.ButtonType.Default"] = "Default",
            ["Plugins.Payments.NovalnetGooglePay.ButtonType.Buy"] = "Buy",
            ["Plugins.Payments.NovalnetGooglePay.ButtonType.Donate"] = "Donate",
            ["Plugins.Payments.NovalnetGooglePay.ButtonType.Book"] = "Book",
            ["Plugins.Payments.NovalnetGooglePay.ButtonType.Contribute"] = "Contribute",
            ["Plugins.Payments.NovalnetGooglePay.ButtonType.Checkout"] = "Checkout",
            ["Plugins.Payments.NovalnetGooglePay.ButtonType.Rent"] = "Rent",
            ["Plugins.Payments.NovalnetGooglePay.ButtonType.Support"] = "Support",
            ["Plugins.Payments.NovalnetGooglePay.CountryRestriction.ErrorText"] = "Google Pay is not available for the selected country",
        });

        var germanLanguage = allLanguages.FirstOrDefault(lang => lang.UniqueSeoCode.Equals("de", StringComparison.InvariantCultureIgnoreCase));
        if (germanLanguage != null)
        {
            await _localizationService.SaveLocalizedFriendlyNameAsync(this, germanLanguage.Id, "Google Pay");
            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Payments.NovalnetGooglePay.Fields.AdditionalFee"] = "zusätzliche Gebühr",
                ["Plugins.Payments.NovalnetGooglePay.Fields.AdditionalFee.Hint"] = "Geben Sie die zusätzliche Gebühr ein, die Sie Ihren Kunden berechnen möchten.",
                ["Plugins.Payments.NovalnetGooglePay.Fields.AdditionalFeePercentage"] = "Zusätzliche Gebühr. Prozentsatz der Nutzung",
                ["Plugins.Payments.NovalnetGooglePay.Fields.AdditionalFeePercentage.Hint"] = "Legt fest, ob ein prozentualer Aufschlag auf den Gesamtbetrag der Bestellung angewendet wird. Wenn diese Option nicht aktiviert ist, wird ein fester Wert verwendet.",
                ["Plugins.Payments.NovalnetGooglePay.PaymentMethodDescription"] = "Der Betrag wird nach erfolgreicher Authentifizierung von Ihrer Karte abgebucht",
                ["Plugins.Payments.NovalnetGooglePay.Fields.DescriptionText"] = "Beschreibung",
                ["Plugins.Payments.NovalnetGooglePay.Fields.DescriptionText.Hint"] = "Geben Sie die Informationen ein, die den Kunden bei der Bezahlung angezeigt werden.",
                ["Plugins.Payments.NovalnetGooglePay.Fields.TestMode"] = "Testmodus aktivieren",
                ["Plugins.Payments.NovalnetGooglePay.Fields.TestMode.Hint"] = "Die Zahlung wird im Testmodus durchgeführt, daher wird der Betrag für diese Transaktion nicht eingezogen",
                ["Plugins.Payments.NovalnetGooglePay.Fields.PaymentAction"] = "Aktion für vom Besteller autorisierte Zahlungen",
                ["Plugins.Payments.NovalnetGooglePay.Fields.PaymentAction.Hint"] = "Wählen Sie, ob die Zahlung sofort belastet werden soll oder nicht. Zahlung einziehen: Betrag sofort belasten. Zahlung autorisieren: Die Zahlung wird überprüft und autorisiert, aber erst zu einem späteren Zeitpunkt belastet. So haben Sie Zeit, über die Bestellung zu entscheiden",
                ["Plugins.Payments.NovalnetGooglePay.Fields.OnholdMinAmount"] = "Mindesttransaktionsbetrag für die Autorisierung (in der kleinsten währungseinheit, z.b. 100 cent = entsprechen 1.00 eur)",
                ["Plugins.Payments.NovalnetGooglePay.Fields.OnholdMinAmount.Hint"] = "Übersteigt der Bestellbetrag das genannte Limit, wird die Transaktion, bis zu ihrer Bestätigung durch Sie, auf on hold gesetzt. Sie können das Feld leer lassen, wenn Sie möchten, dass alle Transaktionen als on hold behandelt werden.",
                ["Plugins.Payments.NovalnetGooglePay.Fields.BusinessName"] = "Name des Geschäfts",
                ["Plugins.Payments.NovalnetGooglePay.Fields.BusinessName.Hint"] = "Der Name des Geschäfts wird in den Zahlungsbeleg von Google Pay eingefügt und der Text wird als PAY 'Name des Geschäfts' angezeigt, so dass der Endkunde weiß, an wen er zahlt",
                ["Plugins.Payments.NovalnetGooglePay.Fields.GoogleMerchantId"] = "Google-Händler-ID",
                ["Plugins.Payments.NovalnetGooglePay.Fields.GoogleMerchantId.Required"] = "Die Google-Händler-ID ist erforderlich.",
                ["Plugins.Payments.NovalnetGooglePay.OrderProcessedWithCard"] = "Ihre Bestellung wurde erfolgreich mit Google Pay durchgeführt ({0} **** {1})",
                ["Plugins.Payments.NovalnetGooglePay.Fields.GoogleMerchantId.Hint"] = "Beachten Sie bitte, dass die Händler-ID von Google für die Ausführung dieser Zahlungsart in der Live-Umgebung benötigt wird. Die Händler-ID wird nach der Registrierung bei <a href=\"https://pay.google.com/business/console/\" target=\"_blank\">Google Pay und der Wallet-Konsole</a> vergeben. Siehe auch: <a href=\"https://developers.google.com/pay/api/web/guides/test-and-deploy/request-prod-access\" target=\"_blank\">Anfrage für Produktiv-Zugang stellen</a>, falls Sie mehr Informationen zum Genehmigungsverfahren benötigen und dazu, wie Sie eine Google Händler-ID erhalten. Die Registrierung beinhaltet auch, dass Sie Ihre Anbindung mit ausreichenden Screenshots einreichen, deshalb sammeln Sie diese Informationen, indem Sie die Zahlungsmethode im Testmodus aktivieren. Um die Validierung dieses Feldes zu überspringen, während Sie die Konfiguration speichern, verwenden Sie diese Test-ID, BCR2DN4XXXTN7FSI , zum Testen und Einreichen Ihrer Anbindung bei Google.",
                ["Plugins.Payments.NovalnetGooglePay.Fields.ExpressCheckoutPages"] = "Google-Pay-Button anzeigen auf",
                ["Plugins.Payments.NovalnetGooglePay.Fields.ExpressCheckoutPages.Hint"] = "Auf den ausgewählten Seiten wird der Google Pay-Button angezeigt um mit der Express-Checkout-Option sofort zu bezahlen",
                ["Plugins.Payments.NovalnetGooglePay.ExpressCheckoutPages.Cart"] = "Warenkorb",
                ["Plugins.Payments.NovalnetGooglePay.ExpressCheckoutPages.Product"] = "Produktseite",
                ["Plugins.Payments.NovalnetGooglePay.LineItems.Shipping"] = "Versand: {0}",
                ["Plugins.Payments.NovalnetGooglePay.Fields.ButtonHeight"] = "Button-Höhe",
                ["Plugins.Payments.NovalnetGooglePay.Fields.ButtonHeight.Hint"] = "zwischen 40 und 100 Pixel",
                ["Plugins.Payments.NovalnetGooglePay.Fields.ButtonType"] = "Button-Typ",
                ["Plugins.Payments.NovalnetGooglePay.Fields.ButtonType.Hint"] = "Wählen Sie den Typ des Google Pay-Buttons aus, der den angezeigten Text bestimmt",
                ["Plugins.Payments.NovalnetGooglePay.ButtonType.Default"] = "Default",
                ["Plugins.Payments.NovalnetGooglePay.ButtonType.Buy"] = "Kaufen",
                ["Plugins.Payments.NovalnetGooglePay.ButtonType.Donate"] = "Spenden",
                ["Plugins.Payments.NovalnetGooglePay.ButtonType.Book"] = "Buchen",
                ["Plugins.Payments.NovalnetGooglePay.ButtonType.Contribute"] = "Beitragen",
                ["Plugins.Payments.NovalnetGooglePay.ButtonType.Checkout"] = "Bezahlen",
                ["Plugins.Payments.NovalnetGooglePay.ButtonType.Rent"] = "Mieten",
                ["Plugins.Payments.NovalnetGooglePay.ButtonType.Support"] = "Unterstützen",
                ["Plugins.Payments.NovalnetGooglePay.CountryRestriction.ErrorText"] = "Google Pay ist für das ausgewählte Land nicht verfügbar.",
            }, germanLanguage.Id);
        }

        await base.InstallAsync();
    }

    /// <summary>
    /// Uninstall the plugin
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task UninstallAsync()
    {
        //settings
        await _settingService.DeleteSettingAsync<NovalnetNovalnetGooglePayPaymentSettings>();

        if (_widgetSettings.ActiveWidgetSystemNames.Contains("Payments.NovalnetGooglePay"))
        {
            _widgetSettings.ActiveWidgetSystemNames.Remove("Payments.NovalnetGooglePay");
            await _settingService.SaveSettingAsync(_widgetSettings);
        }

        //locales
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.NovalnetGooglePay");

        await base.UninstallAsync();
    }

    /// <summary>
    /// Gets a payment method description that will be displayed on checkout pages in the public store
    /// </summary>
    /// <remarks>
    /// return description of this payment method to be display on "payment method" checkout step. good practice is to make it localizable
    /// </remarks>
    /// <returns>A task that represents the asynchronous operation</returns>
    public Task<string> GetPaymentMethodDescriptionAsync()
    {
        return Task.FromResult("");
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets a value indicating whether capture is supported
    /// </summary>
    public bool SupportCapture => true;

    /// <summary>
    /// Gets a value indicating whether partial refund is supported
    /// </summary>
    public bool SupportPartiallyRefund => true;

    /// <summary>
    /// Gets a value indicating whether refund is supported
    /// </summary>
    public bool SupportRefund => true;

    /// <summary>
    /// Gets a value indicating whether void is supported
    /// </summary>
    public bool SupportVoid => true;

    /// <summary>
    /// Gets a recurring payment type of payment method
    /// </summary>paymentRequest
    public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

    /// <summary>
    /// Gets a payment method type
    /// </summary>
    public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

    /// <summary>
    /// Gets a value indicating whether we should display a payment information page for this plugin
    /// </summary>
    public bool SkipPaymentInfo => false;

    #endregion
}

using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.NovalnetApplePay.Components;
using Nop.Plugin.Payments.NovalnetApplePay.Models;
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

namespace Nop.Plugin.Payments.NovalnetApplePay;

/// <summary>
/// NovalnetApplePayPayment payment processor
/// </summary>
public class NovalnetApplePayPaymentProcessor : BasePlugin, IPaymentMethod, IWidgetPlugin
{
    #region Fields

     public bool HideInWidgetList => false;
    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderTotalCalculationService _orderTotalCalculationService;
    protected readonly ISettingService _settingService;
    protected readonly IWebHelper _webHelper;
    protected readonly NovalnetNovalnetApplePayPaymentSettings _novalnetApplePayPaymentSettings;
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

    public NovalnetApplePayPaymentProcessor(ILocalizationService localizationService,
        IOrderTotalCalculationService orderTotalCalculationService,
        ISettingService settingService,
        IWebHelper webHelper,
        NovalnetNovalnetApplePayPaymentSettings novalnetApplePayPaymentSettings,
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
        _novalnetApplePayPaymentSettings = novalnetApplePayPaymentSettings;
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
            return typeof(NovalnetApplePayExpressViewComponent);

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
            NovalnetPayload nnPayload = await NovalnetHelper.GetPaymentRequestParams(processPaymentRequest, _novalnetAPIConfigurationPaymentSettings, _customerService, _addressService, _workContext, _stateProvinceService, _currencyService, _countryService, _webHelper, _httpContextAccessor);

            var response = await SendPaymentRequest(nnPayload, false);
            var result = await NovalnetHelper.SetDirectPaymentResult(response, new ProcessPaymentResult());
            return result;
        
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
        
            NovalnetHelper.ClearSessionKeys(_httpContextAccessor, new[] { "nnApplePayToken" });

            string paymentAccessKey = _novalnetAPIConfigurationPaymentSettings.PaymentAccessKey;
            await NovalnetHelper.UpdateOrderNumber(postProcessPaymentRequest.Order.CustomOrderNumber, tid, paymentAccessKey);

            JObject nnTransactionDetails = await NovalnetHelper.GetNovalnetTransactionDetails(tid, paymentAccessKey, _workContext);

            StringBuilder orderNoteString = await NovalnetHelper.GetTransactionNotes(nnTransactionDetails, _localizationService);
            await NovalnetHelper.InsertTransactionNotes(orderNoteString, postProcessPaymentRequest.Order.Id, _orderService);

            await NovalnetHelper.SetOrderPaymentStatus(order, _orderService, nnTransactionDetails["transaction"]["status"].ToString(), 
            nnTransactionDetails["transaction"]["payment_type"].ToString(), _orderProcessingService);
            
            await NovalnetHelper.InsertTransactionDetails(nnTransactionDetails, _novalnetTransactionDetailsService);
        
    }

    private async Task<JObject> SendPaymentRequest(NovalnetPayload nnPayload, bool redirectPayment)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        string nnApplePayToken = session.GetString("nnApplePayToken");

        PaymentInfo paymentInfo = new PaymentInfo
        {
            wallet_token = nnApplePayToken,
        };

        nnPayload.transaction.payment_data = paymentInfo;
        nnPayload.transaction.test_mode = _novalnetApplePayPaymentSettings.TestMode == true ? 1 : 0;
        
        PaymentAction paymentAction = _novalnetApplePayPaymentSettings.PaymentAction;
        int onHoldMinAmount = _novalnetApplePayPaymentSettings.OnholdMinAmount;

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

        var userAgent = _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString();
        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            bool isSafari = userAgent.Contains("Safari", StringComparison.OrdinalIgnoreCase) &&
                            !userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase) &&
                            !userAgent.Contains("Chromium", StringComparison.OrdinalIgnoreCase) &&
                            !userAgent.Contains("CriOS", StringComparison.OrdinalIgnoreCase) &&
                            !userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase);
            if (!isSafari)
            {
                return Task.FromResult(true);
            }
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
            _novalnetApplePayPaymentSettings.AdditionalFee, _novalnetApplePayPaymentSettings.AdditionalFeePercentage);
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
    public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
    {

        var paymentRequest = new ProcessPaymentRequest();

        var session = _httpContextAccessor.HttpContext?.Session;

        // Extract a custom field from the form
        var keys = new[] { "nnApplePayToken" };

        foreach (var key in keys)
        {
            if (form.TryGetValue(key, out var value))
            {
                session.SetString(key, value.ToString());
            }
        }

        return Task.FromResult(paymentRequest);
    }

    /// <summary>
    /// Gets a configuration page URL
    /// </summary>
    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/NovalnetApplePay/Configure";
    }

    /// <summary>
    /// Gets a type of a view component for displaying plugin in public store ("payment info" checkout step)
    /// </summary>
    /// <returns>View component type</returns>
    public Type GetPublicViewComponent()
    {
        return typeof(NovalnetApplePayViewComponent);
    }

    /// <summary>
    /// Install the plugin
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task InstallAsync()
    {
        //settings
        var settings = new NovalnetNovalnetApplePayPaymentSettings
        {
            ButtonType = "default",
            ButtonTheme = "black",
            ButtonHeight = 45,
            ButtonCornerRadius = 4,
            DisplayPages = "cart,product"
        };
        await _settingService.SaveSettingAsync(settings);

        if (!_widgetSettings.ActiveWidgetSystemNames.Contains("Payments.NovalnetApplePay"))
        {
            _widgetSettings.ActiveWidgetSystemNames.Add("Payments.NovalnetApplePay");
            await _settingService.SaveSettingAsync(_widgetSettings);
        }

        var allLanguages = await _languageService.GetAllLanguagesAsync();

        var englishLanguage = allLanguages.FirstOrDefault(lang => lang.UniqueSeoCode.Equals("en", StringComparison.InvariantCultureIgnoreCase));
        if (englishLanguage != null)
        {
            await _localizationService.SaveLocalizedFriendlyNameAsync(this, englishLanguage.Id, "Apple Pay");
        }
        
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Payments.NovalnetApplePay.Fields.AdditionalFee"] = "Additional fee",
            ["Plugins.Payments.NovalnetApplePay.Fields.AdditionalFee.Hint"] = "Enter additional fee to charge your customers.",
            ["Plugins.Payments.NovalnetApplePay.Fields.AdditionalFeePercentage"] = "Additional fee. Use percentage",
            ["Plugins.Payments.NovalnetApplePay.Fields.AdditionalFeePercentage.Hint"] = "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.",
            ["Plugins.Payments.NovalnetApplePay.PaymentMethodDescription"] = "Amount will be booked from your card after successful authentication",
            ["Plugins.Payments.NovalnetApplePay.Fields.DescriptionText"] = "Description",
            ["Plugins.Payments.NovalnetApplePay.Fields.DescriptionText.Hint"] = "Enter info that will be shown to customers during checkout",
            ["Plugins.Payments.NovalnetApplePay.Fields.TestMode"] = "Enable test mode",
            ["Plugins.Payments.NovalnetApplePay.Fields.TestMode.Hint"] = "The payment will be processed in the test mode therefore amount for this transaction will not be charged",
            ["Plugins.Payments.NovalnetApplePay.Fields.PaymentAction"] = "Payment Action",
            ["Plugins.Payments.NovalnetApplePay.Fields.PaymentAction.Hint"] = "Choose whether or not the payment should be charged immediately. Capture completes the transaction by transferring the funds from buyer account to merchant account. Authorize verifies payment details and reserves funds to capture it later, giving time for the merchant to decide on the order.",
            ["Plugins.Payments.NovalnetApplePay.Fields.OnholdMinAmount"] = "Minimum transaction amount for authorization (in minimum unit of currency. E.g. enter 100 which is equal to 1.00)",
            ["Plugins.Payments.NovalnetApplePay.Fields.OnholdMinAmount.Hint"] = "In case the order amount exceeds the mentioned limit, the transaction will be set on-hold till your confirmation of the transaction. You can leave the field empty if you wish to process all the transactions as on-hold.",
            ["Plugins.Payments.NovalnetApplePay.Fields.BusinessName"] = "Business name",
            ["Plugins.Payments.NovalnetApplePay.Fields.BusinessName.Hint"] = "The business name is rendered in the Apple Pay payment sheet, and this text will appear as PAY 'BUSINESS NAME' so that the customer knows where he is paying to",
            ["Plugins.Payments.NovalnetApplePay.Fields.ButtonType"] = "Button Type",
            ["Plugins.Payments.NovalnetApplePay.Fields.ButtonTheme"] = "Button Theme",
            ["Plugins.Payments.NovalnetApplePay.Fields.ButtonHeight"] = "Button Height",
            ["Plugins.Payments.NovalnetApplePay.Fields.ButtonHeight.Hint"] = "Range from 30 to 64 pixels",
            ["Plugins.Payments.NovalnetApplePay.Fields.ButtonCornerRadius"] = "Button Corner Radius",
            ["Plugins.Payments.NovalnetApplePay.Fields.ButtonCornerRadius.Hint"] = "Range from 0 to 10 pixels",
            ["Plugins.Payments.NovalnetApplePay.OrderProcessedWithCard"] = "Your order was successfully processed using Apple Pay ({0} ****{1})",
            ["Plugins.Payments.NovalnetApplePay.Fields.DisplayPages"] = "Display the Apple Pay Button on",
            ["Plugins.Payments.NovalnetApplePay.Fields.DisplayPages.Hint"] = "The selected pages will display the Apple Pay button to pay instantly as an express checkout option",
            ["Plugins.Payments.NovalnetApplePay.DisplayPages.Cart"] = "Shopping cart page",
            ["Plugins.Payments.NovalnetApplePay.DisplayPages.Product"] = "Product page",
            ["Plugins.Payments.NovalnetApplePay.ButtonType.Default"] = "Default",
            ["Plugins.Payments.NovalnetApplePay.ButtonType.Buy"] = "Buy",
            ["Plugins.Payments.NovalnetApplePay.ButtonType.Donate"] = "Donate",
            ["Plugins.Payments.NovalnetApplePay.ButtonType.Book"] = "Book",
            ["Plugins.Payments.NovalnetApplePay.ButtonType.Contribute"] = "Contribute",
            ["Plugins.Payments.NovalnetApplePay.ButtonType.Checkout"] = "Checkout",
            ["Plugins.Payments.NovalnetApplePay.ButtonType.Rent"] = "Rent",
            ["Plugins.Payments.NovalnetApplePay.ButtonType.Support"] = "Support",
            ["Plugins.Payments.NovalnetApplePay.ButtonTheme.Dark"] = "Dark",
            ["Plugins.Payments.NovalnetApplePay.ButtonTheme.Light"] = "Light",
            ["Plugins.Payments.NovalnetApplePay.ButtonTheme.LightOutline"] = "Light-Outline",
            ["Plugins.Payments.NovalnetApplePay.CountryRestriction.ErrorText"] = "Apple Pay is not available for the selected country",
        });

        var germanLanguage = allLanguages.FirstOrDefault(lang => lang.UniqueSeoCode.Equals("de", StringComparison.InvariantCultureIgnoreCase));
        if (germanLanguage != null)
        {
            await _localizationService.SaveLocalizedFriendlyNameAsync(this, germanLanguage.Id, "Apple Pay");
            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Payments.NovalnetApplePay.Fields.AdditionalFee"] = "zusätzliche Gebühr",
                ["Plugins.Payments.NovalnetApplePay.Fields.AdditionalFee.Hint"] = "Geben Sie die zusätzliche Gebühr ein, die Sie Ihren Kunden berechnen möchten.",
                ["Plugins.Payments.NovalnetApplePay.Fields.AdditionalFeePercentage"] = "Zusätzliche Gebühr. Prozentsatz der Nutzung",
                ["Plugins.Payments.NovalnetApplePay.Fields.AdditionalFeePercentage.Hint"] = "Legt fest, ob ein prozentualer Aufschlag auf den Gesamtbetrag der Bestellung angewendet wird. Wenn diese Option nicht aktiviert ist, wird ein fester Wert verwendet.",
                ["Plugins.Payments.NovalnetApplePay.PaymentMethodDescription"] = "Der Betrag wird nach erfolgreicher Authentifizierung von Ihrer Karte abgebucht",
                ["Plugins.Payments.NovalnetApplePay.Fields.DescriptionText"] = "Beschreibung",
                ["Plugins.Payments.NovalnetApplePay.Fields.DescriptionText.Hint"] = "Geben Sie die Informationen ein, die den Kunden bei der Bezahlung angezeigt werden.",
                ["Plugins.Payments.NovalnetApplePay.Fields.TestMode"] = "Testmodus aktivieren",
                ["Plugins.Payments.NovalnetApplePay.Fields.TestMode.Hint"] = "Die Zahlung wird im Testmodus durchgeführt, daher wird der Betrag für diese Transaktion nicht eingezogen",
                ["Plugins.Payments.NovalnetApplePay.Fields.PaymentAction"] = "Aktion für vom Besteller autorisierte Zahlungen",
                ["Plugins.Payments.NovalnetApplePay.Fields.PaymentAction.Hint"] = "Wählen Sie, ob die Zahlung sofort belastet werden soll oder nicht. Zahlung einziehen: Betrag sofort belasten. Zahlung autorisieren: Die Zahlung wird überprüft und autorisiert, aber erst zu einem späteren Zeitpunkt belastet. So haben Sie Zeit, über die Bestellung zu entscheiden",
                ["Plugins.Payments.NovalnetApplePay.Fields.OnholdMinAmount"] = "Mindesttransaktionsbetrag für die Autorisierung (in der kleinsten währungseinheit, z.b. 100 cent = entsprechen 1.00 eur)",
                ["Plugins.Payments.NovalnetApplePay.Fields.OnholdMinAmount.Hint"] = "Übersteigt der Bestellbetrag das genannte Limit, wird die Transaktion, bis zu ihrer Bestätigung durch Sie, auf on hold gesetzt. Sie können das Feld leer lassen, wenn Sie möchten, dass alle Transaktionen als on hold behandelt werden.",
                ["Plugins.Payments.NovalnetApplePay.Fields.BusinessName"] = "Name des Geschäfts",
                ["Plugins.Payments.NovalnetApplePay.Fields.BusinessName.Hint"] = "Der Name des Geschäfts wird in den Zahlungsbeleg von Apple Pay eingefügt und der Text wird als PAY 'Name des Geschäfts' angezeigt, so dass der Endkunde weiß, an wen er zahlt",
                ["Plugins.Payments.NovalnetApplePay.Fields.ButtonType"] = "Button-Typ",
                ["Plugins.Payments.NovalnetApplePay.Fields.ButtonTheme"] = "Button-Farbe",
                ["Plugins.Payments.NovalnetApplePay.Fields.ButtonHeight"] = "Button-Höhe",
                ["Plugins.Payments.NovalnetApplePay.Fields.ButtonHeight.Hint"] = "zwischen 30 und 64 Pixel",
                ["Plugins.Payments.NovalnetApplePay.Fields.ButtonCornerRadius"] = "Abrundungsgrad der Ecken des Buttons",
                ["Plugins.Payments.NovalnetApplePay.Fields.ButtonCornerRadius.Hint"] = "zwischen 0 und 10 Pixel",
                ["Plugins.Payments.NovalnetApplePay.OrderProcessedWithCard"] = "Ihre Bestellung wurde erfolgreich mit Apple Pay durchgeführt ({0} **** {1})",
                ["Plugins.Payments.NovalnetApplePay.Fields.DisplayPages"] = "Apple-Pay-Button anzeigen auf",
                ["Plugins.Payments.NovalnetApplePay.Fields.DisplayPages.Hint"] = "Auf den ausgewählten Seiten wird der Apple Pay-Button angezeigt um mit der Express-Checkout-Option sofort zu bezahlen",
                ["Plugins.Payments.NovalnetApplePay.DisplayPages.Cart"] = "Warenkorb",
                ["Plugins.Payments.NovalnetApplePay.DisplayPages.Product"] = "Produktseite",
                ["Plugins.Payments.NovalnetApplePay.ButtonType.Default"] = "Default",
                ["Plugins.Payments.NovalnetApplePay.ButtonType.Buy"] = "Kaufen",
                ["Plugins.Payments.NovalnetApplePay.ButtonType.Donate"] = "Spenden",
                ["Plugins.Payments.NovalnetApplePay.ButtonType.Book"] = "Buchen",
                ["Plugins.Payments.NovalnetApplePay.ButtonType.Contribute"] = "Beitragen",
                ["Plugins.Payments.NovalnetApplePay.ButtonType.Checkout"] = "Bezahlen",
                ["Plugins.Payments.NovalnetApplePay.ButtonType.Rent"] = "Mieten",
                ["Plugins.Payments.NovalnetApplePay.ButtonType.Support"] = "Unterstützen",
                ["Plugins.Payments.NovalnetApplePay.ButtonTheme.Dark"] = "Dunkel",
                ["Plugins.Payments.NovalnetApplePay.ButtonTheme.Light"] = "Hell",
                ["Plugins.Payments.NovalnetApplePay.ButtonTheme.LightOutline"] = "An Hintergrund anpassen",
                ["Plugins.Payments.NovalnetApplePay.CountryRestriction.ErrorText"] = "Apple Pay ist für das ausgewählte Land nicht verfügbar.",
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
        await _settingService.DeleteSettingAsync<NovalnetNovalnetApplePayPaymentSettings>();

        if (_widgetSettings.ActiveWidgetSystemNames.Contains("Payments.NovalnetApplePay"))
        {
            _widgetSettings.ActiveWidgetSystemNames.Remove("Payments.NovalnetApplePay");
            await _settingService.SaveSettingAsync(_widgetSettings);
        }

        //locales
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.NovalnetApplePay");

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

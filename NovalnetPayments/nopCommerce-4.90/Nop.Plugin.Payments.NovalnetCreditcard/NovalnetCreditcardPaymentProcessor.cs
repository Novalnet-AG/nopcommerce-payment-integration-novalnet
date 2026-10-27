using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.NovalnetCreditcard.Components;
using Nop.Plugin.Payments.NovalnetCreditcard.Models;
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

namespace Nop.Plugin.Payments.NovalnetCreditcard;

/// <summary>
/// NovalnetCreditcard payment processor
/// </summary>
public class NovalnetCreditcardPaymentProcessor : BasePlugin, IPaymentMethod
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderTotalCalculationService _orderTotalCalculationService;
    protected readonly ISettingService _settingService;
    protected readonly IWebHelper _webHelper;
    protected readonly NovalnetCreditcardPaymentSettings _novalnetCreditcardPaymentSettings;
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

    #endregion

    #region Ctor

    public NovalnetCreditcardPaymentProcessor(ILocalizationService localizationService,
        IOrderTotalCalculationService orderTotalCalculationService,
        ISettingService settingService,
        IWebHelper webHelper,
        NovalnetCreditcardPaymentSettings novalnetCreditcardPaymentSettings,
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
        ILanguageService languageService)
    {
        _localizationService = localizationService;
        _orderTotalCalculationService = orderTotalCalculationService;
        _settingService = settingService;
        _webHelper = webHelper;
        _novalnetCreditcardPaymentSettings = novalnetCreditcardPaymentSettings;
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
        string nnDoRedirect = session.GetString("nnDoRedirect");

        if (_novalnetCreditcardPaymentSettings.Enforce3d == true || nnDoRedirect == "1")
        {
            return new ProcessPaymentResult();
        }
        else
        {
            NovalnetPayload nnPayload = await NovalnetHelper.GetPaymentRequestParams(processPaymentRequest, _novalnetAPIConfigurationPaymentSettings, _customerService, _addressService, _workContext, _stateProvinceService, _currencyService, _countryService, _webHelper, _httpContextAccessor);

            var response = await SendPaymentRequest(nnPayload, false, _httpContextAccessor, _novalnetCreditcardPaymentSettings,  _novalnetAPIConfigurationPaymentSettings, _webHelper);
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
        var session = _httpContextAccessor.HttpContext?.Session;
        string tid = postProcessPaymentRequest.Order.AuthorizationTransactionId;
        Order order = postProcessPaymentRequest.Order;
        if (string.IsNullOrEmpty(tid))
        {
            NovalnetPayload nnPayload = await NovalnetHelper.GetPaymentRequestParams(postProcessPaymentRequest, _novalnetAPIConfigurationPaymentSettings, _customerService, _addressService, _workContext, _stateProvinceService, _currencyService, _countryService, _webHelper, _httpContextAccessor);
            
            var response = await SendPaymentRequest(nnPayload, true, _httpContextAccessor, _novalnetCreditcardPaymentSettings,  _novalnetAPIConfigurationPaymentSettings, _webHelper);

            NovalnetHelper.ClearSessionKeys(_httpContextAccessor, new[] { "nnDoRedirect", "nnPanHash", "nnUniqueId" });
            
			string redirectUrl = await NovalnetHelper.GetNovalnetRedirectUrl(response, postProcessPaymentRequest, _orderService, _orderProcessingService, _httpContextAccessor);

			_httpContextAccessor.HttpContext.Response.Redirect(redirectUrl);
			return;
        }
        else
        {
            NovalnetHelper.ClearSessionKeys(_httpContextAccessor, new[] { "nnDoRedirect", "nnPanHash", "nnUniqueId" });

            string paymentAccessKey = _novalnetAPIConfigurationPaymentSettings.PaymentAccessKey;
            await NovalnetHelper.UpdateOrderNumber(postProcessPaymentRequest.Order.CustomOrderNumber, tid, paymentAccessKey);

            JObject nnTransactionDetails = await NovalnetHelper.GetNovalnetTransactionDetails(tid, paymentAccessKey, _workContext);

            StringBuilder orderNoteString = await NovalnetHelper.GetTransactionNotes(nnTransactionDetails, _localizationService);
            await NovalnetHelper.InsertTransactionNotes(orderNoteString, postProcessPaymentRequest.Order.Id, _orderService);

            await NovalnetHelper.SetOrderPaymentStatus(order, _orderService, nnTransactionDetails["transaction"]["status"].ToString(), 
            nnTransactionDetails["transaction"]["payment_type"].ToString());
            
            await NovalnetHelper.InsertTransactionDetails(nnTransactionDetails, _novalnetTransactionDetailsService);
        }
    }

    public static async Task<JObject> SendPaymentRequest(NovalnetPayload nnPayload, bool redirectPayment, IHttpContextAccessor _httpContextAccessor, NovalnetCreditcardPaymentSettings _novalnetCreditcardPaymentSettings, NovalnetAPIConfigurationPaymentSettings _novalnetAPIConfigurationPaymentSettings, IWebHelper _webHelper)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        string nnPanHash = session.GetString("nnPanHash");
        string nnUniqueId = session.GetString("nnUniqueId");

        PaymentInfo paymentInfo = new PaymentInfo
        {
            pan_hash = nnPanHash,
            unique_id = nnUniqueId
        };

        nnPayload.transaction.payment_data = paymentInfo;
        nnPayload.transaction.test_mode = _novalnetCreditcardPaymentSettings.TestMode == true ? 1 : 0;

        if (redirectPayment == true)
        {
            if (_novalnetCreditcardPaymentSettings.Enforce3d == true)
            {
                nnPayload.transaction.enforce_3d = 1;
            }

            string storeLocation = _webHelper.GetStoreLocation();
            NovalnetHelper.GetRedirectPaymentParams(nnPayload, storeLocation);    
        }
        
        PaymentAction paymentAction = _novalnetCreditcardPaymentSettings.PaymentAction;
        int OnholdMinAmount = _novalnetCreditcardPaymentSettings.OnholdMinAmount;

        string endpoint = NovalnetHelper.GetNovalnetEndPoint(paymentAction, OnholdMinAmount, nnPayload.transaction.amount);

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
            _novalnetCreditcardPaymentSettings.AdditionalFee, _novalnetCreditcardPaymentSettings.AdditionalFeePercentage);
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
        var keys = new[] { "nnPanHash", "nnUniqueId", "nnDoRedirect" };

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
        return $"{_webHelper.GetStoreLocation()}Admin/NovalnetCreditcard/Configure";
    }

    /// <summary>
    /// Gets a type of a view component for displaying plugin in public store ("payment info" checkout step)
    /// </summary>
    /// <returns>View component type</returns>
    public Type GetPublicViewComponent()
    {
        return typeof(NovalnetCreditcardViewComponent);
    }

    /// <summary>
    /// Install the plugin
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task InstallAsync()
    {
        //settings
        var settings = new NovalnetCreditcardPaymentSettings{};
        await _settingService.SaveSettingAsync(settings);

        var allLanguages = await _languageService.GetAllLanguagesAsync();

        var englishLanguage = allLanguages.FirstOrDefault(lang => lang.UniqueSeoCode.Equals("en", StringComparison.InvariantCultureIgnoreCase));
        if (englishLanguage != null)
        {
            await _localizationService.SaveLocalizedFriendlyNameAsync(this, englishLanguage.Id, "Credit/Debit Cards");
        }
        
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Payments.NovalnetCreditcard.Fields.AdditionalFee"] = "Additional fee",
            ["Plugins.Payments.NovalnetCreditcard.Fields.AdditionalFee.Hint"] = "Enter additional fee to charge your customers.",
            ["Plugins.Payments.NovalnetCreditcard.Fields.AdditionalFeePercentage"] = "Additional fee. Use percentage",
            ["Plugins.Payments.NovalnetCreditcard.Fields.AdditionalFeePercentage.Hint"] = "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.",
            ["Plugins.Payments.NovalnetCreditcard.PaymentMethodDescription"] = "The amount will be debited from your credit/debit card",
            ["Plugins.Payments.NovalnetCreditcard.Fields.DescriptionText"] = "Description",
            ["Plugins.Payments.NovalnetCreditcard.Fields.DescriptionText.Hint"] = "Enter info that will be shown to customers during checkout",
            ["Plugins.Payments.NovalnetCreditcard.Fields.TestMode"] = "Enable test mode",
            ["Plugins.Payments.NovalnetCreditcard.Fields.TestMode.Hint"] = "The payment will be processed in the test mode therefore amount for this transaction will not be charged",
            ["Plugins.Payments.NovalnetCreditcard.Fields.PaymentAction"] = "Payment Action",
            ["Plugins.Payments.NovalnetCreditcard.Fields.PaymentAction.Hint"] = "Choose whether or not the payment should be charged immediately. Capture completes the transaction by transferring the funds from buyer account to merchant account. Authorize verifies payment details and reserves funds to capture it later, giving time for the merchant to decide on the order.",
            ["Plugins.Payments.NovalnetCreditcard.Fields.OnholdMinAmount"] = "Minimum transaction amount for authorization (in minimum unit of currency. E.g. enter 100 which is equal to 1.00)",
            ["Plugins.Payments.NovalnetCreditcard.Fields.OnholdMinAmount.Hint"] = "In case the order amount exceeds the mentioned limit, the transaction will be set on-hold till your confirmation of the transaction. You can leave the field empty if you wish to process all the transactions as on-hold.",
            ["Plugins.Payments.NovalnetCreditcard.Fields.Enforce3d"] = "Enforce 3D secure payment outside EU",
            ["Plugins.Payments.NovalnetCreditcard.Fields.Enforce3d.Hint"] = "By enabling this option, all payments from cards issued outside the EU will be authenticated via 3DS 2.0 SCA.",
            ["Plugins.Payments.NovalnetCreditcard.Fields.InlineForm"] = "Enable inline form",
            ["Plugins.Payments.NovalnetCreditcard.Fields.InlineForm.Hint"] = "Inline form: The following fields will be shown in the checkout in two lines: card holder & credit card number / expiry date / CVC",
            ["Plugins.Payments.NovalnetCreditcard.Fields.LabelStyle"] = "CSS settings for iframe form Label",
            ["Plugins.Payments.NovalnetCreditcard.Fields.LabelStyle.Hint"] = "CSS settings for iframe form Label",
            ["Plugins.Payments.NovalnetCreditcard.Fields.InputStyle"] = "CSS settings for iframe form Input",
            ["Plugins.Payments.NovalnetCreditcard.Fields.InputStyle.Hint"] = "CSS settings for iframe form Input",
            ["Plugins.Payments.NovalnetCreditcard.Fields.TextStyle"] = "CSS settings for iframe form text",
            ["Plugins.Payments.NovalnetCreditcard.Fields.TextStyle.Hint"] = "CSS settings for iframe form text",
            ["Plugins.Payments.NovalnetCreditcard.Cardholder.Label"] = "Card holder name",
            ["Plugins.Payments.NovalnetCreditcard.Cardholder.Placeholder"] = "Name on card",
            ["Plugins.Payments.NovalnetCreditcard.Cardnumber.Label"] = "Card number",
            ["Plugins.Payments.NovalnetCreditcard.Cardnumber.Placeholder"] = "XXXX XXXX XXXX XXXX",
            ["Plugins.Payments.NovalnetCreditcard.Expirydate.Label"] = "Expiry date",
            ["Plugins.Payments.NovalnetCreditcard.CVC.Label"] = "CVC/CVV/CID",
            ["Plugins.Payments.NovalnetCreditcard.CVC.Placeholder"] = "XXX",
        });

        var germanLanguage = allLanguages.FirstOrDefault(lang => lang.UniqueSeoCode.Equals("de", StringComparison.InvariantCultureIgnoreCase));
        if (germanLanguage != null)
        {
            await _localizationService.SaveLocalizedFriendlyNameAsync(this, germanLanguage.Id, "Kredit- / Debitkarte");
            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Payments.NovalnetCreditcard.Fields.AdditionalFee"] = "zusätzliche Gebühr",
                ["Plugins.Payments.NovalnetCreditcard.Fields.AdditionalFee.Hint"] = "Geben Sie die zusätzliche Gebühr ein, die Sie Ihren Kunden berechnen möchten.",
                ["Plugins.Payments.NovalnetCreditcard.Fields.AdditionalFeePercentage"] = "Zusätzliche Gebühr. Prozentsatz der Nutzung",
                ["Plugins.Payments.NovalnetCreditcard.Fields.AdditionalFeePercentage.Hint"] = "Legt fest, ob ein prozentualer Aufschlag auf den Gesamtbetrag der Bestellung angewendet wird. Wenn diese Option nicht aktiviert ist, wird ein fester Wert verwendet.",
                ["Plugins.Payments.NovalnetCreditcard.PaymentMethodDescription"] = "Der Betrag wird Ihrer Kredit-/Debitkarte belastet",
                ["Plugins.Payments.NovalnetCreditcard.Fields.DescriptionText"] = "Beschreibung",
                ["Plugins.Payments.NovalnetCreditcard.Fields.DescriptionText.Hint"] = "Geben Sie die Informationen ein, die den Kunden bei der Bezahlung angezeigt werden.",
                ["Plugins.Payments.NovalnetCreditcard.Fields.TestMode"] = "Testmodus aktivieren",
                ["Plugins.Payments.NovalnetCreditcard.Fields.TestMode.Hint"] = "Die Zahlung wird im Testmodus durchgeführt, daher wird der Betrag für diese Transaktion nicht eingezogen",
                ["Plugins.Payments.NovalnetCreditcard.Fields.PaymentAction"] = "Aktion für vom Besteller autorisierte Zahlungen",
                ["Plugins.Payments.NovalnetCreditcard.Fields.PaymentAction.Hint"] = "Wählen Sie, ob die Zahlung sofort belastet werden soll oder nicht. Zahlung einziehen: Betrag sofort belasten. Zahlung autorisieren: Die Zahlung wird überprüft und autorisiert, aber erst zu einem späteren Zeitpunkt belastet. So haben Sie Zeit, über die Bestellung zu entscheiden",
                ["Plugins.Payments.NovalnetCreditcard.Fields.OnholdMinAmount"] = "Mindesttransaktionsbetrag für die Autorisierung (in der kleinsten währungseinheit, z.b. 100 cent = entsprechen 1.00 eur)",
                ["Plugins.Payments.NovalnetCreditcard.Fields.OnholdMinAmount.Hint"] = "Übersteigt der Bestellbetrag das genannte Limit, wird die Transaktion, bis zu ihrer Bestätigung durch Sie, auf on hold gesetzt. Sie können das Feld leer lassen, wenn Sie möchten, dass alle Transaktionen als on hold behandelt werden.",
                ["Plugins.Payments.NovalnetCreditcard.Fields.Enforce3d"] = "3D-Secure-Zahlungen außerhalb der EU erzwingen",
                ["Plugins.Payments.NovalnetCreditcard.Fields.Enforce3d.Hint"] = "Wenn Sie diese Option aktivieren, werden alle Zahlungen mit Karten, die außerhalb der EU ausgegeben wurden, mit der starken Kundenauthentifizierung (Strong Customer Authentication, SCA) von 3D-Secure 2.0 authentifiziert.",
                ["Plugins.Payments.NovalnetCreditcard.Fields.InlineForm"] = "Inline-Formular ermöglichen",
                ["Plugins.Payments.NovalnetCreditcard.Fields.InlineForm.Hint"] = "Inline-Zahlungsformular: Die folgenden Felder werden im Checkout in zwei Zeilen angezeigt: Karteninhaber & Kreditkartennummer / Ablaufdatum / CVC-Code",
                ["Plugins.Payments.NovalnetCreditcard.Fields.LabelStyle"] = "CSS-Einstellungen für iframe-Formularbeschriftung",
                ["Plugins.Payments.NovalnetCreditcard.Fields.LabelStyle.Hint"] = "CSS-Einstellungen für iframe-Formularbeschriftung",
                ["Plugins.Payments.NovalnetCreditcard.Fields.InputStyle"] = "CSS-Einstellungen für iframe-Formular-Eingabe",
                ["Plugins.Payments.NovalnetCreditcard.Fields.InputStyle.Hint"] = "CSS-Einstellungen für iframe-Formular-Eingabe",
                ["Plugins.Payments.NovalnetCreditcard.Fields.TextStyle"] = "CSS-Einstellungen für iframe-Formulartext",
                ["Plugins.Payments.NovalnetCreditcard.Fields.TextStyle.Hint"] = "CSS-Einstellungen für iframe-Formulartext",
                ["Plugins.Payments.NovalnetCreditcard.Cardholder.Label"] = "Name des Karteninhabers",
                ["Plugins.Payments.NovalnetCreditcard.Cardholder.Placeholder"] = "Name auf der karte",
                ["Plugins.Payments.NovalnetCreditcard.Cardnumber.Label"] = "Kreditkartennummer",
                ["Plugins.Payments.NovalnetCreditcard.Cardnumber.Placeholder"] = "XXXX XXXX XXXX XXXX",
                ["Plugins.Payments.NovalnetCreditcard.Expirydate.Label"] = "Ablaufdatum",
                ["Plugins.Payments.NovalnetCreditcard.CVC.Label"] = "CVC/CVV/CID",
                ["Plugins.Payments.NovalnetCreditcard.CVC.Placeholder"] = "XXX",
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
        await _settingService.DeleteSettingAsync<NovalnetCreditcardPaymentSettings>();

        //locales
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.NovalnetCreditcard");

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

using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.NovalnetGuaranteedInvoice.Components;
using Nop.Plugin.Payments.NovalnetGuaranteedInvoice.Models;
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
using System.Globalization;

namespace Nop.Plugin.Payments.NovalnetGuaranteedInvoice;

/// <summary>
/// NovalnetGuaranteedInvoice payment processor
/// </summary>
public class NovalnetGuaranteedInvoicePaymentProcessor : BasePlugin, IPaymentMethod
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderTotalCalculationService _orderTotalCalculationService;
    protected readonly ISettingService _settingService;
    protected readonly IWebHelper _webHelper;
    protected readonly NovalnetGuaranteedInvoicePaymentSettings _novalnetGuaranteedInvoicePaymentSettings;
    private readonly IOrderService _orderService;
    private readonly ICustomerService _customerService;
    private readonly IAddressService _addressService;
    private readonly ICountryService _countryService;
    private readonly ICurrencyService _currencyService;
    private readonly IWorkContext _workContext;
    private readonly IStateProvinceService _stateProvinceService;
    private readonly NovalnetAPIConfigurationPaymentSettings _novalnetAPIConfigurationPaymentSettings;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPaymentPluginManager _paymentPluginManager;
    private readonly INovalnetTransactionDetailsService _novalnetTransactionDetailsService;
    protected readonly ILanguageService _languageService;
    private readonly IOrderProcessingService _orderProcessingService;

    #endregion

    #region Ctor

    public NovalnetGuaranteedInvoicePaymentProcessor(ILocalizationService localizationService,
        IOrderTotalCalculationService orderTotalCalculationService,
        ISettingService settingService,
        IWebHelper webHelper,
        NovalnetGuaranteedInvoicePaymentSettings novalnetGuaranteedInvoicePaymentSettings,
        IOrderService orderService,
        ICustomerService customerService,
        IAddressService addressService,
        ICurrencyService currencyService,
        IWorkContext workContext,
        ICountryService countryService,
        IStateProvinceService stateProvinceService,
        NovalnetAPIConfigurationPaymentSettings novalnetAPIConfigurationPaymentSettings,
        IHttpContextAccessor httpContextAccessor,
        IPaymentPluginManager paymentPluginManager,
        INovalnetTransactionDetailsService novalnetTransactionDetailsService,
        ILanguageService languageService,
        IOrderProcessingService orderProcessingService)
    {
        _localizationService = localizationService;
        _orderTotalCalculationService = orderTotalCalculationService;
        _settingService = settingService;
        _webHelper = webHelper;
        _novalnetGuaranteedInvoicePaymentSettings = novalnetGuaranteedInvoicePaymentSettings;
        _orderService = orderService;
        _customerService = customerService;
        _addressService = addressService;
        _currencyService = currencyService;
        _workContext = workContext;
        _countryService = countryService;
        _stateProvinceService = stateProvinceService;
        _novalnetAPIConfigurationPaymentSettings = novalnetAPIConfigurationPaymentSettings;
        _httpContextAccessor = httpContextAccessor;
        _paymentPluginManager = paymentPluginManager;
        _novalnetTransactionDetailsService = novalnetTransactionDetailsService;
        _languageService = languageService;
        _orderProcessingService = orderProcessingService;
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

        var session = _httpContextAccessor.HttpContext?.Session;
        
        if (string.IsNullOrEmpty(nnPayload.customer.billing.company))
        {
            string nnDob = session.GetString("nn_guaranteed_invoice_dob");
            nnPayload.customer.birth_date = nnDob;
        }

        nnPayload.transaction.test_mode = _novalnetGuaranteedInvoicePaymentSettings.TestMode == true ? 1 : 0;
        
        string jsonPayload = NovalnetHelper.SerializeToJson(nnPayload);

        PaymentAction paymentAction = _novalnetGuaranteedInvoicePaymentSettings.PaymentAction;
        int OnholdMinAmount = _novalnetGuaranteedInvoicePaymentSettings.OnholdMinAmount;

        string endpoint = NovalnetHelper.GetNovalnetEndPoint(paymentAction, OnholdMinAmount, nnPayload.transaction.amount);

        var response = await NovalnetHelper.SendRequestAsync(endpoint, jsonPayload, _novalnetAPIConfigurationPaymentSettings.PaymentAccessKey);

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
        NovalnetHelper.ClearSessionKeys(_httpContextAccessor, new[] { "nn_guaranteed_invoice_dob" });

        string paymentAccessKey = _novalnetAPIConfigurationPaymentSettings.PaymentAccessKey;

        var tid = postProcessPaymentRequest.Order.AuthorizationTransactionId;
        string orderNumber = postProcessPaymentRequest.Order.CustomOrderNumber;
        await NovalnetHelper.UpdateOrderNumber(orderNumber, tid, paymentAccessKey);

        JObject nnTransactionDetails = await NovalnetHelper.GetNovalnetTransactionDetails(tid, paymentAccessKey, _workContext);

        StringBuilder orderNoteString = await NovalnetHelper.GetTransactionNotes(nnTransactionDetails, _localizationService);
        
        await NovalnetHelper.InsertTransactionNotes(orderNoteString, postProcessPaymentRequest.Order.Id, _orderService);
        
        await NovalnetHelper.SetOrderPaymentStatus(postProcessPaymentRequest.Order, _orderService, nnTransactionDetails["transaction"]["status"].ToString(), nnTransactionDetails["transaction"]["payment_type"].ToString());

        await NovalnetHelper.InsertTransactionDetails(nnTransactionDetails, _novalnetTransactionDetailsService);
    }

    /// <summary>
    /// Returns a value indicating whether payment method should be hidden during checkout
    /// </summary>
    /// <param name="cart">Shopping cart</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains true - hide; false - display.
    /// </returns>
    public async Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
    {
        bool isNovalnetAPICredentialsAvailable = NovalnetHelper.CheckNovalnetAPICredentials(_novalnetAPIConfigurationPaymentSettings);
        if (!isNovalnetAPICredentialsAvailable)
        {
            return true;
        }

        var (orderTotal, _, _, _, _, _) = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart);
        int GuaranteedInvoiceMinAmount = _novalnetGuaranteedInvoicePaymentSettings.GuaranteedInvoiceMinAmount ?? 999;

        decimal orderTotalValue = orderTotal ?? 0m;

        bool displayGuaranteedPayment = await NovalnetHelper.DisplayGuaranteedPayment(_workContext, _customerService, _currencyService, _countryService, orderTotalValue, GuaranteedInvoiceMinAmount, _novalnetGuaranteedInvoicePaymentSettings.AllowB2B);

        return !displayGuaranteedPayment;
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
            _novalnetGuaranteedInvoicePaymentSettings.AdditionalFee, _novalnetGuaranteedInvoicePaymentSettings.AdditionalFeePercentage);
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
    public async Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
    {
        var errors = new List<string>();

        if (form.ContainsKey("nn_guaranteed_invoice_dob"))
        {
            var dob = form["nn_guaranteed_invoice_dob"].ToString()?.Trim();
            if (string.IsNullOrEmpty(dob))
            {
                string requiredFieldError = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.FillTheRequiredFields");
                errors.Add(requiredFieldError);
                return errors;
            }

            bool isValidDateFormat = DateTime.TryParseExact(
                dob,
                "dd.MM.yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _
            );

            if (!isValidDateFormat)
            {
                string DateFormatError = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.DateFormatErrorText");
                errors.Add(DateFormatError);
                return errors;
            }

            bool isOver18 = NovalnetHelper.IsOver18(dob.ToString());
            if (!isOver18)
            {
                string ageError = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.AgeErrorText");
                errors.Add(ageError);
                return errors;
            }
        }
        
        return errors;
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
        var request = new ProcessPaymentRequest();

        var session = _httpContextAccessor.HttpContext?.Session;

        if (form.TryGetValue("nn_guaranteed_invoice_dob", out var value))
        {
            if (DateTime.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
            {
                session.SetString("nn_guaranteed_invoice_dob", parsedDate.ToString("yyyy-MM-dd"));  // convert to yyyy-MM-dd
            }
        }

        return Task.FromResult(request);
    }

    /// <summary>
    /// Gets a configuration page URL
    /// </summary>
    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/NovalnetGuaranteedInvoice/Configure";
    }

    /// <summary>
    /// Gets a type of a view component for displaying plugin in public store ("payment info" checkout step)
    /// </summary>
    /// <returns>View component type</returns>
    public Type GetPublicViewComponent()
    {
        return typeof(NovalnetGuaranteedInvoiceViewComponent);
    }

    /// <summary>
    /// Install the plugin
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task InstallAsync()
    {
        //settings
        var settings = new NovalnetGuaranteedInvoicePaymentSettings
        {
            PaymentAction = PaymentAction.Capture
        };
        await _settingService.SaveSettingAsync(settings);
        
        var allLanguages = await _languageService.GetAllLanguagesAsync();
		var englishLanguage = allLanguages.FirstOrDefault(lang => lang.UniqueSeoCode.Equals("en", StringComparison.InvariantCultureIgnoreCase));
		if (englishLanguage != null)
		{
			await _localizationService.SaveLocalizedFriendlyNameAsync(this, englishLanguage.Id, "Invoice with payment guarantee");
		}

        //locales
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.AdditionalFee"] = "Additional fee",
            ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.AdditionalFee.Hint"] = "Enter additional fee to charge your customers.",
            ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.AdditionalFeePercentage"] = "Additional fee. Use percentage",
            ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.AdditionalFeePercentage.Hint"] = "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.",
            ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.PaymentAction"] = "Payment Action",
            ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.PaymentAction.Hint"] = "Choose whether or not the payment should be charged immediately. Capture completes the transaction by transferring the funds from buyer account to merchant account. Authorize verifies payment details and reserves funds to capture it later, giving time for the merchant to decide on the order.",
            ["Plugins.Payments.NovalnetGuaranteedInvoice.PaymentMethodDescription"] = "You will receive an e-mail with the Novalnet account details to complete the payment",
            ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.DescriptionText"] = "Description",
            ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.DescriptionText.Hint"] = "Enter info that will be shown to customers during checkout",
            ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.TestMode"] = "Enable test mode",
            ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.TestMode.Hint"] = "The payment will be processed in the test mode therefore amount for this transaction will not be charged",
            ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.AllowB2B"] = "Allow B2B Customers",
            ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.AllowB2B.Hint"] = "Allow B2B customers to place order",
            ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.OnholdMinAmount"] = "Minimum transaction amount for authorization",
            ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.OnholdMinAmount.Hint"] = "In case the order amount exceeds the mentioned limit, the transaction will be set on-hold till your confirmation of the transaction. You can leave the field empty if you wish to process all the transactions as on-hold.",
            ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.GuaranteedInvoiceMinAmount"] = "Minimum order amount for payment guarantee",
            ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.GuaranteedInvoiceMinAmount.Hint"] = "Enter the minimum amount (in cents) for the transaction to be processed with payment guarantee. For example, enter 100 which is equal to 1,00. By default, the amount will be 9,99 EUR",
        });

        var germanLanguage = allLanguages.FirstOrDefault(lang => lang.UniqueSeoCode.Equals("de", StringComparison.InvariantCultureIgnoreCase));
        if (germanLanguage != null)
        {
			await _localizationService.SaveLocalizedFriendlyNameAsync(this, germanLanguage.Id, "Rechnung mit Zahlungsgarantie");
            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.AdditionalFee"] = "zusätzliche Gebühr",
                ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.AdditionalFee.Hint"] = "Geben Sie die zusätzliche Gebühr ein, die Sie Ihren Kunden berechnen möchten.",
                ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.AdditionalFeePercentage"] = "Zusätzliche Gebühr. Prozentsatz der Nutzung",
                ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.AdditionalFeePercentage.Hint"] = "Legt fest, ob ein prozentualer Aufschlag auf den Gesamtbetrag der Bestellung angewendet wird. Wenn diese Option nicht aktiviert ist, wird ein fester Wert verwendet.",
                ["Plugins.Payments.NovalnetGuaranteedInvoice.PaymentMethodDescription"] = "Sie erhalten eine E-Mail mit den Bankdaten von Novalnet, um die Zahlung abzuschließen",
                ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.DescriptionText"] = "Beschreibung",
                ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.DescriptionText.Hint"] = "Geben Sie die Informationen ein, die den Kunden bei der Bezahlung angezeigt werden.",
                ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.TestMode"] = "Testmodus aktivieren",
                ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.TestMode.Hint"] = "Aktivieren Sie diese Option, um das Bezahlen auf Ihrer Checkout-Seite zu testen. Im Testmodus werden Zahlungen nicht von Novalnet ausgeführt. Vergessen Sie nicht, den Testmodus nach dem Testen wieder zu deaktivieren, um sicherzustellen, dass die echten Bestellungen ordnungsgemäß abgerechnet werden.",
                ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.AllowB2B"] = "B2B-Kunden erlauben",
                ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.AllowB2B.Hint"] = "B2B-Kunden erlauben, Bestellungen aufzugeben",
                ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.PaymentAction"] = "Aktion für vom Besteller autorisierte Zahlungen",
                ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.PaymentAction.Hint"] = "Wählen Sie, ob die Zahlung sofort belastet werden soll oder nicht. Zahlung einziehen: Betrag sofort belasten. Zahlung autorisieren: Die Zahlung wird überprüft und autorisiert, aber erst zu einem späteren Zeitpunkt belastet. So haben Sie Zeit, über die Bestellung zu entscheiden",
                ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.OnholdMinAmount"] = "Mindesttransaktionsbetrag für die Autorisierung",
                ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.OnholdMinAmount.Hint"] = "Übersteigt der Bestellbetrag das genannte Limit, wird die Transaktion, bis zu ihrer Bestätigung durch Sie, auf on hold gesetzt. Sie können das Feld leer lassen, wenn Sie möchten, dass alle Transaktionen als on hold behandelt werden.",
                ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.GuaranteedInvoiceMinAmount"] = "Mindestbestellbetrag für Zahlungsgarantie",
                ["Plugins.Payments.NovalnetGuaranteedInvoice.Fields.GuaranteedInvoiceMinAmount.Hint"] = "Geben Sie den Mindestbetrag (in Cent) für die zu bearbeitende Transaktion mit Zahlungsgarantie ein. Geben Sie z.B. 100 ein, was 1,00 entspricht. Der Standbetrag ist 9,99 EUR",
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
        await _settingService.DeleteSettingAsync<NovalnetGuaranteedInvoicePaymentSettings>();

        //locales
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.NovalnetGuaranteedInvoice");

        await base.UninstallAsync();
    }

    /// <summary>
    /// Gets a payment method description that will be displayed on checkout pages in the public store
    /// </summary>
    /// <remarks>
    /// return description of this payment method to be display on "payment method" checkout step. good practice is to make it localizable
    /// for example, for a redirection payment method, description may be like this: "You will be redirected to PayPal site to complete the payment"
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

    /// <summary>#endregion
    /// Gets a value indicating whether partial refund is supported
    /// </summary>Nop.Plugin.Payments.NovalnetGuaranteedInvoice.NovalnetGuaranteedInvoicePaymentSettings;
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
    /// </summary>
    public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

    /// <summary>
    /// Gets a payment method type
    /// </summary>
    public PaymentMethodType PaymentMethodType => PaymentMethodType.Standard;

    /// <summary>
    /// Gets a value indicating whether we should display a payment information page for this plugin
    /// </summary>
    public bool SkipPaymentInfo => false;

    #endregion
}

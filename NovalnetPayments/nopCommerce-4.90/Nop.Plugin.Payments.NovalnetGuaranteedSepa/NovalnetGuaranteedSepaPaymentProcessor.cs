using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.NovalnetGuaranteedSepa.Components;
using Nop.Plugin.Payments.NovalnetGuaranteedSepa.Models;
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

namespace Nop.Plugin.Payments.NovalnetGuaranteedSepa;

/// <summary>
/// NovalnetGuaranteedSepa payment processor
/// </summary>
public class NovalnetGuaranteedSepaPaymentProcessor : BasePlugin, IPaymentMethod
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderTotalCalculationService _orderTotalCalculationService;
    protected readonly ISettingService _settingService;
    protected readonly IWebHelper _webHelper;
    protected readonly NovalnetGuaranteedSepaPaymentSettings _novalnetGuaranteedSepaPaymentSettings;
    private readonly IOrderService _orderService;
    private readonly ICustomerService _customerService;
    private readonly IAddressService _addressService;
    private readonly ICountryService _countryService;
    private readonly ICurrencyService _currencyService;
    private readonly IWorkContext _workContext;
    private readonly IStateProvinceService _stateProvinceService;
    private readonly NovalnetAPIConfigurationPaymentSettings _novalnetAPIConfigurationPaymentSettings;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly INovalnetTransactionDetailsService _novalnetTransactionDetailsService;
    protected readonly ILanguageService _languageService;
    private readonly IOrderProcessingService _orderProcessingService;

    #endregion

    #region Ctor

    public NovalnetGuaranteedSepaPaymentProcessor(ILocalizationService localizationService,
        IOrderTotalCalculationService orderTotalCalculationService,
        ISettingService settingService,
        IWebHelper webHelper,
        NovalnetGuaranteedSepaPaymentSettings novalnetGuaranteedSepaPaymentSettings,
        IOrderService orderService,
        ICustomerService customerService,
        IAddressService addressService,
        ICurrencyService currencyService,
        IWorkContext workContext,
        ICountryService countryService,
        IStateProvinceService stateProvinceService,
        NovalnetAPIConfigurationPaymentSettings novalnetAPIConfigurationPaymentSettings,
        IHttpContextAccessor httpContextAccessor,
        INovalnetTransactionDetailsService novalnetTransactionDetailsService,
        ILanguageService languageService,
        IOrderProcessingService orderProcessingService)
    {
        _localizationService = localizationService;
        _orderTotalCalculationService = orderTotalCalculationService;
        _settingService = settingService;
        _webHelper = webHelper;
        _novalnetGuaranteedSepaPaymentSettings = novalnetGuaranteedSepaPaymentSettings;
        _orderService = orderService;
        _customerService = customerService;
        _addressService = addressService;
        _currencyService = currencyService;
        _workContext = workContext;
        _countryService = countryService;
        _stateProvinceService = stateProvinceService;
        _novalnetAPIConfigurationPaymentSettings = novalnetAPIConfigurationPaymentSettings;
        _httpContextAccessor = httpContextAccessor;
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
        string nnIban = session.GetString("nn_guaranteed_sepa_iban");
        string nnBic = session.GetString("nn_guaranteed_sepa_bic");
        string nnAccountHolder = session.GetString("nn_guaranteed_sepa_account_holder");
        
        PaymentInfo paymentInfo = new PaymentInfo
        {
            iban = nnIban
        };

        if (!string.IsNullOrEmpty(nnBic) && NovalnetHelper.RequiresBic(nnIban))
            paymentInfo.bic = nnBic;

        if (!string.IsNullOrEmpty(nnAccountHolder))
            paymentInfo.account_holder = nnAccountHolder;

        nnPayload.transaction.payment_data = paymentInfo;

        if (string.IsNullOrEmpty(nnPayload.customer.billing.company))
        {
            string nnDob = session.GetString("nn_guaranteed_sepa_dob");
            nnPayload.customer.birth_date = nnDob;
        }

        nnPayload.transaction.test_mode = _novalnetGuaranteedSepaPaymentSettings.TestMode == true ? 1 : 0;
        
        string jsonPayload = NovalnetHelper.SerializeToJson(nnPayload);

        PaymentAction paymentAction = _novalnetGuaranteedSepaPaymentSettings.PaymentAction;
        int OnholdMinAmount = _novalnetGuaranteedSepaPaymentSettings.OnholdMinAmount;

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
        NovalnetHelper.ClearSessionKeys(_httpContextAccessor, new[] { "nn_guaranteed_sepa_iban", "nn_guaranteed_sepa_bic", "nn_guaranteed_sepa_dob", "nn_guaranteed_sepa_account_holder" });

        string paymentAccessKey = _novalnetAPIConfigurationPaymentSettings.PaymentAccessKey;

        string tid = postProcessPaymentRequest.Order.AuthorizationTransactionId;
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
        int GuaranteedSepaMinAmount = _novalnetGuaranteedSepaPaymentSettings.GuaranteedSepaMinAmount ?? 999;

        decimal orderTotalValue = orderTotal ?? 0m;

        bool displayGuaranteedPayment = await NovalnetHelper.DisplayGuaranteedPayment(_workContext, _customerService, _currencyService, _countryService, orderTotalValue, GuaranteedSepaMinAmount, _novalnetGuaranteedSepaPaymentSettings.AllowB2B);

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
            _novalnetGuaranteedSepaPaymentSettings.AdditionalFee, _novalnetGuaranteedSepaPaymentSettings.AdditionalFeePercentage);
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

        var iban = form["nn_guaranteed_sepa_iban"].ToString()?.Trim();
        var bic = form["nn_guaranteed_sepa_bic"].ToString()?.Trim();
        var accountHolder = form["nn_guaranteed_sepa_account_holder"].ToString()?.Trim();

        string requiredFieldError = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.FillTheRequiredFields");

        if (string.IsNullOrEmpty(iban) || string.IsNullOrEmpty(accountHolder) || (NovalnetHelper.RequiresBic(iban) && string.IsNullOrEmpty(bic)))
        {
            errors.Add(requiredFieldError);
        }

        if (form.ContainsKey("nn_guaranteed_sepa_dob"))
        {
            var dob = form["nn_guaranteed_sepa_dob"].ToString()?.Trim();
            if (string.IsNullOrEmpty(dob))
            {
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

            if (!isValidDateFormat) {
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

        // Extract a custom field from the form
        var keys = new[] { "nn_guaranteed_sepa_iban", "nn_guaranteed_sepa_bic", "nn_guaranteed_sepa_account_holder" };

        foreach (var key in keys)
        {
            if (form.TryGetValue(key, out var fieldValue))
            {
                session.SetString(key, fieldValue.ToString());
            }
        }

        if (form.TryGetValue("nn_guaranteed_sepa_dob", out var value))
        {
            if (DateTime.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
            {
                session.SetString("nn_guaranteed_sepa_dob", parsedDate.ToString("yyyy-MM-dd"));  // convert to yyyy-MM-dd
            }
        }

        return Task.FromResult(request);
    }

    /// <summary>
    /// Gets a configuration page URL
    /// </summary>
    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/NovalnetGuaranteedSepa/Configure";
    }

    /// <summary>
    /// Gets a type of a view component for displaying plugin in public store ("payment info" checkout step)
    /// </summary>
    /// <returns>View component type</returns>
    public Type GetPublicViewComponent()
    {
        return typeof(NovalnetGuaranteedSepaViewComponent);
    }

    /// <summary>
    /// Install the plugin
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task InstallAsync()
    {
        //settings
        var settings = new NovalnetGuaranteedSepaPaymentSettings
        {
            PaymentAction = PaymentAction.Capture
        };
        await _settingService.SaveSettingAsync(settings);

		var allLanguages = await _languageService.GetAllLanguagesAsync();

		var englishLanguage = allLanguages.FirstOrDefault(lang => lang.UniqueSeoCode.Equals("en", StringComparison.InvariantCultureIgnoreCase));

		if (englishLanguage != null)
		{
			await _localizationService.SaveLocalizedFriendlyNameAsync(this, englishLanguage.Id, "Direct Debit SEPA with payment guarantee");
		}
        //locales
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.AdditionalFee"] = "Additional fee",
            ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.AdditionalFee.Hint"] = "Enter additional fee to charge your customers.",
            ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.AdditionalFeePercentage"] = "Additional fee. Use percentage",
            ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.AdditionalFeePercentage.Hint"] = "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.",
            ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.PaymentAction"] = "Payment Action",
            ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.PaymentAction.Hint"] = "Choose whether or not the payment should be charged immediately. Capture completes the transaction by transferring the funds from buyer account to merchant account. Authorize verifies payment details and reserves funds to capture it later, giving time for the merchant to decide on the order.",
            ["Plugins.Payments.NovalnetGuaranteedSepa.PaymentMethodDescription"] = "The amount will be debited from your account by Novalnet",
            ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.DescriptionText"] = "Description",
            ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.DescriptionText.Hint"] = "Enter info that will be shown to customers during checkout",
            ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.TestMode"] = "Enable test mode",
            ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.TestMode.Hint"] = "The payment will be processed in the test mode therefore amount for this transaction will not be charged",
            ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.AllowB2B"] = "Allow B2B Customers",
            ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.AllowB2B.Hint"] = "Allow B2B customers to place order",
            ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.OnholdMinAmount"] = "Minimum transaction amount for authorization (in minimum unit of currency. E.g. enter 100 which is equal to 1.00)",
            ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.OnholdMinAmount.Hint"] = "In case the order amount exceeds the mentioned limit, the transaction will be set on-hold till your confirmation of the transaction. You can leave the field empty if you wish to process all the transactions as on-hold.",
            ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.GuaranteedSepaMinAmount"] = "Minimum order amount for payment guarantee",
            ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.GuaranteedSepaMinAmount.Hint"] = "Enter the minimum amount (in cents) for the transaction to be processed with payment guarantee. For example, enter 100 which is equal to 1,00. By default, the amount will be 9,99 EUR",
        });

        var germanLanguage = allLanguages.FirstOrDefault(lang => lang.UniqueSeoCode.Equals("de", StringComparison.InvariantCultureIgnoreCase));
        if (germanLanguage != null)
        {
			await _localizationService.SaveLocalizedFriendlyNameAsync(this, germanLanguage.Id, "SEPA-Lastschrift mit Zahlungsgarantie");
            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.AdditionalFee"] = "zusätzliche Gebühr",
                ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.AdditionalFee.Hint"] = "Geben Sie die zusätzliche Gebühr ein, die Sie Ihren Kunden berechnen möchten.",
                ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.AdditionalFeePercentage"] = "Zusätzliche Gebühr. Prozentsatz der Nutzung",
                ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.AdditionalFeePercentage.Hint"] = "Legt fest, ob ein prozentualer Aufschlag auf den Gesamtbetrag der Bestellung angewendet wird. Wenn diese Option nicht aktiviert ist, wird ein fester Wert verwendet.",
                ["Plugins.Payments.NovalnetGuaranteedSepa.PaymentMethodDescription"] = "Der Betrag wird durch Novalnet von Ihrem Konto abgebucht",
                ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.DescriptionText"] = "Beschreibung",
                ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.DescriptionText.Hint"] = "Geben Sie die Informationen ein, die den Kunden bei der Bezahlung angezeigt werden.",
                ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.TestMode"] = "Testmodus aktivieren",
                ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.TestMode.Hint"] = "Die Zahlung wird im Testmodus durchgeführt, daher wird der Betrag für diese Transaktion nicht eingezogen",
                ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.AllowB2B"] = "B2B-Kunden erlauben",
                ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.AllowB2B.Hint"] = "B2B-Kunden erlauben, Bestellungen aufzugeben",
                ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.PaymentAction"] = "Aktion für vom Besteller autorisierte Zahlungen",
                ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.PaymentAction.Hint"] = "Wählen Sie, ob die Zahlung sofort belastet werden soll oder nicht. Zahlung einziehen: Betrag sofort belasten. Zahlung autorisieren: Die Zahlung wird überprüft und autorisiert, aber erst zu einem späteren Zeitpunkt belastet. So haben Sie Zeit, über die Bestellung zu entscheiden",
                ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.OnholdMinAmount"] = "Mindesttransaktionsbetrag für die Autorisierung (in der kleinsten währungseinheit, z.b. 100 cent = entsprechen 1.00 eur)",
                ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.OnholdMinAmount.Hint"] = "Übersteigt der Bestellbetrag das genannte Limit, wird die Transaktion, bis zu ihrer Bestätigung durch Sie, auf on hold gesetzt. Sie können das Feld leer lassen, wenn Sie möchten, dass alle Transaktionen als on hold behandelt werden.",
                ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.GuaranteedSepaMinAmount"] = "Mindestbestellbetrag für Zahlungsgarantie",
                ["Plugins.Payments.NovalnetGuaranteedSepa.Fields.GuaranteedSepaMinAmount.Hint"] = "Geben Sie den Mindestbetrag (in Cent) für die zu bearbeitende Transaktion mit Zahlungsgarantie ein. Geben Sie z.B. 100 ein, was 1,00 entspricht. Der Standbetrag ist 9,99 EUR",
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
        await _settingService.DeleteSettingAsync<NovalnetGuaranteedSepaPaymentSettings>();

        //locales
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.NovalnetGuaranteedSepa");

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

    /// <summary>#endregion
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

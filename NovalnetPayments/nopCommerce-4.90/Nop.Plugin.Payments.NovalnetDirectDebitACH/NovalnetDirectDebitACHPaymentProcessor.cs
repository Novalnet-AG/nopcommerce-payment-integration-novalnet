using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.NovalnetDirectDebitACH.Components;
using Nop.Plugin.Payments.NovalnetDirectDebitACH.Models;
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

namespace Nop.Plugin.Payments.NovalnetDirectDebitACH;

/// <summary>
/// NovalnetDirectDebitACH payment processor
/// </summary>
public class NovalnetDirectDebitACHPaymentProcessor : BasePlugin, IPaymentMethod
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderTotalCalculationService _orderTotalCalculationService;
    protected readonly ISettingService _settingService;
    protected readonly IWebHelper _webHelper;
    protected readonly NovalnetDirectDebitACHPaymentSettings _novalnetDirectDebitACHPaymentSettings;
    private readonly IOrderService _orderService;
    private readonly ICustomerService _customerService;
    private readonly IAddressService _addressService;
    private readonly ICountryService _countryService;
    private readonly ICurrencyService _currencyService;
    private readonly IWorkContext _workContext;
    private readonly IStateProvinceService _stateProvinceService;
    private readonly NovalnetAPIConfigurationPaymentSettings _novalnetAPIConfigurationPaymentSettings;
    private readonly IHttpContextAccessor _httpContextAccessor;
    protected readonly IStoreContext _storeContext;
    private readonly IPaymentPluginManager _paymentPluginManager;
    private readonly INovalnetTransactionDetailsService _novalnetTransactionDetailsService;
    protected readonly ILanguageService _languageService;
    private readonly IOrderProcessingService _orderProcessingService;

    #endregion

    #region Ctor

    public NovalnetDirectDebitACHPaymentProcessor(ILocalizationService localizationService,
        IOrderTotalCalculationService orderTotalCalculationService,
        ISettingService settingService,
        IWebHelper webHelper,
        NovalnetDirectDebitACHPaymentSettings novalnetDirectDebitACHPaymentSettings,
        IOrderService orderService,
        ICustomerService customerService,
        IAddressService addressService,
        ICurrencyService currencyService,
        IWorkContext workContext,
        ICountryService countryService,
        IStateProvinceService stateProvinceService,
        NovalnetAPIConfigurationPaymentSettings novalnetAPIConfigurationPaymentSettings,
        IHttpContextAccessor httpContextAccessor,
        IStoreContext storeContext,
        IPaymentPluginManager paymentPluginManager,
        INovalnetTransactionDetailsService novalnetTransactionDetailsService,
        ILanguageService languageService,
        IOrderProcessingService orderProcessingService)
    {
        _localizationService = localizationService;
        _orderTotalCalculationService = orderTotalCalculationService;
        _settingService = settingService;
        _webHelper = webHelper;
        _novalnetDirectDebitACHPaymentSettings = novalnetDirectDebitACHPaymentSettings;
        _orderService = orderService;
        _customerService = customerService;
        _addressService = addressService;
        _currencyService = currencyService;
        _workContext = workContext;
        _countryService = countryService;
        _stateProvinceService = stateProvinceService;
        _novalnetAPIConfigurationPaymentSettings = novalnetAPIConfigurationPaymentSettings;
        _httpContextAccessor = httpContextAccessor;
        _storeContext = storeContext;
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
        string nnAccountHolder = session.GetString("nn_ach_account_holder");
        string nnAccountNumber = session.GetString("nn_ach_account_number");
        string nnRoutingNumber = session.GetString("nn_ach_routing_number");

        PaymentInfo paymentInfo = new PaymentInfo
        {
            account_number = nnAccountNumber,
            routing_number = nnRoutingNumber,
        };

        if (!string.IsNullOrEmpty(nnAccountHolder))
        {
            paymentInfo.account_holder = nnAccountHolder;
        }

        nnPayload.transaction.payment_data = paymentInfo;

        nnPayload.transaction.test_mode = _novalnetDirectDebitACHPaymentSettings.TestMode == true ? 1 : 0;
        
        string jsonPayload = NovalnetHelper.SerializeToJson(nnPayload);

        var response = await NovalnetHelper.SendRequestAsync("/v2/payment", jsonPayload, _novalnetAPIConfigurationPaymentSettings.PaymentAccessKey);

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
        NovalnetHelper.ClearSessionKeys(_httpContextAccessor, new[] { "nn_ach_account_holder", "nn_ach_account_number", "nn_ach_routing_number" });
        string paymentAccessKey = _novalnetAPIConfigurationPaymentSettings.PaymentAccessKey;

        var tid = postProcessPaymentRequest.Order.AuthorizationTransactionId;
        string orderNumber = postProcessPaymentRequest.Order.CustomOrderNumber;
        await NovalnetHelper.UpdateOrderNumber(orderNumber, tid, paymentAccessKey);

        JObject nnTransactionDetails = await NovalnetHelper.GetNovalnetTransactionDetails(tid, paymentAccessKey, _workContext);
        
		await NovalnetHelper.SetOrderPaymentStatus(postProcessPaymentRequest.Order, _orderService, nnTransactionDetails["transaction"]["status"].ToString(), nnTransactionDetails["transaction"]["payment_type"].ToString());
            
        StringBuilder orderNoteString = await NovalnetHelper.GetTransactionNotes(nnTransactionDetails, _localizationService);
        await NovalnetHelper.InsertTransactionNotes(orderNoteString, postProcessPaymentRequest.Order.Id, _orderService);
        
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
    public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
    {
        bool isNovalnetAPICredentialsAvailable = NovalnetHelper.CheckNovalnetAPICredentials(_novalnetAPIConfigurationPaymentSettings);
        if (!isNovalnetAPICredentialsAvailable)
        {
            return Task.FromResult(true);
        }
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
            _novalnetDirectDebitACHPaymentSettings.AdditionalFee, _novalnetDirectDebitACHPaymentSettings.AdditionalFeePercentage);
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

        string nnAccountNumber = form["nn_ach_account_number"].ToString()?.Trim();
        string nnRoutingNumber = form["nn_ach_routing_number"].ToString()?.Trim();
        string accountHolder = form["nn_ach_account_holder"].ToString()?.Trim();

        if (string.IsNullOrEmpty(nnAccountNumber) || string.IsNullOrEmpty(nnRoutingNumber) || string.IsNullOrEmpty(accountHolder)) {
            string requiredFieldError = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.FillTheRequiredFields");
            errors.Add(requiredFieldError);
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
        var keys = new[] { "nn_ach_account_holder", "nn_ach_account_number", "nn_ach_routing_number" };

        foreach (var key in keys)
        {
            if (form.TryGetValue(key, out var value))
            {
                session.SetString(key, value.ToString());
            }
        }

        return Task.FromResult(request);
    }

    /// <summary>
    /// Gets a configuration page URL
    /// </summary>
    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/NovalnetDirectDebitACH/Configure";
    }

    /// <summary>
    /// Gets a type of a view component for displaying plugin in public store ("payment info" checkout step)
    /// </summary>
    /// <returns>View component type</returns>
    public Type GetPublicViewComponent()
    {
        return typeof(NovalnetDirectDebitACHViewComponent);
    }

    /// <summary>
    /// Install the plugin
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task InstallAsync()
    {
        //settings
        var settings = new NovalnetDirectDebitACHPaymentSettings();        
        await _settingService.SaveSettingAsync(settings);
        
        var allLanguages = await _languageService.GetAllLanguagesAsync();

		var englishLanguage = allLanguages.FirstOrDefault(lang => lang.UniqueSeoCode.Equals("en", StringComparison.InvariantCultureIgnoreCase));
		if (englishLanguage != null)
		{
			await _localizationService.SaveLocalizedFriendlyNameAsync(this, englishLanguage.Id, "Direct Debit ACH");
		}

        //locales
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Payments.NovalnetDirectDebitACH.Fields.AdditionalFee"] = "Additional fee",
            ["Plugins.Payments.NovalnetDirectDebitACH.Fields.AdditionalFee.Hint"] = "Enter additional fee to charge your customers.",
            ["Plugins.Payments.NovalnetDirectDebitACH.Fields.AdditionalFeePercentage"] = "Additional fee. Use percentage",
            ["Plugins.Payments.NovalnetDirectDebitACH.Fields.AdditionalFeePercentage.Hint"] = "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.",
            ["Plugins.Payments.NovalnetDirectDebitACH.PaymentMethodDescription"] = "The amount will be debited from your account by Novalnet",
            ["Plugins.Payments.NovalnetDirectDebitACH.Fields.DescriptionText"] = "Description",
            ["Plugins.Payments.NovalnetDirectDebitACH.Fields.DescriptionText.Hint"] = "Enter info that will be shown to customers during checkout",
            ["Plugins.Payments.NovalnetDirectDebitACH.Fields.TestMode"] = "Enable test mode",
            ["Plugins.Payments.NovalnetDirectDebitACH.Fields.TestMode.Hint"] = "The payment will be processed in the test mode therefore amount for this transaction will not be charged",
        });

        var germanLanguage = allLanguages.FirstOrDefault(lang => lang.UniqueSeoCode.Equals("de", StringComparison.InvariantCultureIgnoreCase));
        if (germanLanguage != null)
        {
			await _localizationService.SaveLocalizedFriendlyNameAsync(this, germanLanguage.Id, "Lastschrift ACH");
            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Payments.NovalnetDirectDebitACH.Fields.AdditionalFee"] = "zusätzliche Gebühr",
                ["Plugins.Payments.NovalnetDirectDebitACH.Fields.AdditionalFee.Hint"] = "Geben Sie die zusätzliche Gebühr ein, die Sie Ihren Kunden berechnen möchten.",
                ["Plugins.Payments.NovalnetDirectDebitACH.Fields.AdditionalFeePercentage"] = "Zusätzliche Gebühr. Prozentsatz der Nutzung",
                ["Plugins.Payments.NovalnetDirectDebitACH.Fields.AdditionalFeePercentage.Hint"] = "Legt fest, ob ein prozentualer Aufschlag auf den Gesamtbetrag der Bestellung angewendet wird. Wenn diese Option nicht aktiviert ist, wird ein fester Wert verwendet.",
                ["Plugins.Payments.NovalnetDirectDebitACH.PaymentMethodDescription"] = "Der Betrag wird durch Novalnet von Ihrem Konto abgebucht",
                ["Plugins.Payments.NovalnetDirectDebitACH.Fields.DescriptionText"] = "Beschreibung",
                ["Plugins.Payments.NovalnetDirectDebitACH.Fields.DescriptionText.Hint"] = "Geben Sie die Informationen ein, die den Kunden bei der Bezahlung angezeigt werden.",
                ["Plugins.Payments.NovalnetDirectDebitACH.Fields.TestMode"] = "Testmodus aktivieren",
                ["Plugins.Payments.NovalnetDirectDebitACH.Fields.TestMode.Hint"] = "Die Zahlung wird im Testmodus durchgeführt, daher wird der Betrag für diese Transaktion nicht eingezogen",
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
        await _settingService.DeleteSettingAsync<NovalnetDirectDebitACHPaymentSettings>();

        //locales
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.NovalnetDirectDebitACH");

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

using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.NovalnetAPIConfiguration.Models;
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
using Microsoft.AspNetCore.Hosting;


namespace Nop.Plugin.Payments.NovalnetAPIConfiguration;

/// <summary>
/// NovalnetAPIConfiguration payment processor
/// </summary>
public class NovalnetAPIConfigurationPaymentProcessor : BasePlugin, IPaymentMethod
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderTotalCalculationService _orderTotalCalculationService;
    protected readonly ISettingService _settingService;
    protected readonly IWebHelper _webHelper;
    protected readonly NovalnetAPIConfigurationPaymentSettings _novalnetAPIConfigurationPaymentSettings;
    private readonly IOrderService _orderService;
    private readonly ICustomerService _customerService;
    private readonly IAddressService _addressService;
    private readonly ICountryService _countryService;
    private readonly ICurrencyService _currencyService;
    private readonly IWorkContext _workContext;
    private readonly IStateProvinceService _stateProvinceService;
    private readonly IWebHostEnvironment _webHostEnvironment;
    protected readonly ILanguageService _languageService;

    #endregion

    #region Ctor

    public NovalnetAPIConfigurationPaymentProcessor(ILocalizationService localizationService,
        IOrderTotalCalculationService orderTotalCalculationService,
        ISettingService settingService,
        IWebHelper webHelper,
        NovalnetAPIConfigurationPaymentSettings novalnetAPIConfigurationPaymentSettings,
        IOrderService orderService,
        ICustomerService customerService,
        IAddressService addressService,
        ICurrencyService currencyService,
        IWorkContext workContext,
        ICountryService countryService,
        IStateProvinceService stateProvinceService,
        IWebHostEnvironment webHostEnvironment,
        ILanguageService languageService)
    {
        _localizationService = localizationService;
        _orderTotalCalculationService = orderTotalCalculationService;
        _settingService = settingService;
        _webHelper = webHelper;
        _novalnetAPIConfigurationPaymentSettings = novalnetAPIConfigurationPaymentSettings;
        _orderService = orderService;
        _customerService = customerService;
        _addressService = addressService;
        _currencyService = currencyService;
        _workContext = workContext;
        _countryService = countryService;
        _stateProvinceService = stateProvinceService;
        _webHostEnvironment = webHostEnvironment;
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
    public Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        var result = new ProcessPaymentResult();
        return Task.FromResult(result);
    }

    /// <summary>
    /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
    /// </summary>
    /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
    {
        //nothing
        return Task.CompletedTask;
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
        //you can put any logic here
        //for example, hide this payment method if all products in the cart are downloadable
        //or hide this payment method if current customer is from certain country
        return Task.FromResult(true);
    }

    /// <summary>
    /// Gets additional handling fee
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the additional handling fee
    /// </returns>
    public Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
    {
        return Task.FromResult(decimal.Zero);
    }

    /// <summary>
    /// Captures payment
    /// </summary>
    /// <param name="capturePaymentRequest">Capture payment request</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the capture payment result
    /// </returns>
    public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
    {
        return Task.FromResult(new CapturePaymentResult { Errors = new[] { "Capture method not supported" } });
    }

    /// <summary>
    /// Refunds a payment
    /// </summary>
    /// <param name="refundPaymentRequest">Request</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
    {
        return Task.FromResult(new RefundPaymentResult { Errors = new[] { "Refund method not supported" } });
    }

    /// <summary>
    /// Voids a payment
    /// </summary>
    /// <param name="voidPaymentRequest">Request</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result
    /// </returns>
    public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
    {
        return Task.FromResult(new VoidPaymentResult { Errors = new[] { "Void method not supported" } });
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
        var result = new ProcessPaymentResult
        {
        };

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
        return Task.FromResult(new ProcessPaymentRequest());
    }

    /// <summary>
    /// Gets a configuration page URL
    /// </summary>
    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/NovalnetAPIConfiguration/Configure";
    }

    /// <summary>
    /// Gets a type of a view component for displaying plugin in public store ("payment info" checkout step)
    /// </summary>
    /// <returns>View component type</returns>
    public Type GetPublicViewComponent()
    {
        return null;
    }

    /// <summary>
    /// Install the plugin
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task InstallAsync()
    {
        string storeUrl = _webHelper.GetStoreLocation();
        string webhookUrl = $"{storeUrl}NovalnetWebhook/WebhookHandler";

        //settings
        var settings = new NovalnetAPIConfigurationPaymentSettings
        {
            WebhookUrl = webhookUrl
        };
        await _settingService.SaveSettingAsync(settings);

        var allLanguages = await _languageService.GetAllLanguagesAsync();

        var englishLanguage = allLanguages.FirstOrDefault(lang => lang.UniqueSeoCode.Equals("en", StringComparison.InvariantCultureIgnoreCase));
        if (englishLanguage != null)
        {
            await _localizationService.SaveLocalizedFriendlyNameAsync(this, englishLanguage.Id, "Credit/Debit Cards");
        }

        //locales
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Payment.NovalnetAPIConfiguration.Fields.ProductActivationKey"] = "Product activation key",
            ["Plugins.Payment.NovalnetAPIConfiguration.Fields.ProductActivationKey.Hint"] = "Get your Product activation key from the Novalnet Admin Portal: Projects > Choose your project > Shop Parameters > API Signature (Product activation key)",
            ["Plugins.Payment.NovalnetAPIConfiguration.Fields.PaymentAccessKey"] = "Payment access key",
            ["Plugins.Payment.NovalnetAPIConfiguration.Fields.PaymentAccessKey.Hint"] = "Get your Payment access key from the Novalnet Admin Portal Projects > Choose your project > Shop Parameters > Payment access key",
            ["Plugins.Payment.NovalnetAPIConfiguration.Fields.TariffID"] = "Tariff ID",
            ["Plugins.Payment.NovalnetAPIConfiguration.Fields.TariffID.Hint"] = "Enter a Tariff ID to match the preferred tariff plan you created at the Novalnet Admin Portal for this project",
            ["Plugins.Payment.NovalnetAPIConfiguration.Fields.ClientKey"] = "Client Key",
            ["Plugins.Payment.NovalnetAPIConfiguration.Fields.ClientKey.Hint"] = "The client key is a unique identifier linked to your Novalnet merchant account, required to authenticate client requests, render the secure payment form, and temporarily tokenize sensitive information",
            ["Plugins.Payment.NovalnetAPIConfiguration.Fields.WebhookTestMode"] = "Allow manual testing of the Notification / Webhook URL",
            ["Plugins.Payment.NovalnetAPIConfiguration.Fields.WebhookTestMode.Hint"] = "Enable this to test the Novalnet Notification / Webhook URL manually. Disable this before setting your shop live to block unauthorized calls from external parties",
            ["Plugins.Payment.NovalnetAPIConfiguration.Fields.WebhookEmailTo"] = "Send e-mail to",
            ["Plugins.Payment.NovalnetAPIConfiguration.Fields.WebhookEmailTo.Hint"] = "Notification / Webhook URL execution messages will be sent to this e-mail",
            ["Plugins.Payment.NovalnetAPIConfiguration.Fields.WebhookUrl"] = "Notification / Webhook URL",
            ["Plugins.Payment.NovalnetAPIConfiguration.Fields.WebhookUrl.Hint"] = "You must add the following webhook endpoint to your Novalnet Admin Portal . This will allow you to receive notifications about the transaction status",
            ["Plugins.Payments.NovalnetAPIConfiguration.Testorder"] = "Test order",
            ["Plugins.Payments.NovalnetAPIConfiguration.HashCheckFailed"] = "While redirecting some data has been changed. The hash check failed.",
            ["Plugins.Payments.NovalnetAPIConfiguration.AmountTransfer"] = "Please transfer the amount of {0} to the following account.",
            ["Plugins.Payments.NovalnetAPIConfiguration.AmountTransferDueDate"] = "Please transfer the amount of {0} to the following account on or before {1}.",
            ["Plugins.Payments.NovalnetAPIConfiguration.AgeErrorText"] = "You need to be at least 18 years old.",
            ["Plugins.Payments.NovalnetAPIConfiguration.NovalnetTransactionDetails"] = "Novalnet Transaction Details",
            ["Plugins.Payments.NovalnetAPIConfiguration.NovalnetTID"] = "Novalnet Transaction ID: {0}",
            ["Plugins.Payments.NovalnetAPIConfiguration.PendingTransactionText"] = "Your order is under verification and we will soon update you with the order status. Please note that this may take upto 24 hours.",
            ["Plugins.Payments.NovalnetAPIConfiguration.AccountHolder"] = "Account holder",
            ["Plugins.Payments.NovalnetAPIConfiguration.Bank"] = "BANK: {0}",
            ["Plugins.Payments.NovalnetAPIConfiguration.Amount"] = "Amount: {0}",
            ["Plugins.Payments.NovalnetAPIConfiguration.Date"] = "Date: {0}",
            ["Plugins.Payments.NovalnetAPIConfiguration.Place"] = "Place: {0}",
            ["Plugins.Payments.NovalnetAPIConfiguration.Iban"] = "IBAN",
            ["Plugins.Payments.NovalnetAPIConfiguration.Bic"] = "BIC",
            ["Plugins.Payments.NovalnetAPIConfiguration.InvoicePaymentReferenceDesc"] = "Please use any of the following payment references when transferring the amount. This is necessary to match it with your corresponding order",
            ["Plugins.Payments.NovalnetAPIConfiguration.InvoicePaymentReference"] = "Payment Reference",
            ["Plugins.Payments.NovalnetAPIConfiguration.MultibancoPaymentReferenceDesc"] = "Please use the following payment reference details to pay the amount of {0} at a Multibanco ATM or through your internet banking",
            ["Plugins.Payments.NovalnetAPIConfiguration.MultibancoPaymentReference"] = "Partner Payment Reference: {0}",
            ["Plugins.Payments.NovalnetAPIConfiguration.Entity"] = "Entity: {0}",
            ["Plugins.Payments.NovalnetAPIConfiguration.TestModeDesc"] = "The payment will be processed in the test mode therefore amount for this transaction will not be charged",
            ["Plugins.Payments.NovalnetAPIConfiguration.SepaMandate"] = "I hereby grant the mandate for the SEPA direct debit (electronic transmission) and confirm that the given bank details are correct",
            ["Plugins.Payments.NovalnetAPIConfiguration.SepaMandateDetails"] = "I authorise (A) Novalnet AG to send instructions to my bank to debit my account and (B) my bank to debit my account in accordance with the instructions from Novalnet AG.",
            ["Plugins.Payments.NovalnetAPIConfiguration.SepaCreditor"] = "Creditor identifier: DE53ZZZ00000004253",
            ["Plugins.Payments.NovalnetAPIConfiguration.Note"] = "Note: ",
            ["Plugins.Payments.NovalnetAPIConfiguration.SepaNoteDetails"] = "You are entitled to a refund from your bank under the terms and conditions of your agreement with bank. A refund must be claimed within 8 weeks starting from the date on which your account was debited.",
            ["Plugins.Payments.NovalnetAPIConfiguration.TransactionConfirmed"] = "The transaction has been confirmed on {0}.",
            ["Plugins.Payments.NovalnetAPIConfiguration.TransactionCancelled"] = "The transaction has been canceled on {0}.",
            ["Plugins.Payments.NovalnetAPIConfiguration.ChargebackExecuted"] = "Chargeback executed successfully for the TID: {0} amount: {1} on {2}. The subsequent TID: {3}.",
            ["Plugins.Payments.NovalnetAPIConfiguration.CreditExecuted"] = "Credit has been successfully received for the TID: {0} with amount {1} on {2}. Please refer PAID order details in our Novalnet Admin Portal for the TID: {3}.",
            ["Plugins.Payments.NovalnetAPIConfiguration.TransactionStatusChanged"] = "The transaction status has been changed from pending to on-hold for the TID: {0} on {1}.",
            ["Plugins.Payments.NovalnetAPIConfiguration.TransactionUpdatedWithDueDate"] = "The transaction has been updated with amount {0} and due date {1}.",
            ["Plugins.Payments.NovalnetAPIConfiguration.TransactionUpdated"] = "Transaction updated successfully for the TID: {0} with the amount {1} on {2}",
            ["Plugins.Payments.NovalnetAPIConfiguration.RefundExecuted"] = "Refund has been initiated for the TID: {0} with the amount {1}.",
            ["Plugins.Payments.NovalnetAPIConfiguration.RefundedTid"] = "New TID:{0} for the refunded amount.",
            ["Plugins.Payments.NovalnetAPIConfiguration.PaymentReminderSent"] = "Payment Reminder { 0 } has been sent to the customer.",
            ["Plugins.Payments.NovalnetAPIConfiguration.SubmittedToCollection"] = "The transaction has been submitted to the collection agency. Collection Reference: {0}",
            ["Plugins.Payments.NovalnetAPIConfiguration.creditcardText"] = "Credit/Debit Cards",
            ["Plugins.Payments.NovalnetAPIConfiguration.prepaymentText"] = "Prepayment",
            ["Plugins.Payments.NovalnetAPIConfiguration.invoiceText"] = "Invoice",
            ["Plugins.Payments.NovalnetAPIConfiguration.multibancoText"] = "Multibanco",
            ["Plugins.Payments.NovalnetAPIConfiguration.direct_debit_sepaText"] = "Direct Debit SEPA",
            ["Plugins.Payments.NovalnetAPIConfiguration.direct_debit_achText"] = "Direct Debit ACH",
            ["Plugins.Payments.NovalnetAPIConfiguration.wechatpayText"] = "WeChat Pay",
            ["Plugins.Payments.NovalnetAPIConfiguration.mbwayText"] = "MB Way",
            ["Plugins.Payments.NovalnetAPIConfiguration.payconiqText"] = "Payconiq",
            ["Plugins.Payments.NovalnetAPIConfiguration.kakaopayText"] = "KakaoPay",
            ["Plugins.Payments.NovalnetAPIConfiguration.alipayText"] = "AliPay",
            ["Plugins.Payments.NovalnetAPIConfiguration.naverpayText"] = "Naver Pay",
            ["Plugins.Payments.NovalnetAPIConfiguration.twintText"] = "TWINT",
            ["Plugins.Payments.NovalnetAPIConfiguration.bancontactText"] = "Bancontact",
            ["Plugins.Payments.NovalnetAPIConfiguration.idealText"] = "iDEAL",
            ["Plugins.Payments.NovalnetAPIConfiguration.epsText"] = "eps Online Transfer",
            ["Plugins.Payments.NovalnetAPIConfiguration.przelewy24Text"] = "Przelewy24",
            ["Plugins.Payments.NovalnetAPIConfiguration.online_bank_transferText"] = "Online bank transfer",
            ["Plugins.Payments.NovalnetAPIConfiguration.PostfinanceText"] = "PostFinance E-Finance",
            ["Plugins.Payments.NovalnetAPIConfiguration.postfinance_cardText"] = "PostFinance Card",
            ["Plugins.Payments.NovalnetAPIConfiguration.trustlyText"] = "Trustly",
            ["Plugins.Payments.NovalnetAPIConfiguration.blikText"] = "Blik",
            ["Plugins.Payments.NovalnetAPIConfiguration.paypalText"] = "PayPal",
            ["Plugins.Payments.NovalnetAPIConfiguration.guaranteed_invoiceText"] = "Invoice with payment guarantee",
            ["Plugins.Payments.NovalnetAPIConfiguration.guaranteed_direct_debit_sepaText"] = "Direct debit SEPA with payment guarantee",
            ["Plugins.Payments.NovalnetAPIConfiguration.guaranteed_payment_requirement"] = "Basic requirements for payment guarantee",
            ["Plugins.Payments.NovalnetAPIConfiguration.allowed_b2c_countries_info"] = "Allowed B2C countries: DE, AT, CH",
            ["Plugins.Payments.NovalnetAPIConfiguration.allowed_b2b_countries_info"] = "Allowed B2B countries: European union and Switzerland",
            ["Plugins.Payments.NovalnetAPIConfiguration.guaranteed_mimimum_order_amount_info"] = "Minimum order amount: 9,99 EUR or more",
            ["Plugins.Payments.NovalnetAPIConfiguration.guaranteed_age_limit_info"] = "Age limit: 18 years or more",
            ["Plugins.Payments.NovalnetAPIConfiguration.guaranteed_billing_address_info"] = "The billing address must be the same as the shipping address",
            ["Plugins.Payments.NovalnetAPIConfiguration.AccountNumber"] = "Account Number",
            ["Plugins.Payments.NovalnetAPIConfiguration.RoutingNumber"] = "Routing Number (ABA)",
            ["Plugins.Payments.NovalnetAPIConfiguration.DobFieldText"] = "Your date of birth",
            ["Plugins.Payments.NovalnetAPIConfiguration.DobPlaceholder"] = "DD.MM.YYYY",
            ["Plugins.Payments.NovalnetAPIConfiguration.Authorize"] = "Authorize",
            ["Plugins.Payments.NovalnetAPIConfiguration.Capture"] = "Capture",
            ["Plugins.Payments.NovalnetAPIConfiguration.ValueIsRequired"] = "Value is required",
            ["Plugins.Payments.NovalnetAPIConfiguration.FillTheRequiredFields"] = "Please enter values in the required fields",
            ["Plugins.Payments.NovalnetAPIConfiguration.AgeErrorText"] = "You need to be at least 18 years old",
            ["Plugins.Payments.NovalnetAPIConfiguration.DateFormatErrorText"] = "The date format is invalid",
        });

        var germanLanguage = allLanguages.FirstOrDefault(lang => lang.UniqueSeoCode.Equals("de", StringComparison.InvariantCultureIgnoreCase));
        if (germanLanguage != null)
        {
            await _localizationService.SaveLocalizedFriendlyNameAsync(this, germanLanguage.Id, "Novalnet API-Konfiguration");
            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Payment.NovalnetAPIConfiguration.Fields.ProductActivationKey"] = "Produktaktivierungsschlüssel",
                ["Plugins.Payment.NovalnetAPIConfiguration.Fields.ProductActivationKey.Hint"] = "Ihren Produktaktivierungsschlüssel finden Sie im Novalnet Admin-Portal: Projekte > Wählen Sie Ihr Projekt > API-Anmeldeinformationen > API Signature (Aktivierungsschlüssel des Produkts)",
                ["Plugins.Payment.NovalnetAPIConfiguration.Fields.PaymentAccessKey"] = "Zahlungs-Zugriffsschlüssel",
                ["Plugins.Payment.NovalnetAPIConfiguration.Fields.PaymentAccessKey.Hint"] = "Ihren Paymentzugriffsschlüssel finden Sie im Novalnet Admin-Portal: Projekte > Wählen Sie Ihr Projekt > API-Anmeldeinformationen > Paymentzugriffsschlüssel",
                ["Plugins.Payment.NovalnetAPIConfiguration.Fields.TariffID"] = "Tarif-ID",
                ["Plugins.Payment.NovalnetAPIConfiguration.Fields.TariffID.Hint"] = "Wählen Sie eine Tarif-ID, die dem bevorzugten Tarifplan entspricht, den Sie im Novalnet Admin-Portal für dieses Projekt erstellt haben",
                ["Plugins.Payment.NovalnetAPIConfiguration.Fields.ClientKey"] = "Schlüsselkunde",
                ["Plugins.Payment.NovalnetAPIConfiguration.Fields.ClientKey.Hint"] = "Der Client-Key ist eine eindeutige Kennung, die mit Ihrem Novalnet-Händlerkonto verknüpft ist. Er wird benötigt, um Client-Anfragen zu authentifizieren, das sichere Zahlungsformular darzustellen und sensible Informationen vorübergehend zu tokenisieren",
                ["Plugins.Payment.NovalnetAPIConfiguration.Fields.WebhookTestMode"] = "Manuelles Testen der Benachrichtigungs- / Webhook-URL erlauben",
                ["Plugins.Payment.NovalnetAPIConfiguration.Fields.WebhookTestMode.Hint"] = "Aktivieren Sie diese Option, um die Novalnet-Benachrichtigungs-/Webhook-URL manuell zu testen. Deaktivieren Sie die Option, bevor Sie Ihren Shop liveschalten, um unautorisierte Zugriffe von Dritten zu blockieren",
                ["Plugins.Payment.NovalnetAPIConfiguration.Fields.WebhookEmailTo"] = "E-Mails senden an",
                ["Plugins.Payment.NovalnetAPIConfiguration.Fields.WebhookEmailTo.Hint"] = "E-Mail-Benachrichtigungen werden an diese E-Mail-Adresse gesendet",
                ["Plugins.Payment.NovalnetAPIConfiguration.Fields.WebhookUrl"] = "Benachrichtigungs- / Webhook-URL",
                ["Plugins.Payment.NovalnetAPIConfiguration.Fields.WebhookUrl.Hint"] = "Sie müssen die folgende Webhook-URL im Novalnet Admin-Portal hinzufügen. Dadurch können Sie Benachrichtigungen über den Transaktionsstatus erhalten",
                ["Plugins.Payments.NovalnetAPIConfiguration.Testorder"] = "Testbestellung",
                ["Plugins.Payments.NovalnetAPIConfiguration.HashCheckFailed"] = "Während der Umleitung wurden einige Daten geändert. Die Hash-Prüfung ist fehlgeschlagen",
                ["Plugins.Payments.NovalnetAPIConfiguration.AmountTransfer"] = "Bitte überweisen Sie den Betrag {0} auf das unten stehende Konto.",
                ["Plugins.Payments.NovalnetAPIConfiguration.AmountTransferDueDate"] = "Bitte überweisen Sie den Betrag von {0} spätestens bis zum {1} auf das folgende Konto",
                ["Plugins.Payments.NovalnetAPIConfiguration.AgeErrorText"] = "Sie müssen mindestens 18 Jahre alt sein.",
                ["Plugins.Payments.NovalnetAPIConfiguration.NovalnetTransactionDetails"] = "Novalnet-Transaktionsdetails",
                ["Plugins.Payments.NovalnetAPIConfiguration.NovalnetTID"] = "Novalnet-Transaktions-ID: {0}",
                ["Plugins.Payments.NovalnetAPIConfiguration.PendingTransactionText"] = "Ihre Bestellung wird derzeit überprüft. Wir werden Sie in Kürze über den Bestellstatus informieren. Bitte beachten Sie, dass dies bis zu 24 Stunden dauern kann.",
                ["Plugins.Payments.NovalnetAPIConfiguration.AccountHolder"] = "Kontoinhaber",
                ["Plugins.Payments.NovalnetAPIConfiguration.Bank"] = "BANK: {0}",
                ["Plugins.Payments.NovalnetAPIConfiguration.Amount"] = "Betrag: {0}",
                ["Plugins.Payments.NovalnetAPIConfiguration.Date"] = "Datum: {0}",
                ["Plugins.Payments.NovalnetAPIConfiguration.Place"] = "Ort: {0}",
                ["Plugins.Payments.NovalnetAPIConfiguration.Iban"] = "IBAN",
                ["Plugins.Payments.NovalnetAPIConfiguration.Bic"] = "BIC",
                ["Plugins.Payments.NovalnetAPIConfiguration.InvoicePaymentReferenceDesc"] = "Bitte verwenden Sie einen der unten angegebenen Verwendungszwecke für die Überweisung. Nur so kann Ihr Geldeingang Ihrer Bestellung zugeordnet werden",
                ["Plugins.Payments.NovalnetAPIConfiguration.InvoicePaymentReference"] = "Verwendungszweck",
                ["Plugins.Payments.NovalnetAPIConfiguration.MultibancoPaymentReferenceDesc"] = "Bitte verwenden Sie die folgende Zahlungsreferenz, um den Betrag von {0} an einem Multibanco-Geldautomaten oder über Ihr Onlinebanking zu bezahlen",
                ["Plugins.Payments.NovalnetAPIConfiguration.MultibancoPaymentReference"] = "Partner-Zahlungsreferenz: {0}",
                ["Plugins.Payments.NovalnetAPIConfiguration.Entity"] = "Entität: {0}",
                ["Plugins.Payments.NovalnetAPIConfiguration.TestModeDesc"] = "Die Zahlung wird im Testmodus durchgeführt, daher wird der Betrag für diese Transaktion nicht eingezogen",
                ["Plugins.Payments.NovalnetAPIConfiguration.SepaMandate"] = "Ich erteile hiermit das SEPA-Lastschriftmandat (elektronische Übermittlung) und bestätige, dass die Bankverbindung korrekt ist",
                ["Plugins.Payments.NovalnetAPIConfiguration.SepaMandateDetails"] = "Ich ermächtige den Zahlungsempfänger, Zahlungen von meinem Konto mittels Lastschrift einzuziehen. Zugleich weise ich mein Kreditinstitut an, die von dem Zahlungsempfänger auf mein Konto gezogenen Lastschriften einzulösen.",
                ["Plugins.Payments.NovalnetAPIConfiguration.SepaCreditor"] = "Gläubiger-Identifikationsnummer: DE53ZZZ00000004253",
                ["Plugins.Payments.NovalnetAPIConfiguration.Note"] = "Note: ",
                ["Plugins.Payments.NovalnetAPIConfiguration.SepaNoteDetails"] = "Ich kann innerhalb von acht Wochen, beginnend mit dem Belastungsdatum, die Erstattung des belasteten Betrages verlangen. Es gelten dabei die mit meinem Kreditinstitut vereinbarten Bedingungen.",
                ["Plugins.Payments.NovalnetAPIConfiguration.TransactionConfirmed"] = "Die Buchung wurde am {0} Uhr bestätigt.",
                ["Plugins.Payments.NovalnetAPIConfiguration.TransactionCancelled"] = "Die Transaktion wurde am {0} Uhr storniert",
                ["Plugins.Payments.NovalnetAPIConfiguration.ChargebackExecuted"] = "Chargeback erfolgreich importiert für die TID: {0} Betrag: {1} am {2} Uhr. TID der Folgebuchung: {3}",
                ["Plugins.Payments.NovalnetAPIConfiguration.CreditExecuted"] = "Die Gutschrift für die TID ist erfolgreich eingegangen: {0} mit Betrag {1} am {2} . Bitte entnehmen Sie die TID den Einzelheiten der Bestellung bei BEZAHLT in unserem Novalnet Admin-Portal: {3}.",
                ["Plugins.Payments.NovalnetAPIConfiguration.TransactionStatusChanged"] = "Der Status der Transaktion mit der TID: {0} wurde am {1} Uhr von ausstehend auf ausgesetzt geändert",
                ["Plugins.Payments.NovalnetAPIConfiguration.TransactionUpdatedWithDueDate"] = "Die Transaktion wurde mit dem Betrag {0} und dem Fälligkeitsdatum aktualisiert {1}",
                ["Plugins.Payments.NovalnetAPIConfiguration.TransactionUpdated"] = "Transaktion mit TID {0} und Betrag {1} wurde am {2} erfolgreich aktualisiert",
                ["Plugins.Payments.NovalnetAPIConfiguration.RefundExecuted"] = "Die Rückerstattung für die TID {0} mit dem Betrag {1} wurde veranlasst",
                ["Plugins.Payments.NovalnetAPIConfiguration.RefundedTid"] = "Die neue TID für den erstatteten Betrag lautet: {0}",
                ["Plugins.Payments.NovalnetAPIConfiguration.PaymentReminderSent"] = "Die Zahlungserinnerung {0} wurde an den Kunden gesendet.",
                ["Plugins.Payments.NovalnetAPIConfiguration.SubmittedToCollection"] = "Die Transaktion wurde an das Inkassobüro übermittelt. Inkassoreferenz: {0}.",
                ["Plugins.Payments.NovalnetAPIConfiguration.creditcardText"] = "Kredit- / Debitkarte",
                ["Plugins.Payments.NovalnetAPIConfiguration.prepaymentText"] = "Vorkasse",
                ["Plugins.Payments.NovalnetAPIConfiguration.invoiceText"] = "Kauf auf Rechnung",
                ["Plugins.Payments.NovalnetAPIConfiguration.multibancoText"] = "Multibanco",
                ["Plugins.Payments.NovalnetAPIConfiguration.direct_debit_sepaText"] = "SEPA-Lastschrift",
                ["Plugins.Payments.NovalnetAPIConfiguration.direct_debit_achText"] = "Lastschrift ACH",
                ["Plugins.Payments.NovalnetAPIConfiguration.wechatpayText"] = "WeChat Pay",
                ["Plugins.Payments.NovalnetAPIConfiguration.mbwayText"] = "MB Way",
                ["Plugins.Payments.NovalnetAPIConfiguration.payconiqText"] = "Payconiq",
                ["Plugins.Payments.NovalnetAPIConfiguration.kakaopayText"] = "KakaoPay",
                ["Plugins.Payments.NovalnetAPIConfiguration.naverpayText"] = "Naver Pay",
                ["Plugins.Payments.NovalnetAPIConfiguration.twintText"] = "TWINT",
                ["Plugins.Payments.NovalnetAPIConfiguration.bancontactText"] = "Bancontact",
                ["Plugins.Payments.NovalnetAPIConfiguration.idealText"] = "iDEAL",
                ["Plugins.Payments.NovalnetAPIConfiguration.epsText"] = "eps Online Transfer",
                ["Plugins.Payments.NovalnetAPIConfiguration.przelewy24Text"] = "Przelewy24",
                ["Plugins.Payments.NovalnetAPIConfiguration.online_bank_transferText"] = "Onlineüberweisung",
                ["Plugins.Payments.NovalnetAPIConfiguration.postfinanceText"] = "PostFinance E-Finance",
                ["Plugins.Payments.NovalnetAPIConfiguration.postfinance_cardText"] = "PostFinance Card",
                ["Plugins.Payments.NovalnetAPIConfiguration.trustlyText"] = "Trustly",
                ["Plugins.Payments.NovalnetAPIConfiguration.blikText"] = "Blik",
                ["Plugins.Payments.NovalnetAPIConfiguration.paypalText"] = "PayPal",
                ["Plugins.Payments.NovalnetAPIConfiguration.guaranteed_invoiceText"] = "Rechnung mit Zahlungsgarantie",
                ["Plugins.Payments.NovalnetAPIConfiguration.guaranteed_direct_debit_sepaText"] = "Lastschrift SEPA mit Zahlungsgarantie",
                ["Plugins.Payments.NovalnetAPIConfiguration.guaranteed_payment_requirement"] = "Grundanforderungen für die Zahlungsgarantie",
                ["Plugins.Payments.NovalnetAPIConfiguration.allowed_b2c_countries_info"] = "Erlaubte B2C-Länder: DE, AT, CH",
                ["Plugins.Payments.NovalnetAPIConfiguration.allowed_b2b_countries_info"] = "Für B2B (Geschäftskunden) zugelassene Länder: Europäische Union und die Schweiz.",
                ["Plugins.Payments.NovalnetAPIConfiguration.guaranteed_mimimum_order_amount_info"] = "Mindestbetrag der Bestellung: 9,99 EUR",
                ["Plugins.Payments.NovalnetAPIConfiguration.guaranteed_age_limit_info"] = "Mindestalter: 18 Jahre",
                ["Plugins.Payments.NovalnetAPIConfiguration.guaranteed_billing_address_info"] = "Rechnungsadresse und Lieferadresse müssen übereinstimmen",
                ["Plugins.Payments.NovalnetAPIConfiguration.AccountNumber"] = "Kontonummer",
                ["Plugins.Payments.NovalnetAPIConfiguration.RoutingNumber"] = "Routing-Nummer (ABA)",
                ["Plugins.Payments.NovalnetAPIConfiguration.DobFieldText"] = "Ihr Geburtsdatum",
                ["Plugins.Payments.NovalnetAPIConfiguration.DobPlaceholder"] = "TT.MM.JJJJ",
                ["Plugins.Payments.NovalnetAPIConfiguration.Authorize"] = "Zahlung autorisieren",
                ["Plugins.Payments.NovalnetAPIConfiguration.Capture"] = "Zahlung einziehen",
                ["Plugins.Payments.NovalnetAPIConfiguration.ValueIsRequired"] = "Wert ist erforderlich",
                ["Plugins.Payments.NovalnetAPIConfiguration.FillTheRequiredFields"] = "Bitte geben Sie Werte in die erforderlichen Felder ein",
                ["Plugins.Payments.NovalnetAPIConfiguration.AgeErrorText"] = "Sie müssen mindestens 18 Jahre alt sein.",
                ["Plugins.Payments.NovalnetAPIConfiguration.DateFormatErrorText"] = "Ungültiges Datumsformat",
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
        await _settingService.DeleteSettingAsync<NovalnetAPIConfigurationPaymentSettings>();

        //locales
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.NovalnetAPIConfiguration");

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
    public bool SupportCapture => false;

    /// <summary>
    /// Gets a value indicating whether partial refund is supported
    /// </summary>
    public bool SupportPartiallyRefund => false;

    /// <summary>
    /// Gets a value indicating whether refund is supported
    /// </summary>
    public bool SupportRefund => false;

    /// <summary>
    /// Gets a value indicating whether void is supported
    /// </summary>
    public bool SupportVoid => false;

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

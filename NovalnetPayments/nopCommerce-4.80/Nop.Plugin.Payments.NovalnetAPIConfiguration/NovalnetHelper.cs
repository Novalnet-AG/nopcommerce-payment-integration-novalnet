using System.Net;
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
using System.Globalization;
using Nop.Services.Catalog;
using Nop.Core.Domain.Directory;

namespace Nop.Plugin.Payments.NovalnetAPIConfiguration;

public class NovalnetHelper
{
    #region Fields

    public static string novalnetEndpoint = "https://payport.novalnet.de";

    #endregion

    #region Ctor

    public static async Task<NovalnetPayload> GetPaymentRequestParams(Object paymentRequest, NovalnetAPIConfigurationPaymentSettings _novalnetAPIConfigurationPaymentSettings, ICustomerService _customerService, IAddressService _addressService, IWorkContext _workContext, IStateProvinceService _stateProvinceService, ICurrencyService _currencyService, ICountryService _countryService, IWebHelper _webHelper, IHttpContextAccessor _httpContextAccessor)
    {
        string paymentAccessKey = _novalnetAPIConfigurationPaymentSettings.PaymentAccessKey;
        string encodedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(paymentAccessKey));

        int CustomerId = 0;
        decimal amount = 0;
        string paymentMethodSystemName = "";
        string orderNumber = "";
        string orderGuid = "";

        if (paymentRequest is ProcessPaymentRequest processRequest)
        {
            CustomerId = processRequest.CustomerId;
            amount = processRequest.OrderTotal;
            paymentMethodSystemName = processRequest.PaymentMethodSystemName;
            orderGuid = processRequest.OrderGuid.ToString();
        }
        else if (paymentRequest is PostProcessPaymentRequest postProcessRequest)
        {
            CustomerId = postProcessRequest.Order.CustomerId;
            amount = postProcessRequest.Order.OrderTotal;
            paymentMethodSystemName = postProcessRequest.Order.PaymentMethodSystemName;
            orderNumber = postProcessRequest.Order.CustomOrderNumber;
            orderGuid = postProcessRequest.Order.OrderGuid.ToString();
        }

        string nnPaymentType = GetNovalnetPaymentType(paymentMethodSystemName);

        var customer = await _customerService.GetCustomerByIdAsync(CustomerId);

        Address shippingAddress = null;
        if (customer.ShippingAddressId.HasValue)
            shippingAddress = await _addressService.GetAddressByIdAsync(customer.ShippingAddressId.Value);

        var billingAddress = await _addressService.GetAddressByIdAsync(customer.BillingAddressId.Value);

        //get currency
        var currency = await _workContext.GetWorkingCurrencyAsync();

        int orderAmountInCents = await GetConvertedAmountCents(amount, _currencyService, currency);

        CustomerInfo customerInfo = await NovalnetHelper.GetCustomerInfo(billingAddress, shippingAddress, _countryService, _stateProvinceService);
        customerInfo.customer_no = CustomerId.ToString();
        customerInfo.customer_ip = _webHelper.GetCurrentIpAddress();

        string shopVersion = NopVersion.FULL_VERSION;

        MerchantInfo merchant = new MerchantInfo
        {
            signature = _novalnetAPIConfigurationPaymentSettings.ProductActivationKey,
            tariff = _novalnetAPIConfigurationPaymentSettings.TariffID
        };

        TransactionInfo transaction = new TransactionInfo
        {
            test_mode = 1,
            payment_type = nnPaymentType,
            amount = orderAmountInCents,
            currency = currency.CurrencyCode,
            system_name = "nopCommerce",
            system_version = $"{shopVersion}-NN1.0.0",
            system_ip = GetServerIpAddress(_httpContextAccessor),
            system_url = _webHelper.GetStoreLocation()
        };

        if (!string.IsNullOrEmpty(orderNumber))
        {
            transaction.order_no = orderNumber;
        }

        CustomInfo custom = new CustomInfo
        {
            lang = await GetLangCodeAsync(_workContext),
            input1 = "order_guid",
            inputval1 = orderGuid
        };

        NovalnetPayload nnPayload = new NovalnetPayload
        {
            merchant = merchant,
            customer = customerInfo,
            transaction = transaction,
            custom = custom
        };

        return nnPayload;
    }

    public static async Task<CustomerInfo> GetCustomerInfo(Address billingAddress, Address shippingAddress, ICountryService _countryService, IStateProvinceService _stateProvinceService)
    {
        var billingCountry = await _countryService.GetCountryByAddressAsync(billingAddress);
        var billingState = (await _stateProvinceService.GetStateProvinceByAddressAsync(billingAddress))?.Name;

        CustomerInfo customerInfo = new CustomerInfo
        {
            first_name = billingAddress.FirstName,
            last_name = billingAddress.LastName,
            email = billingAddress.Email,
        };

        BillingInfo billing = new BillingInfo
        {
            street = billingAddress.Address1,
            city = billingAddress.City,
            zip = billingAddress.ZipPostalCode,
            country_code = billingCountry.TwoLetterIsoCode
        };

        if (!string.IsNullOrEmpty(billingState))
        {
            billing.state = billingState;
        }

        if (!string.IsNullOrEmpty(billingAddress.Address2))
        {
            billing.street += " " + billingAddress.Address2;
        }

        if (!string.IsNullOrEmpty(billingAddress.Company))
        {
            billing.company += " " + billingAddress.Company;
        }
        
        customerInfo.billing = billing;

        ShippingInfo shipping = new ShippingInfo();

        if (shippingAddress == null)
        {
            shipping.same_as_billing = 1;
        }
        else
        {
            shipping.street = shippingAddress.Address1;
            shipping.city = shippingAddress.City;
            shipping.zip = shippingAddress.ZipPostalCode;

            var shippingCountry = await _countryService.GetCountryByAddressAsync(shippingAddress);
            shipping.country_code = shippingCountry.TwoLetterIsoCode;

            var shippingState = (await _stateProvinceService.GetStateProvinceByAddressAsync(shippingAddress))?.Name;
            if (!string.IsNullOrEmpty(shippingState))
            {
                shipping.state = shippingState;
            }

            if (!string.IsNullOrEmpty(shippingAddress.Address2))
            {
                shipping.street += " " + shippingAddress.Address2;
            }

            if (!string.IsNullOrEmpty(shippingAddress.Company))
            {
                shipping.company += " " + shippingAddress.Company;
            }

            string billingJson = SerializeToJson(billing);

            string shippingJson = SerializeToJson(shipping);

            if (billingJson == shippingJson)
            {
                shipping = new ShippingInfo
                {
                    same_as_billing = 1
                };
            }
        }

        customerInfo.shipping = shipping;
            
        return customerInfo;
    }

    public static void GetRedirectPaymentParams(NovalnetPayload nnPayload, string storeLocation)
    {
        nnPayload.transaction.return_url = $"{storeLocation}NovalnetPayment/RedirectResponseHandler";
        nnPayload.transaction.error_return_url = $"{storeLocation}NovalnetPayment/RedirectResponseHandler";
    }

    public static string GetNovalnetEndPoint(PaymentAction paymentAction, int OnholdMinAmount, int orderAmount)
    {
        if (paymentAction == PaymentAction.Authorize)
        {
            if (orderAmount >= OnholdMinAmount)
            {
                return "/v2/authorize";
            }
        }
        return "/v2/payment";
    }

    public static string GetNovalnetPaymentType(string paymentMethodSystemName)
    {
        var paymentTypes = new Dictionary<string, string>
        {
            { "Payments.NovalnetInvoice", "INVOICE" },
            { "Payments.NovalnetPrepayment", "PREPAYMENT" },
            { "Payments.NovalnetCreditcard", "CREDITCARD" },
            { "Payments.NovalnetSepa", "DIRECT_DEBIT_SEPA" },
            { "Payments.NovalnetGuaranteedSepa", "GUARANTEED_DIRECT_DEBIT_SEPA" },
            { "Payments.NovalnetGuaranteedInvoice", "GUARANTEED_INVOICE" },
            { "Payments.NovalnetIdeal", "IDEAL" },
            { "Payments.NovalnetOnlineBankTransfer", "ONLINE_BANK_TRANSFER" },
            { "Payments.NovalnetEps", "EPS" },
            { "Payments.NovalnetPaypal", "PAYPAL" },
            { "Payments.NovalnetBancontact", "BANCONTACT" },
            { "Payments.NovalnetMultibanco", "MULTIBANCO" },
            { "Payments.NovalnetPrzelewy", "PRZELEWY24" },
            { "Payments.NovalnetTwint", "TWINT" },
            { "Payments.NovalnetPostfinance", "POSTFINANCE" },
            { "Payments.NovalnetPostfinancecard", "POSTFINANCE_CARD" },
            { "Payments.NovalnetAlipay", "ALIPAY" },
            { "Payments.NovalnetBlik", "BLIK" },
            { "Payments.NovalnetDirectDebitACH", "DIRECT_DEBIT_ACH" },
            { "Payments.NovalnetKakaoPay", "KAKAOPAY" },
            { "Payments.NovalnetMbWay", "MBWAY" },
            { "Payments.NovalnetNaverPay", "NAVERPAY" },
            { "Payments.NovalnetPayconiq", "PAYCONIQ" },
            { "Payments.NovalnetTrustly", "TRUSTLY" },
            { "Payments.NovalnetWechatPay", "WECHATPAY" },
        };

        if (paymentTypes.TryGetValue(paymentMethodSystemName.ToString(), out var value))
        {
            return value;
        }
        return "";
    }

    public static string GenerateSha256Hash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }

    public static Task<ProcessPaymentResult> SetDirectPaymentResult(JObject response, ProcessPaymentResult processPaymentResult)
    {
        if (response != null && response.HasValues)
        {
            string status = response["result"]?["status"].ToString();
            if (status == "SUCCESS")
            {
                string transactionStatus = response["transaction"]?["status"].ToString();
                string[] failureStatus = ["FAILURE", "DEACTIVATED"];

                if (!failureStatus.Contains(transactionStatus))
                {
                    if (transactionStatus == "ON_HOLD")
                    {
                        processPaymentResult.NewPaymentStatus = PaymentStatus.Authorized;
                    }
                    else
                    {
                        processPaymentResult.NewPaymentStatus = PaymentStatus.Pending;
                    }
                }
                processPaymentResult.AuthorizationTransactionId = response["transaction"]["tid"].ToString();
            }
            else
            {
                string statusText = response["result"]?["status_text"].ToString();
                processPaymentResult.AddError(statusText);
            }
        }
        else
        {
            processPaymentResult.AddError("Not supported transaction type");
        }
        return Task.FromResult(processPaymentResult);
    }

    public static async Task<JObject> GetNovalnetTransactionDetails(string tid, string paymentAccessKey, IWorkContext _workContext)
    {
        var nnPayload = new
        {
            transaction = new
            {
                tid = tid
            },
            custom = new
            {
                lang = await GetLangCodeAsync(_workContext),
            }
        };

        string requestJson = SerializeToJson(nnPayload);

        var response = await SendRequestAsync("/v2/transaction/details", requestJson, paymentAccessKey);

        return response;
    }


    public static async Task UpdateOrderNumber(string orderNumber, string tid, string paymentAccessKey)
    {
        string endpoint = "/v2/transaction/update";

        var nnPayload = new
        {
            transaction = new
            {
                tid = tid,
                order_no = orderNumber
            },
        };

        string requestJson = SerializeToJson(nnPayload);

        await SendRequestAsync(endpoint, requestJson, paymentAccessKey);
    }

    public static async Task SetOrderPaymentStatus(Order order, IOrderService _orderService, string transactionStatus, string paymentType, IOrderProcessingService _orderProcessingService)
    {
        string[] failureStatus = ["FAILURE", "DEACTIVATED"];

        if (!failureStatus.Contains(transactionStatus))
        {
            string[] pendingPayments = ["INVOICE", "PREPAYMENT", "MULTIBANCO"];
            if (transactionStatus == "ON_HOLD")
            {
                if (_orderProcessingService.CanMarkOrderAsAuthorized(order))
                    await _orderProcessingService.MarkAsAuthorizedAsync(order);
            }
            else if (transactionStatus == "PENDING" || (transactionStatus == "CONFIRMED" && pendingPayments.Contains(paymentType)))
            {
                order.PaymentStatus = PaymentStatus.Pending;
                await _orderService.UpdateOrderAsync(order);
            }
            else if(transactionStatus == "CONFIRMED" && _orderProcessingService.CanMarkOrderAsPaid(order))
            {
                await _orderProcessingService.MarkOrderAsPaidAsync(order);
            }
        }
        else
        {
            order.PaymentStatus = PaymentStatus.Voided;
            await _orderService.UpdateOrderAsync(order);

            if (_orderProcessingService.CanCancelOrder(order))
               await _orderProcessingService.CancelOrderAsync(order, true);
        }
    }

    public static async Task SetOrderPaymentStatus(Order order, IOrderService _orderService, string transactionStatus, string paymentType)
    {
        string[] failureStatus = ["FAILURE", "DEACTIVATED"];

        if (!failureStatus.Contains(transactionStatus))
        {
            string[] pendingPayments = ["INVOICE", "PREPAYMENT", "MULTIBANCO"];
            if (transactionStatus == "ON_HOLD")
            {
                order.PaymentStatus = PaymentStatus.Authorized;
            }
            else if (transactionStatus == "CONFIRMED" && !pendingPayments.Contains(paymentType))
            {
                order.PaymentStatus = PaymentStatus.Paid;
            }
        }
        else
        {
            order.PaymentStatus = PaymentStatus.Voided;
        }

        await _orderService.UpdateOrderAsync(order);
    }

    public static async Task<StringBuilder> GetTransactionNotes(JObject nnTransactionDetails, ILocalizationService _localizationService)
    {
        StringBuilder orderNoteString = new StringBuilder();
        if (nnTransactionDetails != null && nnTransactionDetails.HasValues)
        {
            orderNoteString.AppendLine(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.NovalnetTransactionDetails"));

            orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.NovalnetTID"), nnTransactionDetails["transaction"]["tid"]));

            string status = nnTransactionDetails["result"]?["status"].ToString();
            if (status == "SUCCESS")
            {
                if (nnTransactionDetails["transaction"]?["test_mode"]?.ToString() == "1")
                {
                    orderNoteString.AppendLine(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.Testorder"));
                }
            }
            else
            {
                orderNoteString.AppendLine(nnTransactionDetails["result"]?["status_text"].ToString());
            }

            await NovalnetHelper.GetPaymentNotes(nnTransactionDetails, orderNoteString, _localizationService);
        }

        return orderNoteString;
    }

    public static async Task InsertTransactionNotes(StringBuilder orderNoteString, int orderNumber, IOrderService _orderService)
    {
        await _orderService.InsertOrderNoteAsync(new OrderNote
        {
            OrderId = orderNumber,
            Note = orderNoteString.ToString(),
            DisplayToCustomer = true,
            CreatedOnUtc = DateTime.UtcNow
        });
    }

    public static bool CheckNovalnetAPICredentials(NovalnetAPIConfigurationPaymentSettings _novalnetAPIConfigurationPaymentSettings)
    {
        if (string.IsNullOrWhiteSpace(_novalnetAPIConfigurationPaymentSettings.ProductActivationKey) || string.IsNullOrWhiteSpace(_novalnetAPIConfigurationPaymentSettings.PaymentAccessKey) || string.IsNullOrWhiteSpace(_novalnetAPIConfigurationPaymentSettings.TariffID))
        {
            return false;
        }
        return true;
    }
    public static string FormatAmount(int cents, string currencyCode)
    {
        decimal amount = cents / 100m;

        var culture = NovalnetHelper.GetCultureByCurrency(currencyCode);

        string formattedAmount = string.Format(culture, "{0:C}", amount);

        return formattedAmount;
    }

    public static CultureInfo GetCultureByCurrency(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
            return CultureInfo.InvariantCulture;

        currencyCode = currencyCode.ToUpperInvariant();

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                if (region.ISOCurrencySymbol == currencyCode)
                    return culture;
            }
            catch
            {
                continue;
            }
        }

        // Fallback
        return CultureInfo.InvariantCulture;
    }


    public static string GetCurrentDate()
    {
        string formattedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        return formattedDate;
    }

    public static async Task GetPaymentNotes(JObject nnTransactionDetails, StringBuilder orderNoteString, ILocalizationService _localizationService)
    {
        string paymentType = nnTransactionDetails["transaction"]["payment_type"].ToString();
        int amount = nnTransactionDetails["transaction"]["amount"].Value<int>();
        string FormattedAmount = NovalnetHelper.FormatAmount(amount, nnTransactionDetails["transaction"]["currency"].ToString());

        string transactionStatus = nnTransactionDetails["transaction"]["status"].ToString();

        string[] guaranteedPayments = ["GUARANTEED_INVOICE", "GUARANTEED_DIRECT_DEBIT_SEPA"];
        if (guaranteedPayments.Contains(paymentType) && transactionStatus == "PENDING")
        {
            orderNoteString.AppendLine(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.PendingTransactionText"));
        }
        else if (nnTransactionDetails["transaction"]["bank_details"] != null)
        {
            string dueDate = nnTransactionDetails["transaction"]["due_date"].ToString();

            string dueDateComments = "Plugins.Payments.NovalnetAPIConfiguration.AmountTransfer";
            if (transactionStatus != "ON_HOLD" && !string.IsNullOrWhiteSpace(dueDate))
            {
                dueDateComments = "Plugins.Payments.NovalnetAPIConfiguration.AmountTransferDueDate";
            }
            dueDateComments = string.Format(await _localizationService.GetResourceAsync(dueDateComments), FormattedAmount, dueDate);
            orderNoteString.AppendLine(dueDateComments);

            var bankDetails = nnTransactionDetails["transaction"]["bank_details"];

            orderNoteString.AppendLine(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.AccountHolder") + ": " + bankDetails["account_holder"].ToString());

            orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.Bank"), bankDetails["bank_name"]?.ToString()));

            orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.Place"), bankDetails["bank_place"]?.ToString()));

            orderNoteString.AppendLine(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.Iban") + ": " + bankDetails["iban"]?.ToString());

            orderNoteString.AppendLine(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.Bic") + ": " + bankDetails["bic"]?.ToString());

            orderNoteString.AppendLine(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.InvoicePaymentReferenceDesc"));

            orderNoteString.AppendLine(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.InvoicePaymentReference") + " 1: " + nnTransactionDetails["transaction"]["tid"]);

            if (!string.IsNullOrWhiteSpace(nnTransactionDetails["transaction"]["invoice_ref"].ToString()))
            {
                orderNoteString.AppendLine(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.InvoicePaymentReference") + " 2: " + nnTransactionDetails["transaction"]["invoice_ref"]);
            }
        }

        if (paymentType == "MULTIBANCO")
        {
            orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.MultibancoPaymentReferenceDesc"), FormattedAmount));

            orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.MultibancoPaymentReference"), nnTransactionDetails["transaction"]?["partner_payment_reference"]?.ToString()));

            orderNoteString.AppendLine(string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.Entity"), nnTransactionDetails["transaction"]?["service_supplier_id"]?.ToString()));
        }
    }

    public static async Task InsertTransactionDetails(JObject nnTransactionDetails, INovalnetTransactionDetailsService _novalnetTransactionDetailsService)
    {
        if (nnTransactionDetails != null && nnTransactionDetails.HasValues)
        {
            var transaction = new NovalnetTransactionDetails
            {
                OrderNo = nnTransactionDetails["transaction"]?["order_no"]?.ToString(),
                Tid = nnTransactionDetails["transaction"]?["tid"]?.ToString(),
                OrderAmount = nnTransactionDetails["transaction"]?["amount"]?.Value<int>() ?? 0,
                TidStatus = nnTransactionDetails["transaction"]?["status"]?.ToString(),
            };
            await _novalnetTransactionDetailsService.InsertAsync(transaction);
        }
    }

    public static async Task<string> GetLangCodeAsync(IWorkContext _workContext)
    {
        var language = await _workContext.GetWorkingLanguageAsync();
        string langCode = language?.LanguageCulture?.Length >= 2
            ? language.LanguageCulture.Substring(0, 2).ToUpperInvariant()
            : "EN";

        return langCode;
    }

    public static bool AreAddressesEqual(Address billingAddress, Address shippingAddress)
    {
        if (billingAddress == null || shippingAddress == null)
            return false;

        return billingAddress.FirstName == shippingAddress.FirstName &&
            billingAddress.LastName == shippingAddress.LastName &&
            billingAddress.Address1 == shippingAddress.Address1 &&
            billingAddress.Address2 == shippingAddress.Address2 &&
            billingAddress.City == shippingAddress.City &&
            billingAddress.ZipPostalCode == shippingAddress.ZipPostalCode &&
            billingAddress.CountryId == shippingAddress.CountryId &&
            billingAddress.StateProvinceId == shippingAddress.StateProvinceId;
    }

    public static bool IsOver18(string dobInput)
    {
        if (DateTime.TryParseExact(dobInput, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var dob))
        {
            int age = DateTime.Today.Year - dob.Year;
            if (dob.Date > DateTime.Today.AddYears(-age))
                age--;

            return age >= 18;
        }
        return false;
    }

    public static string SerializeToJson(object nnPayload)
    {
        var json = JsonConvert.SerializeObject(nnPayload, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });

        return json.ToString();
    }

    public static async Task<int> GetConvertedAmountCents(decimal amount, ICurrencyService _currencyService, Currency currency)
    {
        decimal convertedAmount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(amount, currency);
        int amountInCents = (int)Math.Round(convertedAmount * 100, MidpointRounding.AwayFromZero);
        return amountInCents;
    }

    public static async Task<bool> DisplayGuaranteedPayment(IWorkContext _workContext, ICustomerService _customerService, ICurrencyService _currencyService, ICountryService _countryService, decimal orderTotal, int GuaranteedPaymentMinAmount, bool allowB2B)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();

        var currency = await _workContext.GetWorkingCurrencyAsync();

        int orderAmountInCents = await GetConvertedAmountCents(orderTotal, _currencyService, currency);

        var billingAddress = await _customerService.GetCustomerBillingAddressAsync(customer);
        var shippingAddress = await _customerService.GetCustomerShippingAddressAsync(customer);

        if (billingAddress == null || shippingAddress == null)
            return false;

        var billingCountry = await _countryService.GetCountryByAddressAsync(billingAddress);
        string countryCode = billingCountry?.TwoLetterIsoCode;

        bool isBillingSameAsShipping = NovalnetHelper.AreAddressesEqual(billingAddress, shippingAddress);

        string company = billingAddress.Company;
        var europeanRegionCountryCodes = new[]
        {
            "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "ES", "FI", "FR", "GR", "HR",
            "HU", "IE", "IT", "LT", "LU", "LV", "MT", "NL", "PL", "PT", "RO", "SE", "SI",
            "SK", "UK", "CH"
        };

        if (isBillingSameAsShipping && currency.CurrencyCode == "EUR" && orderAmountInCents >= GuaranteedPaymentMinAmount && (new[] { "DE", "AT", "CH" }.Contains(countryCode.ToUpper()) || (allowB2B == true && europeanRegionCountryCodes.Contains(countryCode))))
        {
            return true;
        }

        return false;
    }

    public static bool RequiresBic(string iban)
    {
        if (string.IsNullOrEmpty(iban) || iban.Length < 2)
            return false;

        var countryCode = iban.Substring(0, 2).ToUpperInvariant();
        string[] bicRequireCountries = ["CH", "MC", "SM", "GB", "GI"];
        return bicRequireCountries.Contains(countryCode);
    }

    public static async Task<JObject> SendRequestAsync(string endpoint, string jsonData, string paymentAccessKey)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(novalnetEndpoint)
        };

        string encodedAccessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(paymentAccessKey));

        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Add("Charset", "utf-8");
        client.DefaultRequestHeaders.Add("X-NN-Access-Key", encodedAccessKey);

        var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync(endpoint, content);
        string result = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return JsonConvert.DeserializeObject<JObject>(result);
    }

    public static void ClearSessionKeys(IHttpContextAccessor httpContextAccessor, IEnumerable<string> keys)
    {
        if (httpContextAccessor?.HttpContext?.Session == null)
            return;

        foreach (var key in keys)
        {
            httpContextAccessor.HttpContext.Session.Remove(key);
        }
    }

    public static async Task<string> GetNovalnetRedirectUrl(JObject response, PostProcessPaymentRequest postProcessPaymentRequest, IOrderService _orderService, IOrderProcessingService _orderProcessingService, IHttpContextAccessor _httpContextAccessor)
    {
        string redirectUrl = "";

        if (response != null && response.HasValues)
        {
            string status = response["result"]?["status"].ToString();
            if (status == "SUCCESS")
            {
                string txnSecret = response["transaction"]?["txn_secret"]?.ToString();

                var session = _httpContextAccessor.HttpContext?.Session;
                session.SetString("nn_txn_secret", txnSecret.ToString());

                redirectUrl = response["result"]?["redirect_url"].ToString();
                return redirectUrl;
            }
        }

        Order order = postProcessPaymentRequest.Order;
        order.PaymentStatus = PaymentStatus.Voided;
        await _orderService.UpdateOrderAsync(order);
        await _orderProcessingService.CancelOrderAsync(order, true);

        StringBuilder orderNoteString = new StringBuilder();

        string statusText = response?["result"]?["status_text"]?.ToString() ?? "Failed";
        orderNoteString.AppendLine(statusText);

        await NovalnetHelper.InsertTransactionNotes(orderNoteString, order.Id, _orderService);

        redirectUrl = $"/orderdetails/{order.Id}";
        return redirectUrl;
    }

    public static async Task<string> GetIpAddressByHostnameAsync(string hostname)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(hostname);
            return addresses.FirstOrDefault()?.ToString() ?? "";
        }
        catch 
        {
            return "";
        }
    }

    public static string GetServerIpAddress(IHttpContextAccessor _httpContextAccessor)
    {
        return _httpContextAccessor.HttpContext?.Connection?.LocalIpAddress?.ToString() ?? "127.0.0.1";
    }

    #endregion
}

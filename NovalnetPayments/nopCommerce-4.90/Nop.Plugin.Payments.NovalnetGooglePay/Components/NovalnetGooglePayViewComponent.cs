using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetGooglePay.Domain;
using Nop.Plugin.Payments.NovalnetGooglePay.Models;
using Nop.Plugin.Payments.NovalnetGooglePay.Services;
using Nop.Core.Domain.Common;
using Nop.Plugin.Payments.NovalnetAPIConfiguration;
using Nop.Web.Framework.Components;
using Nop.Services.Localization;
using Nop.Services.Customers;
using Nop.Services.Orders;
using Nop.Services.Common;
using Nop.Services.Plugins;
using Nop.Core.Domain.Orders;
using Nop.Services.Directory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nop.Services.Logging;
using Nop.Services.Catalog;

namespace Nop.Plugin.Payments.NovalnetGooglePay.Components;

[ViewComponent(Name = "NovalnetGooglePayPaymentInfo")]
public class NovalnetGooglePayViewComponent : NopViewComponent
{

    protected readonly NovalnetNovalnetGooglePayPaymentSettings _novalnetGooglePayPaymentSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;
    private readonly ICustomerService _customerService;
    private readonly IOrderTotalCalculationService _orderTotalCalculationService;
    private readonly IShoppingCartService _shoppingCartService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly IAddressService _addressService;
    private readonly ICountryService _countryService;
    private readonly IStateProvinceService _stateProvinceService;
    private readonly ICurrencyService _currencyService;
    private readonly NovalnetAPIConfigurationPaymentSettings _novalnetAPIConfigurationPaymentSettings;
    private readonly AddressSettings _addressSettings;
    private readonly IProductService _productService;

    public NovalnetGooglePayViewComponent(NovalnetNovalnetGooglePayPaymentSettings novalnetGooglePayPaymentSettings,
        NovalnetAPIConfigurationPaymentSettings novalnetAPIConfigurationPaymentSettings,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        IWorkContext workContext,
        ICustomerService customerService,
        IOrderTotalCalculationService orderTotalCalculationService,
        IShoppingCartService shoppingCartService,
        IGenericAttributeService genericAttributeService,
        IAddressService addressService,
        ICountryService countryService,
        IStateProvinceService stateProvinceService,
        ICurrencyService currencyService,
        AddressSettings addressSettings,
        IProductService productService
        )
    {
        _novalnetGooglePayPaymentSettings = novalnetGooglePayPaymentSettings;
        _novalnetAPIConfigurationPaymentSettings = novalnetAPIConfigurationPaymentSettings;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _workContext = workContext;
        _customerService = customerService;
        _orderTotalCalculationService = orderTotalCalculationService;
        _shoppingCartService = shoppingCartService;
        _genericAttributeService = genericAttributeService;
        _addressService = addressService;
        _countryService = countryService;
        _stateProvinceService = stateProvinceService;
        _currencyService = currencyService;
        _addressSettings = addressSettings;
        _productService = productService;
    }
    public async Task<IViewComponentResult> InvokeAsync()
    {            
        var store = await _storeContext.GetCurrentStoreAsync();
        var customer = await _workContext.GetCurrentCustomerAsync();
        var currency = await _workContext.GetWorkingCurrencyAsync();
        var language = await _workContext.GetWorkingLanguageAsync();

        var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
        var (orderTotal, _, _, _, _, _) = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart);

        int isVirtualProduct = 0;
        if (cart.Any())
        {
            var allVirtualProducts = true;
            foreach (var item in cart)
            {
                var product = await _productService.GetProductByIdAsync(item.ProductId);
                if (product == null || product.IsShipEnabled)
                {
                    allVirtualProducts = false;
                    break;
                }
            }
            if (allVirtualProducts)
                isVirtualProduct = 1;
        }

        decimal orderTotalValue = orderTotal ?? 0m;
        int orderAmountInCents = await NovalnetHelper.GetConvertedAmountCents(orderTotalValue, _currencyService, currency);
        
        var billingAddress = await _customerService.GetCustomerBillingAddressAsync(customer);
        var shippingAddress = await _customerService.GetCustomerShippingAddressAsync(customer);

        CustomerInfo customerInfo = await NovalnetHelper.GetCustomerInfo(billingAddress, shippingAddress, _countryService, _stateProvinceService);       

        var nnPayload = new
        {
            customer = customerInfo,
            client_key = _novalnetAPIConfigurationPaymentSettings.ClientKey,
            business_name = _novalnetGooglePayPaymentSettings.BusinessName,
            google_merchant_id = _novalnetGooglePayPaymentSettings.GoogleMerchantId,
            default_country_code = await GooglePayCountryHelper.GetDefaultCountryCodeAsync(customer, _customerService, _countryService, _addressSettings),
            transaction = new
            {
                test_mode = _novalnetGooglePayPaymentSettings.TestMode == true ? 1 : 0,
                amount = orderAmountInCents,
                currency = currency.CurrencyCode
            },
            button = new
            {
                type = GooglePayButtonDefaults.GetButtonType(_novalnetGooglePayPaymentSettings),
                height = GooglePayButtonDefaults.GetButtonHeight(_novalnetGooglePayPaymentSettings)
            },
            custom = new
            {
                lang = await NovalnetHelper.GetLangCodeAsync(_workContext)
            }
        };

        var json = JsonConvert.SerializeObject(nnPayload, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });
        
        var model = new PaymentInfoModel()
        {
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetGooglePayPaymentSettings,
                x => x.DescriptionText, language.Id, store.Id),
            TestMode = _novalnetGooglePayPaymentSettings.TestMode,
            PaymentJsonData = json.ToString(),
            IsVirtualProduct = isVirtualProduct
        };

        return View("~/Plugins/Payments.NovalnetGooglePay/Views/PaymentInfo.cshtml", model);
    }
}

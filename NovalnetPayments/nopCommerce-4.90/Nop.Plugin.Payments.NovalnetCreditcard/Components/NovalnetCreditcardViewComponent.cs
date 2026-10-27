using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetCreditcard.Models;
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

namespace Nop.Plugin.Payments.NovalnetCreditcard.Components;

[ViewComponent(Name = "NovalnetCreditcardPaymentInfo")]
public class NovalnetCreditcardViewComponent : NopViewComponent
{
    protected readonly NovalnetCreditcardPaymentSettings _novalnetCreditcardPaymentSettings;
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

    public NovalnetCreditcardViewComponent(NovalnetCreditcardPaymentSettings novalnetCreditcardPaymentSettings,
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
        ICurrencyService currencyService
        )
    {
        _novalnetCreditcardPaymentSettings = novalnetCreditcardPaymentSettings;
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
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var customer = await _workContext.GetCurrentCustomerAsync();
        var currency = await _workContext.GetWorkingCurrencyAsync();
        var language = await _workContext.GetWorkingLanguageAsync();

        var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
        var (orderTotal, _, _, _, _, _) = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart);

        decimal orderTotalValue = orderTotal ?? 0m;
        int orderAmountInCents = await NovalnetHelper.GetConvertedAmountCents(orderTotalValue, _currencyService, currency);
        
        var billingAddress = await _customerService.GetCustomerBillingAddressAsync(customer);
        var shippingAddress = await _customerService.GetCustomerShippingAddressAsync(customer);

        CustomerInfo customerInfo = await NovalnetHelper.GetCustomerInfo(billingAddress, shippingAddress, _countryService, _stateProvinceService);       

        var nnPayload = new
        {
            customer = customerInfo,
            client_key = _novalnetAPIConfigurationPaymentSettings.ClientKey,
            inline_form = _novalnetCreditcardPaymentSettings.InlineForm == true ? 1 : 0,
            label_style = _novalnetCreditcardPaymentSettings.LabelStyle,
            input_style = _novalnetCreditcardPaymentSettings.InputStyle,
            text_style = _novalnetCreditcardPaymentSettings.TextStyle,
            transaction = new
            {
                test_mode = _novalnetCreditcardPaymentSettings.TestMode == true ? 1 : 0,
                amount = orderAmountInCents,
                currency = currency.CurrencyCode,                
                enforce_3d = _novalnetCreditcardPaymentSettings.Enforce3d == true ? 1 : 0,
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
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetCreditcardPaymentSettings,
                x => x.DescriptionText, language.Id, store.Id),
            TestMode = _novalnetCreditcardPaymentSettings.TestMode,
            PaymentJsonData = json.ToString()
        };

        return View("~/Plugins/Payments.NovalnetCreditcard/Views/PaymentInfo.cshtml", model);
    }
}

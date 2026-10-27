using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;
using Nop.Core.Http;
using Nop.Plugin.Payments.NovalnetAPIConfiguration;
using Nop.Plugin.Payments.NovalnetGooglePay.Domain;
using Nop.Plugin.Payments.NovalnetGooglePay.Models;
using Nop.Plugin.Payments.NovalnetGooglePay.Services;
using Nop.Core.Domain.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Core;
using Nop.Services.Catalog;
using Nop.Services.Payments;
using Nop.Services.Shipping;
using Nop.Services.Common;
using Nop.Services.Orders;
using Nop.Core.Domain.Orders;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.Infrastructure;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.Payments.NovalnetGooglePay.Components;

[ViewComponent(Name = "NovalnetGooglePayExpress")]
public class NovalnetGooglePayExpressViewComponent : NopViewComponent
{
    private readonly IProductService _productService;
    private readonly IPaymentPluginManager _paymentPluginManager;
    private readonly IStoreContext _storeContext;
    private readonly IWorkContext _workContext;
    private readonly NovalnetNovalnetGooglePayPaymentSettings _novalnetGooglePayPaymentSettings;
    private readonly NovalnetAPIConfigurationPaymentSettings _novalnetAPIConfigurationPaymentSettings;
    private readonly ICustomerService _customerService;
    private readonly ICountryService _countryService;
    private readonly AddressSettings _addressSettings;
    protected readonly IShoppingCartService _shoppingCartService;
    private readonly OrderSettings _orderSettings;

    public NovalnetGooglePayExpressViewComponent(IProductService productService,
        IPaymentPluginManager paymentPluginManager,
        IStoreContext storeContext,
        IWorkContext workContext,
        NovalnetNovalnetGooglePayPaymentSettings novalnetGooglePayPaymentSettings,
        NovalnetAPIConfigurationPaymentSettings novalnetAPIConfigurationPaymentSettings,
        ICustomerService customerService,
        ICountryService countryService,
        AddressSettings addressSettings,
        IShoppingCartService shoppingCartService,
        OrderSettings orderSettings)
    {
        _productService = productService;
        _paymentPluginManager = paymentPluginManager;
        _storeContext = storeContext;
        _workContext = workContext;
        _novalnetGooglePayPaymentSettings = novalnetGooglePayPaymentSettings;
        _novalnetAPIConfigurationPaymentSettings = novalnetAPIConfigurationPaymentSettings;
        _customerService = customerService;
        _countryService = countryService;
        _addressSettings = addressSettings;
        _shoppingCartService = shoppingCartService;
        _orderSettings = orderSettings;
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var customer = await _workContext.GetCurrentCustomerAsync();

        if (!await _paymentPluginManager.IsPluginActiveAsync("Payments.NovalnetGooglePay", customer, store.Id))
            return Content(string.Empty);

        var credentialsAvailable = NovalnetHelper.CheckNovalnetAPICredentials(_novalnetAPIConfigurationPaymentSettings)
            && !string.IsNullOrWhiteSpace(_novalnetAPIConfigurationPaymentSettings.ClientKey)
            && !string.IsNullOrWhiteSpace(_novalnetGooglePayPaymentSettings.GoogleMerchantId);
        if (!credentialsAvailable)
            return Content(string.Empty);

        //which pages the admin has enabled the Google Pay button for (Configure > "Display the Google Pay Button on") -
        //a null setting means the merchant never touched this (new field on an existing install), so default to
        //product + cart rather than silently hiding the button everywhere
        var displayPages = GooglePayButtonDefaults.GetExpressCheckoutPages(_novalnetGooglePayPaymentSettings);

        ExpressButtonPlacement placement;
        int? productId = null;
        int isVirtualProduct = 0;

        if (widgetZone.Equals(PublicWidgetZones.ProductDetailsAddInfo))
        {
            if (!displayPages.Contains("product"))
                return Content(string.Empty);

            placement = ExpressButtonPlacement.Product;

            productId = additionalData is ProductDetailsModel.AddToCartModel addToCart ? addToCart.ProductId : null;
            if (productId is null)
                return Content(string.Empty);

            var product = await _productService.GetProductByIdAsync(productId.Value);
            if (product == null || product.ParentGroupedProductId != 0)
                return Content(string.Empty);

            if (!product.IsShipEnabled)
                isVirtualProduct = 1;

        }
        else if (widgetZone.Equals(PublicWidgetZones.OrderSummaryContentBefore))
        {
            //OrderSummaryContentBefore is rendered both on the cart page and on every checkout step -
            //the express button is only offered on the cart page
            var routeName = HttpContext.GetEndpoint()?.Metadata.GetMetadata<RouteNameMetadata>()?.RouteName;

            if (routeName != NopRouteNames.General.CART || !displayPages.Contains("cart"))
                return Content(string.Empty);

            placement = ExpressButtonPlacement.Cart;

            var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);

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

        }
        else
        {
            return Content(string.Empty);
        }

        var currency = await _workContext.GetWorkingCurrencyAsync();

        var nnPayload = new
        {
            client_key = _novalnetAPIConfigurationPaymentSettings.ClientKey,
            business_name = _novalnetGooglePayPaymentSettings.BusinessName,
            google_merchant_id = _novalnetGooglePayPaymentSettings.GoogleMerchantId,
            default_country_code = await GooglePayCountryHelper.GetDefaultCountryCodeAsync(customer, _customerService, _countryService, _addressSettings),
            transaction = new
            {
                test_mode = _novalnetGooglePayPaymentSettings.TestMode == true ? 1 : 0,
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

        var checkoutDisabled = _orderSettings.CheckoutDisabled;

        var model = new ExpressCheckoutModel
        {
            Placement = placement,
            ProductId = productId,
            TestMode = _novalnetGooglePayPaymentSettings.TestMode,
            PaymentJsonData = json,
            IsVirtualProduct = isVirtualProduct,
            CheckoutDisabled = checkoutDisabled == true ? 1 : 0
        };

        return View("~/Plugins/Payments.NovalnetGooglePay/Views/Public/GooglePayExpressCheckout.cshtml", model);
    }
}

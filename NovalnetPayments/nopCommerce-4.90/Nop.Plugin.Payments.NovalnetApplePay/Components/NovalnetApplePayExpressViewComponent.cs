using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;
using Nop.Core.Http;
using Nop.Plugin.Payments.NovalnetAPIConfiguration;
using Nop.Plugin.Payments.NovalnetApplePay.Domain;
using Nop.Plugin.Payments.NovalnetApplePay.Models;
using Nop.Plugin.Payments.NovalnetApplePay.Services;
using Nop.Core;
using Nop.Services.Catalog;
using Nop.Services.Payments;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.Infrastructure;
using Nop.Web.Models.Catalog;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Orders;
using Nop.Services.Orders;

namespace Nop.Plugin.Payments.NovalnetApplePay.Components;

[ViewComponent(Name = "NovalnetApplePayExpress")]
public class NovalnetApplePayExpressViewComponent : NopViewComponent
{
    private readonly IProductService _productService;
    private readonly IPaymentPluginManager _paymentPluginManager;
    private readonly IStoreContext _storeContext;
    private readonly IWorkContext _workContext;
    private readonly NovalnetNovalnetApplePayPaymentSettings _novalnetApplePayPaymentSettings;
    private readonly NovalnetAPIConfigurationPaymentSettings _novalnetAPIConfigurationPaymentSettings;

    private readonly ICustomerService _customerService;
    private readonly ICountryService _countryService;
    private readonly AddressSettings _addressSettings;
    protected readonly IShoppingCartService _shoppingCartService;
    private readonly OrderSettings _orderSettings;

    public NovalnetApplePayExpressViewComponent(IProductService productService,
        IPaymentPluginManager paymentPluginManager,
        IStoreContext storeContext,
        IWorkContext workContext,
        NovalnetNovalnetApplePayPaymentSettings novalnetApplePayPaymentSettings,
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
        _novalnetApplePayPaymentSettings = novalnetApplePayPaymentSettings;
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

        if (!await _paymentPluginManager.IsPluginActiveAsync("Payments.NovalnetApplePay", customer, store.Id))
            return Content(string.Empty);

        var credentialsAvailable = NovalnetHelper.CheckNovalnetAPICredentials(_novalnetAPIConfigurationPaymentSettings)
            && !string.IsNullOrWhiteSpace(_novalnetAPIConfigurationPaymentSettings.ClientKey);
        if (!credentialsAvailable)
            return Content(string.Empty);

        //which pages the admin has enabled the Apple Pay button for (Configure > "Display Apple Pay button on")
        var displayPages = (_novalnetApplePayPaymentSettings.DisplayPages ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
            //tell those two apart by route name, and gate each independently by DisplayPages
            var routeName = HttpContext.GetEndpoint()?.Metadata.GetMetadata<RouteNameMetadata>()?.RouteName;

            if (routeName == NopRouteNames.General.CART)
            {
                if (!displayPages.Contains("cart"))
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
        }
        else
        {
            return Content(string.Empty);
        }

        var currency = await _workContext.GetWorkingCurrencyAsync();

        var nnPayload = new
        {
            client_key = _novalnetAPIConfigurationPaymentSettings.ClientKey,
            business_name = _novalnetApplePayPaymentSettings.BusinessName,
            button_type = _novalnetApplePayPaymentSettings.ButtonType,
            button_theme = _novalnetApplePayPaymentSettings.ButtonTheme,
            button_height = _novalnetApplePayPaymentSettings.ButtonHeight,
            button_corner_radius = _novalnetApplePayPaymentSettings.ButtonCornerRadius,
            default_country_code = await ApplePayCountryHelper.GetDefaultCountryCodeAsync(customer, _customerService, _countryService, _addressSettings),
            transaction = new
            {
                test_mode = _novalnetApplePayPaymentSettings.TestMode == true ? 1 : 0,
                currency = currency.CurrencyCode,
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
            TestMode = _novalnetApplePayPaymentSettings.TestMode,
            PaymentJsonData = json,
            IsVirtualProduct = isVirtualProduct,
            CheckoutDisabled = checkoutDisabled == true ? 1 : 0
        };

        return View("~/Plugins/Payments.NovalnetApplePay/Views/Public/ApplePayExpressCheckout.cshtml", model);
    }
}

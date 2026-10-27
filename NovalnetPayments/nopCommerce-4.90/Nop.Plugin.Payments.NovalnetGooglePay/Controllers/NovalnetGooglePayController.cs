using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Stores;
using Nop.Core.Http;
using Nop.Plugin.Payments.NovalnetAPIConfiguration;
using Nop.Plugin.Payments.NovalnetGooglePay;
using Nop.Plugin.Payments.NovalnetGooglePay.Domain;
using Nop.Plugin.Payments.NovalnetGooglePay.Models;
using Nop.Services.Attributes;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Services.Shipping;
using Nop.Services.Stores;
using Nop.Services.Tax;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Services.Shipping.Pickup;
using Nop.Core.Domain.Shipping;
using System.Text.Json;

namespace Nop.Plugin.Payments.NovalnetGooglePay.Controllers;

[AutoValidateAntiforgeryToken]
public class NovalnetGooglePayController : BasePaymentController
{
    #region Fields

    protected readonly ILanguageService _languageService;
    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly IPermissionService _permissionService;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;
    protected readonly IShoppingCartService _shoppingCartService;
    protected readonly IShippingService _shippingService;
    protected readonly IOrderTotalCalculationService _orderTotalCalculationService;
    protected readonly ICountryService _countryService;
    protected readonly IStateProvinceService _stateProvinceService;
    protected readonly ICurrencyService _currencyService;
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly ICustomerService _customerService;
    protected readonly IAddressService _addressService;
    protected readonly IProductService _productService;
    protected readonly IPriceCalculationService _priceCalculationService;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly ITaxService _taxService;
    protected readonly IOrderProcessingService _orderProcessingService;
    protected readonly IPaymentPluginManager _paymentPluginManager;
    protected readonly Nop.Services.Payments.IPaymentService _paymentService;
    protected readonly OrderSettings _orderSettings;
    protected readonly IWebHelper _webHelper;
    protected readonly IAttributeService<CheckoutAttribute, CheckoutAttributeValue> _checkoutAttributeService;
    protected readonly IAttributeParser<CheckoutAttribute, CheckoutAttributeValue> _checkoutAttributeParser;
    protected readonly IStaticCacheManager _staticCacheManager;
    protected readonly IStoreMappingService _storeMappingService;
    private readonly IPickupPluginManager _pickupPluginManager;
    private readonly ShippingSettings _shippingSettings;

    #endregion

    #region Ctor

    public NovalnetGooglePayController(ILanguageService languageService,
        ILocalizationService localizationService,
        INotificationService notificationService,
        IPermissionService permissionService,
        ISettingService settingService,
        IStoreContext storeContext,
        IWorkContext workContext,
        IShoppingCartService shoppingCartService,
        IShippingService shippingService,
        IOrderTotalCalculationService orderTotalCalculationService,
        ICountryService countryService,
        IStateProvinceService stateProvinceService,
        ICurrencyService currencyService,
        IGenericAttributeService genericAttributeService,
        ICustomerService customerService,
        IAddressService addressService,
        IProductService productService,
        IPriceCalculationService priceCalculationService,
        IPriceFormatter priceFormatter,
        ITaxService taxService,
        IOrderProcessingService orderProcessingService,
        IPaymentPluginManager paymentPluginManager,
        Nop.Services.Payments.IPaymentService paymentService,
        OrderSettings orderSettings,
        IWebHelper webHelper,
        IAttributeService<CheckoutAttribute, CheckoutAttributeValue> checkoutAttributeService,
        IAttributeParser<CheckoutAttribute, CheckoutAttributeValue> checkoutAttributeParser,
        IStaticCacheManager staticCacheManager,
        IStoreMappingService storeMappingService,
        IPickupPluginManager pickupPluginManager,
        ShippingSettings shippingSettings)
    {
        _languageService = languageService;
        _localizationService = localizationService;
        _notificationService = notificationService;
        _permissionService = permissionService;
        _settingService = settingService;
        _storeContext = storeContext;
        _workContext = workContext;
        _shoppingCartService = shoppingCartService;
        _shippingService = shippingService;
        _orderTotalCalculationService = orderTotalCalculationService;
        _countryService = countryService;
        _stateProvinceService = stateProvinceService;
        _currencyService = currencyService;
        _genericAttributeService = genericAttributeService;
        _customerService = customerService;
        _addressService = addressService;
        _productService = productService;
        _priceCalculationService = priceCalculationService;
        _priceFormatter = priceFormatter;
        _taxService = taxService;
        _orderProcessingService = orderProcessingService;
        _paymentPluginManager = paymentPluginManager;
        _paymentService = paymentService;
        _orderSettings = orderSettings;
        _webHelper = webHelper;
        _checkoutAttributeService = checkoutAttributeService;
        _checkoutAttributeParser = checkoutAttributeParser;
        _staticCacheManager = staticCacheManager;
        _storeMappingService = storeMappingService;
        _pickupPluginManager = pickupPluginManager;
        _shippingSettings = shippingSettings;
    }

    #endregion

    #region Methods

    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure()
    {
        //load settings for a chosen store scope
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var novalnetGooglePayPaymentSettings = await _settingService.LoadSettingAsync<NovalnetNovalnetGooglePayPaymentSettings>(storeScope);

        var model = new ConfigurationModel
        {
            AdditionalFee = novalnetGooglePayPaymentSettings.AdditionalFee,
            AdditionalFeePercentage = novalnetGooglePayPaymentSettings.AdditionalFeePercentage,
            ActiveStoreScopeConfiguration = storeScope,
            DescriptionText = novalnetGooglePayPaymentSettings.DescriptionText,
            TestMode = novalnetGooglePayPaymentSettings.TestMode,
            OnholdMinAmount = novalnetGooglePayPaymentSettings.OnholdMinAmount,
            BusinessName = novalnetGooglePayPaymentSettings.BusinessName,
            GoogleMerchantId = novalnetGooglePayPaymentSettings.GoogleMerchantId,
            //unsaved values (new fields on an existing install) reflect the defaults the storefront actually uses
            SelectedExpressCheckoutPages = GooglePayButtonDefaults.GetExpressCheckoutPages(novalnetGooglePayPaymentSettings),
            ButtonHeight = GooglePayButtonDefaults.GetButtonHeight(novalnetGooglePayPaymentSettings),
            ButtonType = GooglePayButtonDefaults.GetButtonType(novalnetGooglePayPaymentSettings),
        };

        //locales
        await AddLocalesAsync(_languageService, model.Locales, async (locale, languageId) =>
        {
            locale.DescriptionText = await _localizationService
                .GetLocalizedSettingAsync(novalnetGooglePayPaymentSettings, x => x.DescriptionText, languageId, 0, false, false);
        });

        model.AvailablePaymentActions = new List<SelectListItem>
        {
            new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.Authorize"), Value = "1" },
            new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.Capture"), Value = "0" }
        };
        model.PaymentActionString = ((int)novalnetGooglePayPaymentSettings.PaymentAction).ToString();

        model.AvailableExpressCheckoutPages = new List<SelectListItem>
        {
            new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetGooglePay.ExpressCheckoutPages.Cart"), Value = "cart" },
            new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetGooglePay.ExpressCheckoutPages.Product"), Value = "product" },
        };

        model.AvailableButtonTypes = new List<SelectListItem>
        {
            new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetGooglePay.ButtonType.Default"), Value = "default" },
            new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetGooglePay.ButtonType.Buy"), Value = "buy" },
            new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetGooglePay.ButtonType.Donate"), Value = "donate" },
            new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetGooglePay.ButtonType.Book"), Value = "book" },
            new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetGooglePay.ButtonType.Contribute"), Value = "contribute" },
            new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetGooglePay.ButtonType.Checkout"), Value = "checkout" },
            new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetGooglePay.ButtonType.Rent"), Value = "rent" },
            new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetGooglePay.ButtonType.Support"), Value = "support" },
        };

        if (storeScope > 0)
        {
            model.AdditionalFee_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGooglePayPaymentSettings, x => x.AdditionalFee, storeScope);
            model.AdditionalFeePercentage_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGooglePayPaymentSettings, x => x.AdditionalFeePercentage, storeScope);
            model.DescriptionText_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGooglePayPaymentSettings, x => x.DescriptionText, storeScope);
            model.PaymentAction_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGooglePayPaymentSettings, x => x.PaymentAction, storeScope);
            model.TestMode_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGooglePayPaymentSettings, x => x.TestMode, storeScope);
            model.OnholdMinAmount_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGooglePayPaymentSettings, x => x.OnholdMinAmount, storeScope);
            model.BusinessName_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGooglePayPaymentSettings, x => x.BusinessName, storeScope);
            model.GoogleMerchantId_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGooglePayPaymentSettings, x => x.GoogleMerchantId, storeScope);
            model.ExpressCheckoutPages_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGooglePayPaymentSettings, x => x.ExpressCheckoutPages, storeScope);
            model.ButtonHeight_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGooglePayPaymentSettings, x => x.ButtonHeight, storeScope);
            model.ButtonType_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGooglePayPaymentSettings, x => x.ButtonType, storeScope);
        }

        return View("~/Plugins/Payments.NovalnetGooglePay/Views/Configure.cshtml", model);
    }

    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return await Configure();

        //load settings for a chosen store scope
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var novalnetGooglePayPaymentSettings = await _settingService.LoadSettingAsync<NovalnetNovalnetGooglePayPaymentSettings>(storeScope);

        if (int.TryParse(model.PaymentActionString, out var parsedAction))
        {
            novalnetGooglePayPaymentSettings.PaymentAction = (PaymentAction)parsedAction;
        }

        //save settings
        novalnetGooglePayPaymentSettings.AdditionalFee = model.AdditionalFee;
        novalnetGooglePayPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
        novalnetGooglePayPaymentSettings.TestMode = model.TestMode;
        novalnetGooglePayPaymentSettings.OnholdMinAmount = model.OnholdMinAmount ?? 0;
        novalnetGooglePayPaymentSettings.DescriptionText = model.DescriptionText;
        novalnetGooglePayPaymentSettings.BusinessName = model.BusinessName;
        novalnetGooglePayPaymentSettings.GoogleMerchantId = model.GoogleMerchantId;
        novalnetGooglePayPaymentSettings.ExpressCheckoutPages = string.Join(",", model.SelectedExpressCheckoutPages ?? new List<string>());
        novalnetGooglePayPaymentSettings.ButtonHeight = model.ButtonHeight;
        novalnetGooglePayPaymentSettings.ButtonType = model.ButtonType;

        /* We do not clear cache after each setting update.
         * This behavior can increase performance because cached settings will not be cleared
         * and loaded from database after each update */

        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGooglePayPaymentSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGooglePayPaymentSettings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGooglePayPaymentSettings, x => x.DescriptionText, model.DescriptionText_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGooglePayPaymentSettings, x => x.TestMode, model.TestMode_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGooglePayPaymentSettings, x => x.OnholdMinAmount, model.OnholdMinAmount_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGooglePayPaymentSettings, x => x.BusinessName, model.BusinessName_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGooglePayPaymentSettings, x => x.GoogleMerchantId, model.GoogleMerchantId_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGooglePayPaymentSettings, x => x.PaymentAction, model.PaymentAction_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGooglePayPaymentSettings, x => x.ExpressCheckoutPages, model.ExpressCheckoutPages_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGooglePayPaymentSettings, x => x.ButtonHeight, model.ButtonHeight_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGooglePayPaymentSettings, x => x.ButtonType, model.ButtonType_OverrideForStore, storeScope, false);

        //now clear settings cache
        await _settingService.ClearCacheAsync();

        //localization. no multi-store support for localization yet.
        foreach (var localized in model.Locales)
        {
            await _localizationService.SaveLocalizedSettingAsync(novalnetGooglePayPaymentSettings,
                x => x.DescriptionText, localized.LanguageId, localized.DescriptionText);
        }

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return RedirectToAction("Configure");
    }

    /// <summary>
    /// Checks that the one page checkout is still in the state the Google Pay sheet was opened for.
    /// nopCommerce clears the selected payment method whenever the cart changes (e.g. an item is added
    /// or removed in another tab) or checkout is restarted, after which OpcSavePaymentInfo fails with
    /// "Payment method is not selected" - so the checkout must be restarted rather than submitted
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CheckCheckoutState()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();

        var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
        var selectedPaymentMethod = await _genericAttributeService.GetAttributeAsync<string>(customer,
            NopCustomerDefaults.SelectedPaymentMethodAttribute, store.Id);

        var valid = cart.Any() && string.Equals(selectedPaymentMethod, "Payments.NovalnetGooglePay", StringComparison.InvariantCultureIgnoreCase);
        if (!valid)
        {
            return Json(new { valid = false, message = "Your cart or checkout has changed. Please review your order and complete the checkout again." });
        }

        return Json(new { valid = true });
    }

    /// <summary>
    /// Calculates real shipping options and cart line items for the given (partial) address,
    /// so the Google Pay sheet can be updated dynamically instead of using hardcoded values
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UpdateShipping()
    {
        var city = Request.Form["city"].ToString();
        var state = Request.Form["state"].ToString();
        var country = Request.Form["country"].ToString();
        var postalCode = Request.Form["postalCode"].ToString();
        var selectedOptionId = Request.Form["selectedOptionId"].ToString();
        var pageType = Request.Form["pageType"].ToString();
        
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var currency = await _workContext.GetWorkingCurrencyAsync();

        var cart = (await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id)).ToList();
        if (!cart.Any())
        {
            return Json(new { error = "Cart is empty" });
        }

        var countryEntity = await _countryService.GetCountryByTwoLetterIsoCodeAsync(country);

        StateProvince stateEntity = null;
        if (countryEntity != null && !string.IsNullOrEmpty(state))
        {
            var states = await _stateProvinceService.GetStateProvincesByCountryIdAsync(countryEntity.Id);
            stateEntity = states.FirstOrDefault(s =>
                string.Equals(s.Abbreviation, state, StringComparison.InvariantCultureIgnoreCase) ||
                string.Equals(s.Name, state, StringComparison.InvariantCultureIgnoreCase));
        }

        var shippingAddress = new Address
        {
            City = city,
            CountryId = countryEntity?.Id,
            StateProvinceId = stateEntity?.Id,
            ZipPostalCode = postalCode,
            CreatedOnUtc = DateTime.UtcNow
        };

        var shippingResponse = await _shippingService.GetShippingOptionsAsync(cart, shippingAddress, customer, storeId: store.Id);
        if (!shippingResponse.Success || !shippingResponse.ShippingOptions.Any())
        {
            return Json(new { error = "No shipping options available for this address" });
        }

        var options = new List<(Nop.Core.Domain.Shipping.ShippingOption Option, decimal Rate)>();
        foreach (var option in shippingResponse.ShippingOptions)
        {
            var (adjustedRate, _) = await _orderTotalCalculationService.AdjustShippingRateAsync(option.Rate, cart);
            options.Add((option, adjustedRate));
        }

        if (string.Equals(pageType, "checkout", StringComparison.OrdinalIgnoreCase))
        {
            var checkoutShippingOption = await _genericAttributeService.GetAttributeAsync<ShippingOption>(customer, NopCustomerDefaults.SelectedShippingOptionAttribute, store.Id);

            if (checkoutShippingOption != null)
            {
                selectedOptionId = checkoutShippingOption.Name;
            }
        }

        var selected = options.FirstOrDefault(o => string.Equals(o.Option.Name, selectedOptionId, StringComparison.InvariantCultureIgnoreCase));

        ShippingOption selectedShippingOption = null;
        PickupPoint selectedPickupPoint = null;
        decimal selectedShippingRate = 0m;

        if (selected.Option != null)
        {
            selectedShippingOption = selected.Option;
            selectedShippingRate = selected.Rate;
        }

        if (selectedShippingOption == null && _shippingSettings.AllowPickupInStore)
        {
            var pickupPointProviders = await _pickupPluginManager.LoadActivePluginsAsync(customer, store.Id);

            if (pickupPointProviders.Any())
            {
                var billingAddress = await _customerService.GetCustomerBillingAddressAsync(customer);

                var pickupPointsResponse = await _shippingService.GetPickupPointsAsync(cart, billingAddress, customer, storeId: store.Id);

                if (pickupPointsResponse.Success && pickupPointsResponse.PickupPoints.Any())
                {
                    foreach (var point in pickupPointsResponse.PickupPoints)
                    {
                        var pickupOptionName = string.IsNullOrEmpty(point.Name) ? await _localizationService.GetResourceAsync("Checkout.PickupPoints.NullName") : string.Format( await _localizationService.GetResourceAsync("Checkout.PickupPoints.Name"), point.Name);

                        if (!string.Equals( pickupOptionName, selectedOptionId, StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue;
                        }

                        var (adjustedPickupRate, _) = await _orderTotalCalculationService.AdjustShippingRateAsync(point. PickupFee,cart, true);

                        selectedPickupPoint = point;
                        selectedShippingRate = adjustedPickupRate;

                        selectedShippingOption = new ShippingOption
                        {
                            Name = pickupOptionName,
                            Rate = adjustedPickupRate,
                            Description = point.Description,
                            ShippingRateComputationMethodSystemName = point.ProviderSystemName,
                            IsPickupInStore = true,
                            DisplayOrder = point.DisplayOrder,
                            TransitDays = point.TransitDays
                        };

                        break;
                    }
                }
            }
        }

        if (selectedShippingOption == null)
        {
            var firstOption = options.FirstOrDefault();

            if (firstOption.Option != null)
            {
                selectedShippingOption = firstOption.Option;
                selectedShippingRate = firstOption.Rate;
            }
        }

        await _genericAttributeService.SaveAttributeAsync(customer, NopCustomerDefaults.SelectedShippingOptionAttribute, selectedShippingOption, store.Id);

        var (cartTotal, _, _, _, _, _) = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart, usePaymentMethodAdditionalFee: false);
        var (taxTotal, _) = await _orderTotalCalculationService.GetTaxTotalAsync(cart);

        async Task<int> ToCentsAsync(decimal amount) => await NovalnetHelper.GetConvertedAmountCents(amount, _currencyService, currency);

        var lineItems = await PrepareCartProductLineItemsAsync(cart, customer, currency);
        var shippingLabel = string.Format(await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetGooglePay.LineItems.Shipping"), selectedShippingOption.Name);
        lineItems.Add(new { label = shippingLabel, type = "SUBTOTAL", amount = await ToCentsAsync(selectedShippingRate) });
        lineItems.Add(new { label = "Tax", type = "TAX", amount = await ToCentsAsync(taxTotal) });

        var (paymentFeeLineItem, paymentMethodAdditionalFee) = await AddPaymentMethodAdditionalFeeLineItemAsync(cart, currency);
        if (paymentFeeLineItem != null)
            lineItems.Add(paymentFeeLineItem);

        var shippingOptionsResult = new List<(string Identifier, string Label, string Detail, int Amount)>();

        foreach (var o in options)
        {
            shippingOptionsResult.Add((o.Option.Name, o.Option.Name, o.Option.Description, await ToCentsAsync(o.Rate)));
        }

        var pickupStoreOptions = await GetPickupStoreShippingOptionsAsync(cart, shippingAddress, customer, store.Id, currency);
        shippingOptionsResult.AddRange(pickupStoreOptions);

        var result = new
        {
            amount = await ToCentsAsync((cartTotal ?? 0m) + paymentMethodAdditionalFee),
            lineItems,
            shippingOptions = shippingOptionsResult.Select(o => new
            {
                identifier = o.Identifier,
                label = o.Label,
                detail = o.Detail,
                amount = o.Amount
            }).ToList(),
            defaultIdentifier = selectedShippingOption.Name
        };

        return Json(result);
    }

    /// <summary>
    /// Returns the amount and a subtotal/tax line item breakdown (no shipping yet - the buyer hasn't
    /// picked a shipping address in the Google Pay sheet at this point) used to seed the express
    /// button's payment intent, on the product details / cart pages
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> GetExpressTransactionInfo(int? productId = null)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var currency = await _workContext.GetWorkingCurrencyAsync();

        var cart = (await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id)).ToList();

        //lets the product page's onPaymentButtonClicked decide whether it still needs to add the product to the
        //cart, or whether a previous click already did so (the sheet was closed without completing payment) -
        //adding it again on every click would keep piling up quantity of the same product
        var productInCart = productId.HasValue && cart.Any(item => item.ProductId == productId.Value);

        async Task<int> ToCentsAsync(decimal cartAmount) => await NovalnetHelper.GetConvertedAmountCents(cartAmount, _currencyService, currency);

        decimal amount;
        List<object> lineItems;

        if (cart.Any())
        {
            var (_, _, _, subTotalWithDiscount, _) = await _orderTotalCalculationService.GetShoppingCartSubTotalAsync(cart, false);
            var (orderTotal, _, _, _, _, _) = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart, usePaymentMethodAdditionalFee: false);
            var (taxTotal, _) = await _orderTotalCalculationService.GetTaxTotalAsync(cart);
            amount = orderTotal ?? 0;

            lineItems = await PrepareCartProductLineItemsAsync(cart, customer, currency);
            lineItems.Add(new { label = await _localizationService.GetResourceAsync("ShoppingCart.Totals.Tax"), type = "TAX", amount = await ToCentsAsync(taxTotal) });
        }
        else if (productId.HasValue)
        {
            //product page, first render, before the item is in the cart - estimate from the product's own price
            var product = await _productService.GetProductByIdAsync(productId.Value);
            if (product == null)
            {
                return Json(new { error = "Product not found" });
            }

            var (_, finalPrice, _, _) = await _priceCalculationService.GetFinalPriceAsync(product, customer, store, quantity: 1);
            var (priceWithTax, _) = await _taxService.GetProductPriceAsync(product, finalPrice);
            amount = priceWithTax;
            var taxAmount = priceWithTax - finalPrice;

            var productName = await _localizationService.GetLocalizedAsync(product, x => x.Name);

            lineItems = new List<object>
            {
                await PrepareProductLineItemAsync(productName, 1, finalPrice, finalPrice, currency),
                new { label = await _localizationService.GetResourceAsync("ShoppingCart.Totals.Tax"), type = "TAX", amount = await ToCentsAsync(taxAmount) }
            };
        }
        else
        {
            return Json(new { error = "Cart is empty" });
        }

        var (paymentFeeLineItem, paymentMethodAdditionalFee) = await AddPaymentMethodAdditionalFeeLineItemAsync(cart, currency);
        if (paymentFeeLineItem != null)
            lineItems.Add(paymentFeeLineItem);

        return Json(new
        {
            amount = await ToCentsAsync(amount + paymentMethodAdditionalFee),
            lineItems,
            productInCart
        });
    }

    /// <summary>
    /// Places (and pays for) the order immediately after the Google Pay sheet completes, from the
    /// product details / cart pages. Unlike the checkout Payment Info step, no billing/shipping address
    /// exists on the customer yet, so it's built here from the contact info the Google Pay sheet collected
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> PlaceExpressOrder(string nnGooglePayResponse)
    {
        try
        {
            var customer = await _workContext.GetCurrentCustomerAsync();
            var store = await _storeContext.GetCurrentStoreAsync();

            if (await _customerService.IsGuestAsync(customer) && !_orderSettings.AnonymousCheckoutAllowed)
            {
                return Json(new { error = "Guest checkout is not allowed" });
            }

            var cart = (await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id)).ToList();
            if (!cart.Any())
            {
                return Json(new { error = "Cart is empty" });
            }

            var response = JsonConvert.DeserializeObject<ExpressGooglePayResponseModel>(nnGooglePayResponse ?? string.Empty);
            if (string.IsNullOrEmpty(response?.Token))
            {
                return Json(new { error = "Missing Google Pay token" });
            }

            var billingContact = response.Order?.Billing?.Contact;
            if (billingContact == null)
                return Json(new { error = "Google Pay did not return a billing address" });

            var billingEmail = response.Order?.Billing?.Email ?? billingContact.Email;
            if (string.IsNullOrWhiteSpace(billingEmail))
                return Json(new { error = "Google Pay did not return an email address" });

            var billingAddress = await BuildExpressAddressAsync(billingContact, billingEmail);
            if (billingAddress.CountryId == null)
                return Json(new { error = "Google Pay did not return a recognized billing country" });

            await _addressService.InsertAddressAsync(billingAddress);
            await _customerService.InsertCustomerAddressAsync(customer, billingAddress);
            customer.BillingAddressId = billingAddress.Id;

            var paymentMethodObj = await _paymentPluginManager.LoadPluginBySystemNameAsync("Payments.NovalnetGooglePay", customer, store.Id);
            if (paymentMethodObj == null)
            {
                return Json(new { error = "Google Pay is not available" } );
            }

            var restrictedCountryIds = await _paymentPluginManager.GetRestrictedCountryIdsAsync(paymentMethodObj);

            if(restrictedCountryIds.Contains((int)billingAddress.CountryId.Value))
            {
                return Json(new { error = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetGooglePay.CountryRestriction.ErrorText") } );
            }

            if (await _shoppingCartService.ShoppingCartRequiresShippingAsync(cart))
            {
                //Google Pay's shipping contact normally carries no email (only the billing/payer contact does),
                //but nopCommerce requires every Address - including the shipping one - to have a valid email
                //(see OrderProcessingService.PrepareShippingAsync), so fall back to the billing email here too
                var shippingContact = response.Order?.Shipping?.Contact ?? billingContact;
                var shippingAddress = await BuildExpressAddressAsync(shippingContact, billingEmail);
                if (shippingAddress.CountryId == null)
                    return Json(new { error = "Google Pay did not return a recognized shipping country" });

                await _addressService.InsertAddressAsync(shippingAddress);
                await _customerService.InsertCustomerAddressAsync(customer, shippingAddress);
                customer.ShippingAddressId = shippingAddress.Id;
            }

            await _customerService.UpdateCustomerAsync(customer);

            await _genericAttributeService.SaveAttributeAsync(customer,
                NopCustomerDefaults.SelectedPaymentMethodAttribute, "Payments.NovalnetGooglePay", store.Id);

            //the express flow has no UI step to collect required checkout attributes (e.g. "Gift wrapping") -
            //normally the customer selects those on the cart page before the checkout wizard runs. Rather than
            //blocking the purchase at this point (the buyer already authorized payment in the Google Pay sheet),
            //auto-select a value for anything still missing so PlaceOrderAsync's own validation passes, the same
            //way other express/one-click payment buttons bypass this instead of turning the order away
            await AutoSelectRequiredCheckoutAttributesAsync(cart, customer, store);

            var processPaymentRequest = new ProcessPaymentRequest
            {
                StoreId = store.Id,
                CustomerId = customer.Id,
                PaymentMethodSystemName = "Payments.NovalnetGooglePay"
            };

            //stashed in session rather than ProcessPaymentRequest.CustomValues - CustomValues get persisted onto
            //the order and shown verbatim on the admin Order Details page regardless of DisplayToCustomer (that
            //flag only hides them from the customer-facing views, not admin). PlaceOrderAsync below synchronously
            //invokes ProcessPaymentAsync/PostProcessPaymentAsync in this same request, which read it back out
            HttpContext.Session.SetString("nnGooglePayToken", response.Token);
            HttpContext.Session.SetString("nnGooglePayDoRedirect", response.DoRedirect ?? "0");

            await _orderProcessingService.SetProcessPaymentRequestAsync(processPaymentRequest);
            var placeOrderResult = await _orderProcessingService.PlaceOrderAsync(processPaymentRequest);

            if (!placeOrderResult.Success)
            {
                await _orderProcessingService.SetProcessPaymentRequestAsync(null);
            }

            await _orderProcessingService.SetProcessPaymentRequestAsync(null);

            var paymentMethod = await _paymentPluginManager.LoadPluginBySystemNameAsync(
                placeOrderResult.PlacedOrder.PaymentMethodSystemName, customer, store.Id);

            var postProcessPaymentRequest = new PostProcessPaymentRequest
            {
                Order = placeOrderResult.PlacedOrder
            };

            //doRedirect (3D secure): PostProcessPaymentAsync gets the redirect_url from Novalnet and, because of this
            //item, hands it back here instead of redirecting this AJAX request - the JS then sends the buyer there,
            //and Novalnet returns them to NovalnetPayment/RedirectResponseHandler
            HttpContext.Items[NovalnetGooglePayPaymentProcessor.ExpressRedirectUrlItemKey] = null;
            await _paymentService.PostProcessPaymentAsync(postProcessPaymentRequest);
            var novalnetRedirectUrl = HttpContext.Items[NovalnetGooglePayPaymentProcessor.ExpressRedirectUrlItemKey] as string;
            
            if (!string.IsNullOrEmpty(novalnetRedirectUrl))
                return Json(new { success = 1, redirectUrl = novalnetRedirectUrl });

            return Json(new
            {
                success = 1,
                redirect = Url.RouteUrl(NopRouteNames.Standard.CHECKOUT_COMPLETED, new { orderId = placeOrderResult.PlacedOrder.Id })
            });
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Builds one Google Pay line item per cart item, labelled "Product name (quantity x unit price)" with the
    /// item's subtotal (before discounts) as its amount, followed by a single "Discount" line with the negative
    /// sum of all product, order subtotal and order total discounts when any apply.
    /// Prices are excluding tax, as tax is shown as a separate line item
    /// </summary>
    protected virtual async Task<List<object>> PrepareCartProductLineItemsAsync(IList<ShoppingCartItem> cart, Customer customer, Currency currency)
    {
        var lineItems = new List<object>();
        var productDiscount = decimal.Zero;

        foreach (var item in cart)
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId);
            if (product == null)
                continue;

            //product (SKU/category/manufacturer) discounts are shown on the discount line instead of lowering the product price
            var (unitPrice, _, _) = await _shoppingCartService.GetUnitPriceAsync(item, false);
            var (unitPriceExclTax, _) = await _taxService.GetProductPriceAsync(product, unitPrice, false, customer);
            var (subTotal, _, _, _) = await _shoppingCartService.GetSubTotalAsync(item, false);
            var (subTotalExclTax, _) = await _taxService.GetProductPriceAsync(product, subTotal, false, customer);
            var (discountedSubTotal, _, _, _) = await _shoppingCartService.GetSubTotalAsync(item, true);
            var (discountedSubTotalExclTax, _) = await _taxService.GetProductPriceAsync(product, discountedSubTotal, false, customer);
            productDiscount += subTotalExclTax - discountedSubTotalExclTax;

            var productName = await _localizationService.GetLocalizedAsync(product, x => x.Name);
            lineItems.Add(await PrepareProductLineItemAsync(productName, item.Quantity, unitPriceExclTax, subTotalExclTax, currency));
        }

        var (subTotalDiscount, _, _, _, _) = await _orderTotalCalculationService.GetShoppingCartSubTotalAsync(cart, false);
        var (_, orderTotalDiscount, _, _, _, _) = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart);

        var totalDiscount = productDiscount + subTotalDiscount + orderTotalDiscount;
        if (totalDiscount > decimal.Zero)
        {
            lineItems.Add(new
            {
                label = await _localizationService.GetResourceAsync("ShoppingCart.Totals.OrderTotalDiscount"),
                type = "LINE_ITEM",
                amount = -await NovalnetHelper.GetConvertedAmountCents(totalDiscount, _currencyService, currency)
            });
        }

        return lineItems;
    }

    /// <summary>
    /// Builds a single product line item in the "Product name (quantity x unit price)" = total format
    /// </summary>
    protected virtual async Task<object> PrepareProductLineItemAsync(string productName, int quantity, decimal unitPrice, decimal total, Currency currency)
    {
        var convertedUnitPrice = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(unitPrice, currency);
        var formattedUnitPrice = await _priceFormatter.FormatPriceAsync(convertedUnitPrice, true, currency);

        return new
        {
            label = $"{productName} ({quantity} x {formattedUnitPrice})",
            type = "LINE_ITEM",
            amount = await NovalnetHelper.GetConvertedAmountCents(total, _currencyService, currency)
        };
    }

    /// <summary>
    /// Maps the contact info the Google Pay sheet collected (as normalized by the Novalnet SDK) onto a
    /// nopCommerce Address entity, resolving the country/state text against the store's configured lists
    /// </summary>
    protected virtual async Task<Address> BuildExpressAddressAsync(ExpressContactModel contact, string emailOverride)
    {
        Country country = null;
        if (!string.IsNullOrEmpty(contact.CountryCode))
            country = await _countryService.GetCountryByTwoLetterIsoCodeAsync(contact.CountryCode);

        StateProvince state = null;
        if (country != null && !string.IsNullOrEmpty(contact.AdministrativeArea))
        {
            var states = await _stateProvinceService.GetStateProvincesByCountryIdAsync(country.Id);
            state = states.FirstOrDefault(s =>
                string.Equals(s.Abbreviation, contact.AdministrativeArea, StringComparison.InvariantCultureIgnoreCase) ||
                string.Equals(s.Name, contact.AdministrativeArea, StringComparison.InvariantCultureIgnoreCase));
        }

        return new Address
        {
            FirstName = contact.FirstName ?? string.Empty,
            LastName = contact.LastName ?? string.Empty,
            Email = emailOverride ?? contact.Email ?? string.Empty,
            PhoneNumber = contact.PhoneNumber ?? string.Empty,
            Address1 = contact.AddressLines ?? string.Empty,
            City = contact.Locality ?? string.Empty,
            StateProvinceId = state?.Id,
            CountryId = country?.Id,
            ZipPostalCode = contact.PostalCode ?? string.Empty,
            CreatedOnUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Auto-selects a value for any store-required checkout attribute (e.g. "Gift wrapping") that the customer
    /// hasn't already selected, and saves it onto the customer so PlaceOrderAsync's built-in validation - which
    /// always requires one - passes. The express flow has no page where the customer could pick these, so this
    /// bypasses the requirement instead of blocking the order after payment has already been authorized
    /// </summary>
    protected virtual async Task AutoSelectRequiredCheckoutAttributesAsync(IList<ShoppingCartItem> cart, Customer customer, Store store)
    {
        var checkoutAttributesXml = await _genericAttributeService.GetAttributeAsync<string>(customer, NopCustomerDefaults.CheckoutAttributes, store.Id);

        var excludeShippableAttributes = !await _shoppingCartService.ShoppingCartRequiresShippingAsync(cart);
        var attributes = await _checkoutAttributeService.GetAllAttributesAsync(_staticCacheManager, _storeMappingService, store.Id, excludeShippableAttributes);

        var xmlChanged = false;

        foreach (var attribute in attributes)
        {
            if (!attribute.IsRequired)
                continue;

            var conditionMet = await _checkoutAttributeParser.IsConditionMetAsync(attribute.ConditionAttributeXml, checkoutAttributesXml);
            if (conditionMet.HasValue && !conditionMet.Value)
                continue;

            var alreadySelected = _checkoutAttributeParser.ParseValues(checkoutAttributesXml, attribute.Id)
                .Any(value => !string.IsNullOrWhiteSpace(value));
            if (alreadySelected)
                continue;

            string autoValue = null;

            if (attribute.ShouldHaveValues)
            {
                //dropdown/radio list/checkboxes/color squares/image squares/read-only checkboxes - pick the
                //admin-configured default (pre-selected) value, or just the first one if none is marked default
                var values = await _checkoutAttributeService.GetAttributeValuesAsync(attribute.Id);
                var defaultValue = values.OrderByDescending(value => value.IsPreSelected).ThenBy(value => value.DisplayOrder).FirstOrDefault();
                if (defaultValue != null)
                    autoValue = defaultValue.Id.ToString();
            }
            else if (attribute.AttributeControlType == AttributeControlType.Datepicker)
            {
                autoValue = DateTime.UtcNow.ToString("D");
            }
            else if (attribute.AttributeControlType == AttributeControlType.TextBox || attribute.AttributeControlType == AttributeControlType.MultilineTextbox)
            {
                autoValue = !string.IsNullOrEmpty(attribute.DefaultValue) ? attribute.DefaultValue : "N/A";
                if (attribute.ValidationMinLength.HasValue && autoValue.Length < attribute.ValidationMinLength.Value)
                    autoValue = autoValue.PadRight(attribute.ValidationMinLength.Value, '.');
            }
            //FileUpload can't be auto-filled without a real uploaded file - leave it unselected, PlaceOrderAsync
            //will surface its own error for that (rare) case

            if (autoValue == null)
                continue;

            checkoutAttributesXml = _checkoutAttributeParser.AddAttribute(checkoutAttributesXml, attribute, autoValue);
            xmlChanged = true;
        }

        if (xmlChanged)
            await _genericAttributeService.SaveAttributeAsync(customer, NopCustomerDefaults.CheckoutAttributes, checkoutAttributesXml, store.Id);
    }
    private async Task<(object LineItem, decimal Amount)> AddPaymentMethodAdditionalFeeLineItemAsync(IList<ShoppingCartItem> cart, Currency currency)
    {
        var paymentMethodAdditionalFee = await _paymentService.GetAdditionalHandlingFeeAsync(cart, "Payments.NovalnetGooglePay");

        if (paymentMethodAdditionalFee <= 0)
            return (null, 0m);

        var lineItem = new
        {
            label = await _localizationService.GetResourceAsync("ShoppingCart.Totals.PaymentMethodAdditionalFee"),
            type = "LINE_ITEM",
            amount = await NovalnetHelper.GetConvertedAmountCents(paymentMethodAdditionalFee, _currencyService, currency)
        };

        return (lineItem, paymentMethodAdditionalFee);
    }

    private async Task<List<(string Identifier, string Label, string Detail, int Amount)>> GetPickupStoreShippingOptionsAsync(IList<ShoppingCartItem> cart,  Address shippingAddress,  Customer customer,  int storeId,  Currency currency)
    {
        var pickupOptions = new List<(string Identifier, string Label, string Detail, int Amount)>();

        if (!_shippingSettings.AllowPickupInStore)
            return pickupOptions;

        var pickupPointProviders =  await _pickupPluginManager.LoadActivePluginsAsync(customer, storeId);

        if (!pickupPointProviders.Any())
            return pickupOptions;

        var pickupPointsResponse = await _shippingService.GetPickupPointsAsync(cart, shippingAddress, customer, storeId: storeId);

        if (!pickupPointsResponse.Success || !pickupPointsResponse.PickupPoints.Any())
        {
            return pickupOptions;
        }

        foreach (var point in pickupPointsResponse.PickupPoints)
        {
            var optionName = await _localizationService.GetResourceAsync("Checkout.PickupPoints.NullName");

            var identifier = string.IsNullOrEmpty(point.Name) ? optionName : string.Format(await _localizationService.GetResourceAsync("Checkout.PickupPoints.Name"), point.Name);

            var (adjustedRate, _) = await _orderTotalCalculationService.AdjustShippingRateAsync( point.PickupFee, cart, true);

            var amount = await NovalnetHelper.GetConvertedAmountCents(adjustedRate, _currencyService, currency);

            pickupOptions.Add((identifier, optionName, point.Description, amount));
        }

        return pickupOptions;
    }


    #endregion
}

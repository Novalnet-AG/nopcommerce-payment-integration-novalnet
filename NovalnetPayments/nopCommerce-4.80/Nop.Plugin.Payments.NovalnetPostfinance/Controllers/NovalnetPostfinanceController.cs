using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.NovalnetPostfinance.Models;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Payments;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Nop.Plugin.Payments.NovalnetPostfinance.Controllers;


[AutoValidateAntiforgeryToken]
public class NovalnetPostfinanceController : BasePaymentController
{
    #region Fields

    protected readonly ILanguageService _languageService;
    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly IPermissionService _permissionService;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;
    private readonly IPaymentPluginManager _paymentPluginManager;
    private readonly IWebHelper _webHelper;
    private readonly IOrderService _orderService;
    private readonly IOrderProcessingService _orderProcessingService;

    #endregion

    #region Ctor

    public NovalnetPostfinanceController(ILocalizationService localizationService,
        INotificationService notificationService,
        IPermissionService permissionService,
        ISettingService settingService,
        IStoreContext storeContext,
        IWebHelper webHelper,
        IPaymentPluginManager paymentPluginManager,
        IOrderService orderService,
        IOrderProcessingService orderProcessingService,
        ILanguageService languageService)
    {
        _localizationService = localizationService;
        _notificationService = notificationService;
        _permissionService = permissionService;
        _settingService = settingService;
        _storeContext = storeContext;
        _webHelper = webHelper;
        _paymentPluginManager = paymentPluginManager;
        _orderService = orderService;
        _orderProcessingService = orderProcessingService;
        _languageService = languageService;
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
        var novalnetPostfinancePaymentSettings = await _settingService.LoadSettingAsync<NovalnetPostfinancePaymentSettings>(storeScope);

        var model = new ConfigurationModel
        {
            AdditionalFee = novalnetPostfinancePaymentSettings.AdditionalFee,
            AdditionalFeePercentage = novalnetPostfinancePaymentSettings.AdditionalFeePercentage,
            ActiveStoreScopeConfiguration = storeScope,
            TestMode = novalnetPostfinancePaymentSettings.TestMode,
            DescriptionText = novalnetPostfinancePaymentSettings.DescriptionText,
        };

        //locales
        await AddLocalesAsync(_languageService, model.Locales, async (locale, languageId) =>
        {
            locale.DescriptionText = await _localizationService
                .GetLocalizedSettingAsync(novalnetPostfinancePaymentSettings, x => x.DescriptionText, languageId, 0, false, false);
        });

        if (storeScope > 0)
        {
            model.AdditionalFee_OverrideForStore = await _settingService.SettingExistsAsync(novalnetPostfinancePaymentSettings, x => x.AdditionalFee, storeScope);
            model.AdditionalFeePercentage_OverrideForStore = await _settingService.SettingExistsAsync(novalnetPostfinancePaymentSettings, x => x.AdditionalFeePercentage, storeScope);
            model.TestMode_OverrideForStore = await _settingService.SettingExistsAsync(novalnetPostfinancePaymentSettings, x => x.TestMode,
            storeScope);
            model.DescriptionText_OverrideForStore = await _settingService.SettingExistsAsync(novalnetPostfinancePaymentSettings, x => x.DescriptionText, storeScope);
        }

        return View("~/Plugins/Payments.NovalnetPostfinance/Views/Configure.cshtml", model);
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
        var novalnetPostfinancePaymentSettings = await _settingService.LoadSettingAsync<NovalnetPostfinancePaymentSettings>(storeScope);

        //save settings
        novalnetPostfinancePaymentSettings.AdditionalFee = model.AdditionalFee;
        novalnetPostfinancePaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
        novalnetPostfinancePaymentSettings.TestMode = model.TestMode;
        novalnetPostfinancePaymentSettings.DescriptionText = model.DescriptionText;

        /* We do not clear cache after each setting update.
         * This behavior can increase performance because cached settings will not be cleared 
         * and loaded from database after each update */

        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetPostfinancePaymentSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetPostfinancePaymentSettings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetPostfinancePaymentSettings, x => x.TestMode, model.
        TestMode_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetPostfinancePaymentSettings, x => x.DescriptionText, model.DescriptionText_OverrideForStore, storeScope, false);

        //now clear settings cache
        await _settingService.ClearCacheAsync();

        //localization. no multi-store support for localization yet.
        foreach (var localized in model.Locales)
        {
            await _localizationService.SaveLocalizedSettingAsync(novalnetPostfinancePaymentSettings,
                x => x.DescriptionText, localized.LanguageId, localized.DescriptionText);
        }

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return RedirectToAction("Configure");
    }

    #endregion
}

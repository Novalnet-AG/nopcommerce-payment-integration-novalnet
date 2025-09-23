using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetDirectDebitACH.Models;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Plugin.Payments.NovalnetAPIConfiguration;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Nop.Plugin.Payments.NovalnetDirectDebitACH.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class NovalnetDirectDebitACHController : BasePaymentController
{
    #region Fields
    protected readonly ILanguageService _languageService;
    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly IPermissionService _permissionService;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;

    #endregion

    #region Ctor

    public NovalnetDirectDebitACHController(ILocalizationService localizationService,
        INotificationService notificationService,
        IPermissionService permissionService,
        ISettingService settingService,
        IStoreContext storeContext,
        ILanguageService languageService)
    {
        _localizationService = localizationService;
        _notificationService = notificationService;
        _permissionService = permissionService;
        _settingService = settingService;
        _storeContext = storeContext;
        _languageService = languageService;
    }

    #endregion

    #region Methods

    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure()
    {
        //load settings for a chosen store scope
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var novalnetDirectDebitACHPaymentSettings = await _settingService.LoadSettingAsync<NovalnetDirectDebitACHPaymentSettings>(storeScope);

        var model = new ConfigurationModel
        {
            AdditionalFee = novalnetDirectDebitACHPaymentSettings.AdditionalFee,
            AdditionalFeePercentage = novalnetDirectDebitACHPaymentSettings.AdditionalFeePercentage,
            ActiveStoreScopeConfiguration = storeScope,
            DescriptionText = novalnetDirectDebitACHPaymentSettings.DescriptionText,
            TestMode = novalnetDirectDebitACHPaymentSettings.TestMode,
        };

        //locales
        await AddLocalesAsync(_languageService, model.Locales, async (locale, languageId) =>
        {
            locale.DescriptionText = await _localizationService
                .GetLocalizedSettingAsync(novalnetDirectDebitACHPaymentSettings, x => x.DescriptionText, languageId, 0, false, false);
        });

        if (storeScope > 0)
        {
            model.DescriptionText_OverrideForStore = await _settingService.SettingExistsAsync(novalnetDirectDebitACHPaymentSettings, x => x.DescriptionText, storeScope);
            model.AdditionalFee_OverrideForStore = await _settingService.SettingExistsAsync(novalnetDirectDebitACHPaymentSettings, x => x.AdditionalFee, storeScope);
            model.AdditionalFeePercentage_OverrideForStore = await _settingService.SettingExistsAsync(novalnetDirectDebitACHPaymentSettings, x => x.AdditionalFeePercentage, storeScope);
            model.TestMode_OverrideForStore = await _settingService.SettingExistsAsync(novalnetDirectDebitACHPaymentSettings, x => x.TestMode,
            storeScope);
        }

        return View("~/Plugins/Payments.NovalnetDirectDebitACH/Views/Configure.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return await Configure();

        //load settings for a chosen store scope
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var novalnetDirectDebitACHPaymentSettings = await _settingService.LoadSettingAsync<NovalnetDirectDebitACHPaymentSettings>(storeScope);

        //save settings
        novalnetDirectDebitACHPaymentSettings.DescriptionText = model.DescriptionText;
        novalnetDirectDebitACHPaymentSettings.AdditionalFee = model.AdditionalFee;
        novalnetDirectDebitACHPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
        novalnetDirectDebitACHPaymentSettings.TestMode = model.TestMode;

        /* We do not clear cache after each setting update.
         * This behavior can increase performance because cached settings will not be cleared 
         * and loaded from database after each update */

        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetDirectDebitACHPaymentSettings, x => x.DescriptionText, model.DescriptionText_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetDirectDebitACHPaymentSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetDirectDebitACHPaymentSettings, x => x.AdditionalFeePercentage, model.
        AdditionalFeePercentage_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetDirectDebitACHPaymentSettings, x => x.TestMode, model.
        TestMode_OverrideForStore, storeScope, false);

        //now clear settings cache
        await _settingService.ClearCacheAsync();

        //localization. no multi-store support for localization yet.
        foreach (var localized in model.Locales)
        {
            await _localizationService.SaveLocalizedSettingAsync(novalnetDirectDebitACHPaymentSettings,
                x => x.DescriptionText, localized.LanguageId, localized.DescriptionText);
        }

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return RedirectToAction("Configure");
    }

    #endregion
}

using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetMultibanco.Models;
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

namespace Nop.Plugin.Payments.NovalnetMultibanco.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class NovalnetMultibancoController : BasePaymentController
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

    public NovalnetMultibancoController(ILocalizationService localizationService,
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
        var novalnetMultibancoPaymentSettings = await _settingService.LoadSettingAsync<NovalnetMultibancoPaymentSettings>(storeScope);

        var model = new ConfigurationModel
        {
            AdditionalFee = novalnetMultibancoPaymentSettings.AdditionalFee,
            AdditionalFeePercentage = novalnetMultibancoPaymentSettings.AdditionalFeePercentage,
            ActiveStoreScopeConfiguration = storeScope,
            DescriptionText = novalnetMultibancoPaymentSettings.DescriptionText,
            TestMode = novalnetMultibancoPaymentSettings.TestMode,
        };

        //locales
        await AddLocalesAsync(_languageService, model.Locales, async (locale, languageId) =>
        {
            locale.DescriptionText = await _localizationService
                .GetLocalizedSettingAsync(novalnetMultibancoPaymentSettings, x => x.DescriptionText, languageId, 0, false, false);
        });

        if (storeScope > 0)
        {
            model.AdditionalFee_OverrideForStore = await _settingService.SettingExistsAsync(novalnetMultibancoPaymentSettings, x => x.AdditionalFee, storeScope);
            model.AdditionalFeePercentage_OverrideForStore = await _settingService.SettingExistsAsync(novalnetMultibancoPaymentSettings, x => x.AdditionalFeePercentage, storeScope);
            model.TestMode_OverrideForStore = await _settingService.SettingExistsAsync(novalnetMultibancoPaymentSettings, x => x.TestMode,
            storeScope);
        }

        return View("~/Plugins/Payments.NovalnetMultibanco/Views/Configure.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return await Configure();

        //load settings for a chosen store scope
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var novalnetMultibancoPaymentSettings = await _settingService.LoadSettingAsync<NovalnetMultibancoPaymentSettings>(storeScope);

        //save settings
        novalnetMultibancoPaymentSettings.AdditionalFee = model.AdditionalFee;
        novalnetMultibancoPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
        novalnetMultibancoPaymentSettings.DescriptionText = model.DescriptionText;
        novalnetMultibancoPaymentSettings.TestMode = model.TestMode;

        /* We do not clear cache after each setting update.
         * This behavior can increase performance because cached settings will not be cleared 
         * and loaded from database after each update */

        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetMultibancoPaymentSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetMultibancoPaymentSettings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetMultibancoPaymentSettings, x => x.DescriptionText, model.DescriptionText_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetMultibancoPaymentSettings, x => x.TestMode, model.
        TestMode_OverrideForStore, storeScope, false);

        //now clear settings cache
        await _settingService.ClearCacheAsync();

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return RedirectToAction("Configure");
    }

    #endregion
}

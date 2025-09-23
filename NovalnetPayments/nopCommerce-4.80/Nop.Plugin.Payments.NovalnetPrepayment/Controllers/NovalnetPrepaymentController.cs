using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetPrepayment.Models;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.NovalnetPrepayment.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class NovalnetPrepaymentController : BasePaymentController
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

    public NovalnetPrepaymentController(ILocalizationService localizationService,
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
        var novalnetPrepaymentPaymentSettings = await _settingService.LoadSettingAsync<NovalnetPrepaymentPaymentSettings>(storeScope);

        var model = new ConfigurationModel
        {
            DescriptionText = novalnetPrepaymentPaymentSettings.DescriptionText,
            AdditionalFee = novalnetPrepaymentPaymentSettings.AdditionalFee,
            AdditionalFeePercentage = novalnetPrepaymentPaymentSettings.AdditionalFeePercentage,
            ActiveStoreScopeConfiguration = storeScope,
            DueDate = novalnetPrepaymentPaymentSettings.DueDate,
            TestMode = novalnetPrepaymentPaymentSettings.TestMode,
        };

        await AddLocalesAsync(_languageService, model.Locales, async (locale, languageId) =>
        {
            locale.DescriptionText = await _localizationService
                .GetLocalizedSettingAsync(novalnetPrepaymentPaymentSettings, x => x.DescriptionText, languageId, 0, false, false);
        });


        if (storeScope > 0)
        {
            model.AdditionalFee_OverrideForStore = await _settingService.SettingExistsAsync(novalnetPrepaymentPaymentSettings, x => x.AdditionalFee, storeScope);
            model.AdditionalFeePercentage_OverrideForStore = await _settingService.SettingExistsAsync(novalnetPrepaymentPaymentSettings, x => x.AdditionalFeePercentage, storeScope);
            model.TestMode_OverrideForStore = await _settingService.SettingExistsAsync(novalnetPrepaymentPaymentSettings, x => x.TestMode,
            storeScope);
            model.DueDate_OverrideForStore = await _settingService.SettingExistsAsync(novalnetPrepaymentPaymentSettings, x => x.DueDate,
            storeScope);
            model.DescriptionText_OverrideForStore = await _settingService.SettingExistsAsync(novalnetPrepaymentPaymentSettings, x => x.DescriptionText, storeScope);
        }

        return View("~/Plugins/Payments.NovalnetPrepayment/Views/Configure.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return await Configure();

        //load settings for a chosen store scope
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var novalnetPrepaymentPaymentSettings = await _settingService.LoadSettingAsync<NovalnetPrepaymentPaymentSettings>(storeScope);

        //save settings
        novalnetPrepaymentPaymentSettings.AdditionalFee = model.AdditionalFee;
        novalnetPrepaymentPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
        novalnetPrepaymentPaymentSettings.DescriptionText = model.DescriptionText;
        novalnetPrepaymentPaymentSettings.DueDate = model.DueDate??0;
        novalnetPrepaymentPaymentSettings.TestMode = model.TestMode;

        /* We do not clear cache after each setting update.
         * This behavior can increase performance because cached settings will not be cleared 
         * and loaded from database after each update */
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetPrepaymentPaymentSettings, x => x.DescriptionText, model.DescriptionText_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetPrepaymentPaymentSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetPrepaymentPaymentSettings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetPrepaymentPaymentSettings, x => x.TestMode, model.
        TestMode_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetPrepaymentPaymentSettings, x => x.DueDate, model.
        DueDate_OverrideForStore, storeScope, false);

        //now clear settings cache
        await _settingService.ClearCacheAsync();

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return RedirectToAction("Configure");
    }

    #endregion
}

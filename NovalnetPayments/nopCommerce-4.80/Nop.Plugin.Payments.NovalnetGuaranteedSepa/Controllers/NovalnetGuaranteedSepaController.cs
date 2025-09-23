using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetGuaranteedSepa.Models;
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

namespace Nop.Plugin.Payments.NovalnetGuaranteedSepa.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class NovalnetGuaranteedSepaController : BasePaymentController
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

    public NovalnetGuaranteedSepaController(ILocalizationService localizationService,
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
        var novalnetGuaranteedSepaPaymentSettings = await _settingService.LoadSettingAsync<NovalnetGuaranteedSepaPaymentSettings>(storeScope);

        var model = new ConfigurationModel
        {
            AdditionalFee = novalnetGuaranteedSepaPaymentSettings.AdditionalFee,
            AdditionalFeePercentage = novalnetGuaranteedSepaPaymentSettings.AdditionalFeePercentage,
            ActiveStoreScopeConfiguration = storeScope,
            DescriptionText = novalnetGuaranteedSepaPaymentSettings.DescriptionText,
            TestMode = novalnetGuaranteedSepaPaymentSettings.TestMode,
            AllowB2B = novalnetGuaranteedSepaPaymentSettings.AllowB2B,
            OnholdMinAmount = novalnetGuaranteedSepaPaymentSettings.OnholdMinAmount,
            GuaranteedSepaMinAmount = novalnetGuaranteedSepaPaymentSettings.GuaranteedSepaMinAmount
        };

        //locales
        await AddLocalesAsync(_languageService, model.Locales, async (locale, languageId) =>
        {
            locale.DescriptionText = await _localizationService
                .GetLocalizedSettingAsync(novalnetGuaranteedSepaPaymentSettings, x => x.DescriptionText, languageId, 0, false, false);
        });

        model.AvailablePaymentActions = new List<SelectListItem>
        {
            new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.Authorize"), Value = "1" },
            new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.Capture"), Value = "0" }
        };
        model.PaymentActionString = ((int)novalnetGuaranteedSepaPaymentSettings.PaymentAction).ToString();

        if (storeScope > 0)
        {
            model.DescriptionText_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGuaranteedSepaPaymentSettings, x => x.DescriptionText, storeScope);
            model.AdditionalFee_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGuaranteedSepaPaymentSettings, x => x.AdditionalFee, storeScope);
            model.AdditionalFeePercentage_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGuaranteedSepaPaymentSettings, x => x.AdditionalFeePercentage, storeScope);
            model.PaymentAction_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGuaranteedSepaPaymentSettings, x => x.PaymentAction,
            storeScope);
            model.TestMode_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGuaranteedSepaPaymentSettings, x => x.TestMode,
            storeScope);
            model.AllowB2B_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGuaranteedSepaPaymentSettings, x => x.AllowB2B,
            storeScope);
            model.OnholdMinAmount_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGuaranteedSepaPaymentSettings, x => x.OnholdMinAmount, storeScope);
            model.GuaranteedSepaMinAmount_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGuaranteedSepaPaymentSettings, x => x.GuaranteedSepaMinAmount, storeScope);
        }

        return View("~/Plugins/Payments.NovalnetGuaranteedSepa/Views/Configure.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return await Configure();

        //load settings for a chosen store scope
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var novalnetGuaranteedSepaPaymentSettings = await _settingService.LoadSettingAsync<NovalnetGuaranteedSepaPaymentSettings>(storeScope);


        if (int.TryParse(model.PaymentActionString, out var parsedAction))
        {
            novalnetGuaranteedSepaPaymentSettings.PaymentAction = (PaymentAction)parsedAction;
        }

        //save settings
        novalnetGuaranteedSepaPaymentSettings.DescriptionText = model.DescriptionText;
        novalnetGuaranteedSepaPaymentSettings.AdditionalFee = model.AdditionalFee;
        novalnetGuaranteedSepaPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
        novalnetGuaranteedSepaPaymentSettings.TestMode = model.TestMode;
        novalnetGuaranteedSepaPaymentSettings.AllowB2B = model.AllowB2B;
        novalnetGuaranteedSepaPaymentSettings.OnholdMinAmount = model.OnholdMinAmount??0;
        novalnetGuaranteedSepaPaymentSettings.GuaranteedSepaMinAmount = model.GuaranteedSepaMinAmount??0;

        /* We do not clear cache after each setting update.
         * This behavior can increase performance because cached settings will not be cleared 
         * and loaded from database after each update */

        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGuaranteedSepaPaymentSettings, x => x.DescriptionText, model.DescriptionText_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGuaranteedSepaPaymentSettings, x => x.PaymentAction, model.PaymentAction_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGuaranteedSepaPaymentSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGuaranteedSepaPaymentSettings, x => x.AdditionalFeePercentage, model.
        AdditionalFeePercentage_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGuaranteedSepaPaymentSettings, x => x.TestMode, model.
        TestMode_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGuaranteedSepaPaymentSettings, x => x.AllowB2B, model.
        AllowB2B_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGuaranteedSepaPaymentSettings, x => x.OnholdMinAmount, model.
        OnholdMinAmount_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGuaranteedSepaPaymentSettings, x => x.GuaranteedSepaMinAmount, model.
        GuaranteedSepaMinAmount_OverrideForStore, storeScope, false);

        //now clear settings cache
        await _settingService.ClearCacheAsync();

        //localization. no multi-store support for localization yet.
        foreach (var localized in model.Locales)
        {
            await _localizationService.SaveLocalizedSettingAsync(novalnetGuaranteedSepaPaymentSettings,
                x => x.DescriptionText, localized.LanguageId, localized.DescriptionText);
        }

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return RedirectToAction("Configure");
    }

    #endregion
}

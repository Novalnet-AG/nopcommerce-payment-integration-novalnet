using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetGuaranteedInvoice.Models;
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

namespace Nop.Plugin.Payments.NovalnetGuaranteedInvoice.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class NovalnetGuaranteedInvoiceController : BasePaymentController
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

    public NovalnetGuaranteedInvoiceController(ILocalizationService localizationService,
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
        var novalnetGuaranteedInvoicePaymentSettings = await _settingService.LoadSettingAsync<NovalnetGuaranteedInvoicePaymentSettings>(storeScope);

        var model = new ConfigurationModel
        {
            AdditionalFee = novalnetGuaranteedInvoicePaymentSettings.AdditionalFee,
            AdditionalFeePercentage = novalnetGuaranteedInvoicePaymentSettings.AdditionalFeePercentage,
            ActiveStoreScopeConfiguration = storeScope,
            DescriptionText = novalnetGuaranteedInvoicePaymentSettings.DescriptionText,
            TestMode = novalnetGuaranteedInvoicePaymentSettings.TestMode,
            AllowB2B = novalnetGuaranteedInvoicePaymentSettings.AllowB2B,
            OnholdMinAmount = novalnetGuaranteedInvoicePaymentSettings.OnholdMinAmount,
            GuaranteedInvoiceMinAmount = novalnetGuaranteedInvoicePaymentSettings.GuaranteedInvoiceMinAmount
        };

        //locales
        await AddLocalesAsync(_languageService, model.Locales, async (locale, languageId) =>
        {
            locale.DescriptionText = await _localizationService
                .GetLocalizedSettingAsync(novalnetGuaranteedInvoicePaymentSettings, x => x.DescriptionText, languageId, 0, false, false);
        });

        model.AvailablePaymentActions = new List<SelectListItem>
        {
            new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.Authorize"), Value = "1" },
            new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.Capture"), Value = "0" }
        };
        model.PaymentActionString = ((int)novalnetGuaranteedInvoicePaymentSettings.PaymentAction).ToString();

        if (storeScope > 0)
        {
            model.DescriptionText_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGuaranteedInvoicePaymentSettings, x => x.DescriptionText, storeScope);
            model.AdditionalFee_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGuaranteedInvoicePaymentSettings, x => x.AdditionalFee, storeScope);
            model.AdditionalFeePercentage_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGuaranteedInvoicePaymentSettings, x => x.AdditionalFeePercentage, storeScope);
            model.PaymentAction_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGuaranteedInvoicePaymentSettings, x => x.PaymentAction,
            storeScope);
            model.TestMode_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGuaranteedInvoicePaymentSettings, x => x.TestMode,
            storeScope);
            model.AllowB2B_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGuaranteedInvoicePaymentSettings, x => x.AllowB2B,
            storeScope);
            model.OnholdMinAmount_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGuaranteedInvoicePaymentSettings, x => x.OnholdMinAmount, storeScope);
            model.GuaranteedInvoiceMinAmount_OverrideForStore = await _settingService.SettingExistsAsync(novalnetGuaranteedInvoicePaymentSettings, x => x.GuaranteedInvoiceMinAmount, storeScope);
        }

        return View("~/Plugins/Payments.NovalnetGuaranteedInvoice/Views/Configure.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return await Configure();

        //load settings for a chosen store scope
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var novalnetGuaranteedInvoicePaymentSettings = await _settingService.LoadSettingAsync<NovalnetGuaranteedInvoicePaymentSettings>(storeScope);


        if (int.TryParse(model.PaymentActionString, out var parsedAction))
        {
            novalnetGuaranteedInvoicePaymentSettings.PaymentAction = (PaymentAction)parsedAction;
        }

        //save settings
        novalnetGuaranteedInvoicePaymentSettings.DescriptionText = model.DescriptionText;
        novalnetGuaranteedInvoicePaymentSettings.AdditionalFee = model.AdditionalFee;
        novalnetGuaranteedInvoicePaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
        novalnetGuaranteedInvoicePaymentSettings.TestMode = model.TestMode;
        novalnetGuaranteedInvoicePaymentSettings.AllowB2B = model.AllowB2B;
        novalnetGuaranteedInvoicePaymentSettings.OnholdMinAmount = model.OnholdMinAmount??0;
        novalnetGuaranteedInvoicePaymentSettings.GuaranteedInvoiceMinAmount = model.GuaranteedInvoiceMinAmount??0;

        /* We do not clear cache after each setting update.
         * This behavior can increase performance because cached settings will not be cleared 
         * and loaded from database after each update */

        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGuaranteedInvoicePaymentSettings, x => x.DescriptionText, model.DescriptionText_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGuaranteedInvoicePaymentSettings, x => x.PaymentAction, model.PaymentAction_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGuaranteedInvoicePaymentSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGuaranteedInvoicePaymentSettings, x => x.AdditionalFeePercentage, model.
        AdditionalFeePercentage_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGuaranteedInvoicePaymentSettings, x => x.TestMode, model.
        TestMode_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGuaranteedInvoicePaymentSettings, x => x.AllowB2B, model.
        AllowB2B_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGuaranteedInvoicePaymentSettings, x => x.OnholdMinAmount, model.
        OnholdMinAmount_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetGuaranteedInvoicePaymentSettings, x => x.GuaranteedInvoiceMinAmount, model.
        GuaranteedInvoiceMinAmount_OverrideForStore, storeScope, false);

        //now clear settings cache
        await _settingService.ClearCacheAsync();

        //localization. no multi-store support for localization yet.
        foreach (var localized in model.Locales)
        {
            await _localizationService.SaveLocalizedSettingAsync(novalnetGuaranteedInvoicePaymentSettings,
                x => x.DescriptionText, localized.LanguageId, localized.DescriptionText);
        }

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return RedirectToAction("Configure");
    }

    #endregion
}

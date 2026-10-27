using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetInvoice.Models;
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

namespace Nop.Plugin.Payments.NovalnetInvoice.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class NovalnetInvoiceController : BasePaymentController
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

    public NovalnetInvoiceController(ILocalizationService localizationService,
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
        var novalnetInvoicePaymentSettings = await _settingService.LoadSettingAsync<NovalnetInvoicePaymentSettings>(storeScope);

        var model = new ConfigurationModel
        {
            AdditionalFee = novalnetInvoicePaymentSettings.AdditionalFee,
            AdditionalFeePercentage = novalnetInvoicePaymentSettings.AdditionalFeePercentage,
            ActiveStoreScopeConfiguration = storeScope,
            DescriptionText = novalnetInvoicePaymentSettings.DescriptionText,
            TestMode = novalnetInvoicePaymentSettings.TestMode,
            OnholdMinAmount = novalnetInvoicePaymentSettings.OnholdMinAmount,
            DueDate = novalnetInvoicePaymentSettings.DueDate,
        };

        //locales
        await AddLocalesAsync(_languageService, model.Locales, async (locale, languageId) =>
        {
            locale.DescriptionText = await _localizationService
                .GetLocalizedSettingAsync(novalnetInvoicePaymentSettings, x => x.DescriptionText, languageId, 0, false, false);
        });

        model.AvailablePaymentActions = new List<SelectListItem>
        {
            new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.Authorize"), Value = "1" },
            new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.Capture"), Value = "0" }
        };
        model.PaymentActionString = ((int)novalnetInvoicePaymentSettings.PaymentAction).ToString();

        if (storeScope > 0)
        {
            model.AdditionalFee_OverrideForStore = await _settingService.SettingExistsAsync(novalnetInvoicePaymentSettings, x => x.AdditionalFee, storeScope);
            model.AdditionalFeePercentage_OverrideForStore = await _settingService.SettingExistsAsync(novalnetInvoicePaymentSettings, x => x.AdditionalFeePercentage, storeScope);
            model.PaymentAction_OverrideForStore = await _settingService.SettingExistsAsync(novalnetInvoicePaymentSettings, x => x.PaymentAction,
            storeScope);
            model.TestMode_OverrideForStore = await _settingService.SettingExistsAsync(novalnetInvoicePaymentSettings, x => x.TestMode,
            storeScope);
            model.OnholdMinAmount_OverrideForStore = await _settingService.SettingExistsAsync(novalnetInvoicePaymentSettings, x => x.OnholdMinAmount,
            storeScope);
            model.DueDate_OverrideForStore = await _settingService.SettingExistsAsync(novalnetInvoicePaymentSettings, x => x.DueDate,
            storeScope);
        }

        return View("~/Plugins/Payments.NovalnetInvoice/Views/Configure.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return await Configure();

        //load settings for a chosen store scope
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var novalnetInvoicePaymentSettings = await _settingService.LoadSettingAsync<NovalnetInvoicePaymentSettings>(storeScope);

        if (int.TryParse(model.PaymentActionString, out var parsedAction))
        {
            novalnetInvoicePaymentSettings.PaymentAction = (PaymentAction)parsedAction;
        }

        //save settings
        novalnetInvoicePaymentSettings.AdditionalFee = model.AdditionalFee;
        novalnetInvoicePaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
        novalnetInvoicePaymentSettings.DescriptionText = model.DescriptionText;
        novalnetInvoicePaymentSettings.TestMode = model.TestMode;
        novalnetInvoicePaymentSettings.OnholdMinAmount = model.OnholdMinAmount??0;
        novalnetInvoicePaymentSettings.DueDate = model.DueDate??0;

        /* We do not clear cache after each setting update.
         * This behavior can increase performance because cached settings will not be cleared 
         * and loaded from database after each update */

        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetInvoicePaymentSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetInvoicePaymentSettings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetInvoicePaymentSettings, x => x.DescriptionText, model.DescriptionText_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetInvoicePaymentSettings, x => x.PaymentAction, model.PaymentAction_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetInvoicePaymentSettings, x => x.TestMode, model.
        TestMode_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetInvoicePaymentSettings, x => x.OnholdMinAmount, model.OnholdMinAmount_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetInvoicePaymentSettings, x => x.DueDate, model.
        DueDate_OverrideForStore, storeScope, false);

        //now clear settings cache
        await _settingService.ClearCacheAsync();

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return RedirectToAction("Configure");
    }

    #endregion
}

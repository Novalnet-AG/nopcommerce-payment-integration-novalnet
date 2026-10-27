using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetAPIConfiguration.Models;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.NovalnetAPIConfiguration.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class NovalnetAPIConfigurationController : BasePaymentController
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly IPermissionService _permissionService;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;

    #endregion

    #region Ctor

    public NovalnetAPIConfigurationController(ILocalizationService localizationService,
        INotificationService notificationService,
        IPermissionService permissionService,
        ISettingService settingService,
        IStoreContext storeContext)
    {
        _localizationService = localizationService;
        _notificationService = notificationService;
        _permissionService = permissionService;
        _settingService = settingService;
        _storeContext = storeContext;
    }

    #endregion

    #region Methods

    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure()
    {
        //load settings for a chosen store scope
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var novalnetAPIConfigurationPaymentSettings = await _settingService.LoadSettingAsync<NovalnetAPIConfigurationPaymentSettings>(storeScope);

        var model = new ConfigurationModel
        {
            ProductActivationKey = novalnetAPIConfigurationPaymentSettings.ProductActivationKey,
            PaymentAccessKey = novalnetAPIConfigurationPaymentSettings.PaymentAccessKey,
            ClientKey = novalnetAPIConfigurationPaymentSettings.ClientKey,
            TariffID = novalnetAPIConfigurationPaymentSettings.TariffID,
            WebhookTestMode = novalnetAPIConfigurationPaymentSettings.WebhookTestMode,
            WebhookEmailTo = novalnetAPIConfigurationPaymentSettings.WebhookEmailTo,
            WebhookUrl = novalnetAPIConfigurationPaymentSettings.WebhookUrl,
            ActiveStoreScopeConfiguration = storeScope
        };
        if (storeScope > 0)
        {
            model.ProductActivationKey_OverrideForStore = await _settingService.SettingExistsAsync(novalnetAPIConfigurationPaymentSettings, x => x.ProductActivationKey, storeScope);
            model.PaymentAccessKey_OverrideForStore = await _settingService.SettingExistsAsync(novalnetAPIConfigurationPaymentSettings, x => x.PaymentAccessKey, storeScope);
            model.ClientKey_OverrideForStore = await _settingService.SettingExistsAsync(novalnetAPIConfigurationPaymentSettings, x => x.ClientKey, storeScope);
            model.TariffID_OverrideForStore = await _settingService.SettingExistsAsync(novalnetAPIConfigurationPaymentSettings, x => x.TariffID, storeScope);
            model.WebhookTestMode_OverrideForStore = await _settingService.SettingExistsAsync(novalnetAPIConfigurationPaymentSettings, x => x.WebhookTestMode, storeScope);
            model.WebhookEmailTo_OverrideForStore = await _settingService.SettingExistsAsync(novalnetAPIConfigurationPaymentSettings, x => x.WebhookEmailTo, storeScope);
            model.WebhookUrl_OverrideForStore = await _settingService.SettingExistsAsync(novalnetAPIConfigurationPaymentSettings, x => x.WebhookUrl, storeScope);
        }

        return View("~/Plugins/Payments.NovalnetAPIConfiguration/Views/Configure.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return await Configure();

        //load settings for a chosen store scope
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var novalnetAPIConfigurationPaymentSettings = await _settingService.LoadSettingAsync<NovalnetAPIConfigurationPaymentSettings>(storeScope);

        //save settings
        novalnetAPIConfigurationPaymentSettings.ProductActivationKey = model.ProductActivationKey;
        novalnetAPIConfigurationPaymentSettings.PaymentAccessKey = model.PaymentAccessKey;
        novalnetAPIConfigurationPaymentSettings.ClientKey = model.ClientKey;
        novalnetAPIConfigurationPaymentSettings.TariffID = model.TariffID;
        novalnetAPIConfigurationPaymentSettings.WebhookTestMode = model.WebhookTestMode;
        novalnetAPIConfigurationPaymentSettings.WebhookEmailTo = model.WebhookEmailTo;
        novalnetAPIConfigurationPaymentSettings.WebhookUrl = model.WebhookUrl;

        /* We do not clear cache after each setting update.
         * This behavior can increase performance because cached settings will not be cleared 
         * and loaded from database after each update */

        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetAPIConfigurationPaymentSettings, x => x.ProductActivationKey, model.ProductActivationKey_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetAPIConfigurationPaymentSettings, x => x.PaymentAccessKey, model.PaymentAccessKey_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetAPIConfigurationPaymentSettings, x => x.ClientKey, model.ClientKey_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetAPIConfigurationPaymentSettings, x => x.TariffID, model.TariffID_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetAPIConfigurationPaymentSettings, x => x.WebhookTestMode, model.WebhookTestMode_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetAPIConfigurationPaymentSettings, x => x.WebhookEmailTo, model.WebhookEmailTo_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetAPIConfigurationPaymentSettings, x => x.WebhookUrl, model.WebhookUrl_OverrideForStore, storeScope, false);

        //now clear settings cache
        await _settingService.ClearCacheAsync();

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"), true);

        return RedirectToAction("Configure");
    }

    #endregion
}

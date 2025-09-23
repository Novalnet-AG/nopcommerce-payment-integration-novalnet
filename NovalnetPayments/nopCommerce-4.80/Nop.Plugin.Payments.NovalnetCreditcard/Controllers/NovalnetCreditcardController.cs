using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.NovalnetCreditcard.Models;
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
using Nop.Plugin.Payments.NovalnetAPIConfiguration;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Nop.Plugin.Payments.NovalnetCreditcard.Controllers;


[AutoValidateAntiforgeryToken]
public class NovalnetCreditcardController : BasePaymentController
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

    public NovalnetCreditcardController(ILocalizationService localizationService,
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
        var novalnetCreditcardPaymentSettings = await _settingService.LoadSettingAsync<NovalnetCreditcardPaymentSettings>(storeScope);

        var model = new ConfigurationModel
        {
            AdditionalFee = novalnetCreditcardPaymentSettings.AdditionalFee,
            AdditionalFeePercentage = novalnetCreditcardPaymentSettings.AdditionalFeePercentage,
            ActiveStoreScopeConfiguration = storeScope,
            DescriptionText = novalnetCreditcardPaymentSettings.DescriptionText,
            TestMode = novalnetCreditcardPaymentSettings.TestMode,
            OnholdMinAmount = novalnetCreditcardPaymentSettings.OnholdMinAmount,
            Enforce3d = novalnetCreditcardPaymentSettings.Enforce3d,
            InlineForm = novalnetCreditcardPaymentSettings.InlineForm,
            LabelStyle = novalnetCreditcardPaymentSettings.LabelStyle,
            InputStyle = novalnetCreditcardPaymentSettings.InputStyle,
            TextStyle = novalnetCreditcardPaymentSettings.TextStyle,
        };

                //locales
        await AddLocalesAsync(_languageService, model.Locales, async (locale, languageId) =>
        {
            locale.DescriptionText = await _localizationService
                .GetLocalizedSettingAsync(novalnetCreditcardPaymentSettings, x => x.DescriptionText, languageId, 0, false, false);
        });

        model.AvailablePaymentActions = new List<SelectListItem>
        {
            new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.Authorize"), Value = "1" },
            new SelectListItem { Text = await _localizationService.GetResourceAsync("Plugins.Payments.NovalnetAPIConfiguration.Capture"), Value = "0" }
        };
        model.PaymentActionString = ((int)novalnetCreditcardPaymentSettings.PaymentAction).ToString();


        if (storeScope > 0)
        {
            model.AdditionalFee_OverrideForStore = await _settingService.SettingExistsAsync(novalnetCreditcardPaymentSettings, x => x.AdditionalFee, storeScope);
            model.AdditionalFeePercentage_OverrideForStore = await _settingService.SettingExistsAsync(novalnetCreditcardPaymentSettings, x => x.AdditionalFeePercentage, storeScope);
            model.DescriptionText_OverrideForStore = await _settingService.SettingExistsAsync(novalnetCreditcardPaymentSettings, x => x.DescriptionText, storeScope);
            model.PaymentAction_OverrideForStore = await _settingService.SettingExistsAsync(novalnetCreditcardPaymentSettings, x => x.PaymentAction,
            storeScope);
            model.TestMode_OverrideForStore = await _settingService.SettingExistsAsync(novalnetCreditcardPaymentSettings, x => x.TestMode,
            storeScope);
            model.OnholdMinAmount_OverrideForStore = await _settingService.SettingExistsAsync(novalnetCreditcardPaymentSettings, x => x.OnholdMinAmount,
            storeScope);
            model.Enforce3d_OverrideForStore = await _settingService.SettingExistsAsync(novalnetCreditcardPaymentSettings, x => x.Enforce3d,
            storeScope);
            model.InlineForm_OverrideForStore = await _settingService.SettingExistsAsync(novalnetCreditcardPaymentSettings, x => x.InlineForm,
            storeScope);
            model.LabelStyle_OverrideForStore = await _settingService.SettingExistsAsync(novalnetCreditcardPaymentSettings, x => x.LabelStyle,
            storeScope);
            model.InputStyle_OverrideForStore = await _settingService.SettingExistsAsync(novalnetCreditcardPaymentSettings, x => x.InputStyle,
            storeScope);
            model.TextStyle_OverrideForStore = await _settingService.SettingExistsAsync(novalnetCreditcardPaymentSettings, x => x.TextStyle,
            storeScope);
        }

        return View("~/Plugins/Payments.NovalnetCreditcard/Views/Configure.cshtml", model);
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
        var novalnetCreditcardPaymentSettings = await _settingService.LoadSettingAsync<NovalnetCreditcardPaymentSettings>(storeScope);

        if (int.TryParse(model.PaymentActionString, out var parsedAction))
        {
            novalnetCreditcardPaymentSettings.PaymentAction = (PaymentAction)parsedAction;
        }

        //save settings
        novalnetCreditcardPaymentSettings.AdditionalFee = model.AdditionalFee;
        novalnetCreditcardPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
        novalnetCreditcardPaymentSettings.TestMode = model.TestMode;
        novalnetCreditcardPaymentSettings.OnholdMinAmount = model.OnholdMinAmount??0;
        novalnetCreditcardPaymentSettings.DescriptionText = model.DescriptionText;
        novalnetCreditcardPaymentSettings.Enforce3d = model.Enforce3d;
        novalnetCreditcardPaymentSettings.InlineForm = model.InlineForm;
        novalnetCreditcardPaymentSettings.LabelStyle = model.LabelStyle;
        novalnetCreditcardPaymentSettings.InputStyle = model.InputStyle;
        novalnetCreditcardPaymentSettings.TextStyle = model.TextStyle;

        /* We do not clear cache after each setting update.
         * This behavior can increase performance because cached settings will not be cleared 
         * and loaded from database after each update */

        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetCreditcardPaymentSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetCreditcardPaymentSettings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetCreditcardPaymentSettings, x => x.DescriptionText, model.DescriptionText_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetCreditcardPaymentSettings, x => x.TestMode, model.TestMode_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetCreditcardPaymentSettings, x => x.OnholdMinAmount, model.OnholdMinAmount_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetCreditcardPaymentSettings, x => x.Enforce3d, model.Enforce3d_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetCreditcardPaymentSettings, x => x.InlineForm, model.InlineForm_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetCreditcardPaymentSettings, x => x.LabelStyle, model.LabelStyle_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetCreditcardPaymentSettings, x => x.InputStyle, model.InputStyle_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetCreditcardPaymentSettings, x => x.TextStyle, model.TextStyle_OverrideForStore, 
        storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(novalnetCreditcardPaymentSettings, x => x.PaymentAction, model.PaymentAction_OverrideForStore, storeScope, false);


        //now clear settings cache
        await _settingService.ClearCacheAsync();

        //localization. no multi-store support for localization yet.
        foreach (var localized in model.Locales)
        {
            await _localizationService.SaveLocalizedSettingAsync(novalnetCreditcardPaymentSettings,
                x => x.DescriptionText, localized.LanguageId, localized.DescriptionText);
        }

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return RedirectToAction("Configure");
    }

    #endregion
}

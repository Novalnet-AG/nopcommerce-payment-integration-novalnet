using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetBancontact.Models;
using Nop.Web.Framework.Components;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.NovalnetBancontact.Components;

public class NovalnetBancontactViewComponent : NopViewComponent
{
    protected readonly NovalnetBancontactPaymentSettings _novalnetBancontactPaymentSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;

    public NovalnetBancontactViewComponent(NovalnetBancontactPaymentSettings novalnetBancontactPaymentSettings,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _novalnetBancontactPaymentSettings = novalnetBancontactPaymentSettings;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        var model = new PaymentInfoModel()
        {
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetBancontactPaymentSettings,
                x => x.DescriptionText, (await _workContext.GetWorkingLanguageAsync()).Id, store.Id),
			TestMode = _novalnetBancontactPaymentSettings.TestMode
        };



        return View("~/Plugins/Payments.NovalnetBancontact/Views/PaymentInfo.cshtml", model);
    }
}

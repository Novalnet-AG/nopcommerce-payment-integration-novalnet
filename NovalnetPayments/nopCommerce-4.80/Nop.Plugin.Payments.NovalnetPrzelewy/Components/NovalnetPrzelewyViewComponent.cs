using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetPrzelewy.Models;
using Nop.Web.Framework.Components;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.NovalnetPrzelewy.Components;

public class NovalnetPrzelewyViewComponent : NopViewComponent
{
    protected readonly NovalnetPrzelewyPaymentSettings _novalnetPrzelewyPaymentSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;

    public NovalnetPrzelewyViewComponent(NovalnetPrzelewyPaymentSettings novalnetPrzelewyPaymentSettings,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _novalnetPrzelewyPaymentSettings = novalnetPrzelewyPaymentSettings;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        var model = new PaymentInfoModel()
        {
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetPrzelewyPaymentSettings,
                x => x.DescriptionText, (await _workContext.GetWorkingLanguageAsync()).Id, store.Id),
            TestMode = _novalnetPrzelewyPaymentSettings.TestMode
        };

        return View("~/Plugins/Payments.NovalnetPrzelewy/Views/PaymentInfo.cshtml", model);
    }
}

using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetEps.Models;
using Nop.Web.Framework.Components;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.NovalnetEps.Components;

public class NovalnetEpsViewComponent : NopViewComponent
{
    protected readonly NovalnetEpsPaymentSettings _novalnetEpsPaymentSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;

    public NovalnetEpsViewComponent(NovalnetEpsPaymentSettings novalnetEpsPaymentSettings,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _novalnetEpsPaymentSettings = novalnetEpsPaymentSettings;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        var model = new PaymentInfoModel()
        {
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetEpsPaymentSettings,
                x => x.DescriptionText, (await _workContext.GetWorkingLanguageAsync()).Id, store.Id),
            TestMode = _novalnetEpsPaymentSettings.TestMode
        };



        return View("~/Plugins/Payments.NovalnetEps/Views/PaymentInfo.cshtml", model);
    }
}

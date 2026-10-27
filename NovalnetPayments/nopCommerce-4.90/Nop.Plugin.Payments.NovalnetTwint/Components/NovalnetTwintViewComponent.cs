using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetTwint.Models;
using Nop.Web.Framework.Components;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.NovalnetTwint.Components;

public class NovalnetTwintViewComponent : NopViewComponent
{
    protected readonly NovalnetTwintPaymentSettings _novalnetTwintPaymentSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;

    public NovalnetTwintViewComponent(NovalnetTwintPaymentSettings novalnetTwintPaymentSettings,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _novalnetTwintPaymentSettings = novalnetTwintPaymentSettings;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        var model = new PaymentInfoModel()
        {
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetTwintPaymentSettings,
                x => x.DescriptionText, (await _workContext.GetWorkingLanguageAsync()).Id, store.Id),
            TestMode = _novalnetTwintPaymentSettings.TestMode
        };



        return View("~/Plugins/Payments.NovalnetTwint/Views/PaymentInfo.cshtml", model);
    }
}

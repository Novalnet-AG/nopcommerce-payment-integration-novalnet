using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetPostfinance.Models;
using Nop.Web.Framework.Components;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.NovalnetPostfinance.Components;

public class NovalnetPostfinanceViewComponent : NopViewComponent
{
    protected readonly NovalnetPostfinancePaymentSettings _novalnetPostfinancePaymentSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;

    public NovalnetPostfinanceViewComponent(NovalnetPostfinancePaymentSettings novalnetPostfinancePaymentSettings,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _novalnetPostfinancePaymentSettings = novalnetPostfinancePaymentSettings;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        var model = new PaymentInfoModel()
        {
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetPostfinancePaymentSettings,
                x => x.DescriptionText, (await _workContext.GetWorkingLanguageAsync()).Id, store.Id),
			TestMode = _novalnetPostfinancePaymentSettings.TestMode
        };

        return View("~/Plugins/Payments.NovalnetPostfinance/Views/PaymentInfo.cshtml", model);
    }
}

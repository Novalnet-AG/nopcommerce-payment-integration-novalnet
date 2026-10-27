using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetTrustly.Models;
using Nop.Web.Framework.Components;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.NovalnetTrustly.Components;

public class NovalnetTrustlyViewComponent : NopViewComponent
{
    protected readonly NovalnetTrustlyPaymentSettings _novalnetTrustlyPaymentSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;

    public NovalnetTrustlyViewComponent(NovalnetTrustlyPaymentSettings novalnetTrustlyPaymentSettings,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _novalnetTrustlyPaymentSettings = novalnetTrustlyPaymentSettings;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        var model = new PaymentInfoModel()
        {
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetTrustlyPaymentSettings,
                x => x.DescriptionText, (await _workContext.GetWorkingLanguageAsync()).Id, store.Id),
			TestMode = _novalnetTrustlyPaymentSettings.TestMode
        };

        return View("~/Plugins/Payments.NovalnetTrustly/Views/PaymentInfo.cshtml", model);
    }
}

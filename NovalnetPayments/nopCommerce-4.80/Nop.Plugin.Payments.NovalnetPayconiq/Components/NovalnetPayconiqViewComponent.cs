using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetPayconiq.Models;
using Nop.Web.Framework.Components;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.NovalnetPayconiq.Components;

public class NovalnetPayconiqViewComponent : NopViewComponent
{
    protected readonly NovalnetPayconiqPaymentSettings _novalnetPayconiqPaymentSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;

    public NovalnetPayconiqViewComponent(NovalnetPayconiqPaymentSettings novalnetPayconiqPaymentSettings,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _novalnetPayconiqPaymentSettings = novalnetPayconiqPaymentSettings;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        var model = new PaymentInfoModel()
        {
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetPayconiqPaymentSettings,
                x => x.DescriptionText, (await _workContext.GetWorkingLanguageAsync()).Id, store.Id),
			TestMode = _novalnetPayconiqPaymentSettings.TestMode
        };

        return View("~/Plugins/Payments.NovalnetPayconiq/Views/PaymentInfo.cshtml", model);
    }
}

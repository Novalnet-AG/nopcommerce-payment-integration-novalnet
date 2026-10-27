using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetPostfinancecard.Models;
using Nop.Web.Framework.Components;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.NovalnetPostfinancecard.Components;

public class NovalnetPostfinancecardViewComponent : NopViewComponent
{
    protected readonly NovalnetPostfinancecardPaymentSettings _novalnetPostfinancecardPaymentSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;

    public NovalnetPostfinancecardViewComponent(NovalnetPostfinancecardPaymentSettings novalnetPostfinancecardPaymentSettings,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _novalnetPostfinancecardPaymentSettings = novalnetPostfinancecardPaymentSettings;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        var model = new PaymentInfoModel()
        {
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetPostfinancecardPaymentSettings,
                x => x.DescriptionText, (await _workContext.GetWorkingLanguageAsync()).Id, store.Id),
			TestMode = _novalnetPostfinancecardPaymentSettings.TestMode
        };



        return View("~/Plugins/Payments.NovalnetPostfinancecard/Views/PaymentInfo.cshtml", model);
    }
}

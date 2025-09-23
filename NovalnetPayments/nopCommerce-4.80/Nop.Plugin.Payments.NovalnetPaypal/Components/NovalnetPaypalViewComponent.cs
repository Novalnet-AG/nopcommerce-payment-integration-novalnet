using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetPaypal.Models;
using Nop.Web.Framework.Components;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.NovalnetPaypal.Components;

public class NovalnetPaypalViewComponent : NopViewComponent
{
    protected readonly NovalnetPaypalPaymentSettings _novalnetPaypalPaymentSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;

    public NovalnetPaypalViewComponent(NovalnetPaypalPaymentSettings novalnetPaypalPaymentSettings,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _novalnetPaypalPaymentSettings = novalnetPaypalPaymentSettings;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        var model = new PaymentInfoModel()
        {
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetPaypalPaymentSettings,
                x => x.DescriptionText, (await _workContext.GetWorkingLanguageAsync()).Id, store.Id),
			TestMode = _novalnetPaypalPaymentSettings.TestMode
        };
        
        return View("~/Plugins/Payments.NovalnetPaypal/Views/PaymentInfo.cshtml", model);
    }
}

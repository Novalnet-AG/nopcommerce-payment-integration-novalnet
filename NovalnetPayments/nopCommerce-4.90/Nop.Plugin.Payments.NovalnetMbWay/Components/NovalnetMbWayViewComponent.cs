using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetMbWay.Models;
using Nop.Web.Framework.Components;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.NovalnetMbWay.Components;

public class NovalnetMbWayViewComponent : NopViewComponent
{
    protected readonly NovalnetMbWayPaymentSettings _novalnetMbWayPaymentSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;

    public NovalnetMbWayViewComponent(NovalnetMbWayPaymentSettings novalnetMbWayPaymentSettings,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _novalnetMbWayPaymentSettings = novalnetMbWayPaymentSettings;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        var model = new PaymentInfoModel()
        {
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetMbWayPaymentSettings,
                x => x.DescriptionText, (await _workContext.GetWorkingLanguageAsync()).Id, store.Id),
			TestMode = _novalnetMbWayPaymentSettings.TestMode
        };

        return View("~/Plugins/Payments.NovalnetMbWay/Views/PaymentInfo.cshtml", model);
    }
}

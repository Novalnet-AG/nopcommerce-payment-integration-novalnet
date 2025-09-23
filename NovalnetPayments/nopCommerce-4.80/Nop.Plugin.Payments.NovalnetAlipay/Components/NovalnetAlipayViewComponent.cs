using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetAlipay.Models;
using Nop.Web.Framework.Components;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.NovalnetAlipay.Components;

public class NovalnetAlipayViewComponent : NopViewComponent
{
    protected readonly NovalnetAlipayPaymentSettings _novalnetAlipayPaymentSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;

    public NovalnetAlipayViewComponent(NovalnetAlipayPaymentSettings novalnetAlipayPaymentSettings,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _novalnetAlipayPaymentSettings = novalnetAlipayPaymentSettings;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        var model = new PaymentInfoModel()
        {
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetAlipayPaymentSettings,
                x => x.DescriptionText, (await _workContext.GetWorkingLanguageAsync()).Id, store.Id),
			TestMode = _novalnetAlipayPaymentSettings.TestMode
        };

        return View("~/Plugins/Payments.NovalnetAlipay/Views/PaymentInfo.cshtml", model);
    }
}

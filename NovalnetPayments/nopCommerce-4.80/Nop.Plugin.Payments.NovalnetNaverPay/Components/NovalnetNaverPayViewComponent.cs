using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetNaverPay.Models;
using Nop.Web.Framework.Components;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.NovalnetNaverPay.Components;

public class NovalnetNaverPayViewComponent : NopViewComponent
{
    protected readonly NovalnetNaverPayPaymentSettings _novalnetNaverPayPaymentSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;

    public NovalnetNaverPayViewComponent(NovalnetNaverPayPaymentSettings novalnetNaverPayPaymentSettings,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _novalnetNaverPayPaymentSettings = novalnetNaverPayPaymentSettings;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        var model = new PaymentInfoModel()
        {
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetNaverPayPaymentSettings,
                x => x.DescriptionText, (await _workContext.GetWorkingLanguageAsync()).Id, store.Id),
			TestMode = _novalnetNaverPayPaymentSettings.TestMode
        };

        return View("~/Plugins/Payments.NovalnetNaverPay/Views/PaymentInfo.cshtml", model);
    }
}

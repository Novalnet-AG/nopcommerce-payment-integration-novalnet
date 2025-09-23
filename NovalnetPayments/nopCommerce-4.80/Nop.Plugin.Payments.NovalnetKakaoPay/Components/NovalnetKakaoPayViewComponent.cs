using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetKakaoPay.Models;
using Nop.Web.Framework.Components;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.NovalnetKakaoPay.Components;

public class NovalnetKakaoPayViewComponent : NopViewComponent
{
    protected readonly NovalnetKakaoPayPaymentSettings _novalnetKakaoPayPaymentSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;

    public NovalnetKakaoPayViewComponent(NovalnetKakaoPayPaymentSettings novalnetKakaoPayPaymentSettings,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _novalnetKakaoPayPaymentSettings = novalnetKakaoPayPaymentSettings;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        var model = new PaymentInfoModel()
        {
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetKakaoPayPaymentSettings,
                x => x.DescriptionText, (await _workContext.GetWorkingLanguageAsync()).Id, store.Id),
			TestMode = _novalnetKakaoPayPaymentSettings.TestMode
        };

        return View("~/Plugins/Payments.NovalnetKakaoPay/Views/PaymentInfo.cshtml", model);
    }
}

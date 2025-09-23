using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetDirectDebitACH.Models;
using Nop.Web.Framework.Components;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.NovalnetDirectDebitACH.Components;

public class NovalnetDirectDebitACHViewComponent : NopViewComponent
{
    protected readonly NovalnetDirectDebitACHPaymentSettings _novalnetDirectDebitACHPaymentSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;

    public NovalnetDirectDebitACHViewComponent(NovalnetDirectDebitACHPaymentSettings novalnetDirectDebitACHPaymentSettings,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _novalnetDirectDebitACHPaymentSettings = novalnetDirectDebitACHPaymentSettings;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        var model = new PaymentInfoModel()
        {
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetDirectDebitACHPaymentSettings,
                x => x.DescriptionText, (await _workContext.GetWorkingLanguageAsync()).Id, store.Id),
            TestMode = _novalnetDirectDebitACHPaymentSettings.TestMode
        };

        return View("~/Plugins/Payments.NovalnetDirectDebitACH/Views/PaymentInfo.cshtml", model);
    }
}

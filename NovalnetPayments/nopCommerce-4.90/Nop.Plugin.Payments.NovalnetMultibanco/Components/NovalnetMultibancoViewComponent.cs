using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetMultibanco.Models;
using Nop.Web.Framework.Components;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.NovalnetMultibanco.Components;

public class NovalnetMultibancoViewComponent : NopViewComponent
{
    protected readonly NovalnetMultibancoPaymentSettings _novalnetMultibancoPaymentSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;

    public NovalnetMultibancoViewComponent(NovalnetMultibancoPaymentSettings novalnetMultibancoPaymentSettings,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _novalnetMultibancoPaymentSettings = novalnetMultibancoPaymentSettings;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        var model = new PaymentInfoModel()
        {
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetMultibancoPaymentSettings,
                x => x.DescriptionText, (await _workContext.GetWorkingLanguageAsync()).Id, store.Id),
			TestMode = _novalnetMultibancoPaymentSettings.TestMode
        };



        return View("~/Plugins/Payments.NovalnetMultibanco/Views/PaymentInfo.cshtml", model);
    }
}

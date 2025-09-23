using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetOnlineBankTransfer.Models;
using Nop.Web.Framework.Components;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.NovalnetOnlineBankTransfer.Components;

public class NovalnetOnlineBankTransferViewComponent : NopViewComponent
{
    protected readonly NovalnetOnlineBankTransferPaymentSettings _novalnetOnlineBankTransferPaymentSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;

    public NovalnetOnlineBankTransferViewComponent(NovalnetOnlineBankTransferPaymentSettings novalnetOnlineBankTransferPaymentSettings,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _novalnetOnlineBankTransferPaymentSettings = novalnetOnlineBankTransferPaymentSettings;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        var model = new PaymentInfoModel()
        {
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetOnlineBankTransferPaymentSettings,
                x => x.DescriptionText, (await _workContext.GetWorkingLanguageAsync()).Id, store.Id),
			TestMode = _novalnetOnlineBankTransferPaymentSettings.TestMode
        };
        
        return View("~/Plugins/Payments.NovalnetOnlineBankTransfer/Views/PaymentInfo.cshtml", model);
    }
}

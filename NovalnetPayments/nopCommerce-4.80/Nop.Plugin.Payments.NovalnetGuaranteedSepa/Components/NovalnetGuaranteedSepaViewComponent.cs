using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetGuaranteedSepa.Models;
using Nop.Web.Framework.Components;
using Nop.Services.Localization;
using Nop.Services.Customers;

namespace Nop.Plugin.Payments.NovalnetGuaranteedSepa.Components;

public class NovalnetGuaranteedSepaViewComponent : NopViewComponent
{
    protected readonly NovalnetGuaranteedSepaPaymentSettings _novalnetGuaranteedSepaPaymentSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;
    private readonly ICustomerService _customerService;

    public NovalnetGuaranteedSepaViewComponent(NovalnetGuaranteedSepaPaymentSettings novalnetGuaranteedSepaPaymentSettings,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        IWorkContext workContext,
        ICustomerService customerService)
    {
        _novalnetGuaranteedSepaPaymentSettings = novalnetGuaranteedSepaPaymentSettings;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _workContext = workContext;
        _customerService = customerService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        var customer = await _workContext.GetCurrentCustomerAsync();
        var billingAddress = await _customerService.GetCustomerBillingAddressAsync(customer);

        string customerName = billingAddress?.FirstName;
        if (!string.IsNullOrEmpty(billingAddress?.LastName))
        {
            customerName += " " + billingAddress.LastName;
        }

        var hasCompany = !string.IsNullOrEmpty(billingAddress?.Company); 

        var model = new PaymentInfoModel()
        {
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetGuaranteedSepaPaymentSettings,
                x => x.DescriptionText, (await _workContext.GetWorkingLanguageAsync()).Id, store.Id),
            HasCompany = hasCompany,
            TestMode = _novalnetGuaranteedSepaPaymentSettings.TestMode,
            CustomerName = customerName
        };

        return View("~/Plugins/Payments.NovalnetGuaranteedSepa/Views/PaymentInfo.cshtml", model);
    }
}

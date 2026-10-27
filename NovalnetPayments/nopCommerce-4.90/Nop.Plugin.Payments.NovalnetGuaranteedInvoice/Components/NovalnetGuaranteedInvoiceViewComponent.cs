using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetGuaranteedInvoice.Models;
using Nop.Web.Framework.Components;
using Nop.Services.Localization;
using Nop.Services.Customers;

namespace Nop.Plugin.Payments.NovalnetGuaranteedInvoice.Components;

public class NovalnetGuaranteedInvoiceViewComponent : NopViewComponent
{
    protected readonly NovalnetGuaranteedInvoicePaymentSettings _novalnetGuaranteedInvoicePaymentSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;
    private readonly ICustomerService _customerService;

    public NovalnetGuaranteedInvoiceViewComponent(NovalnetGuaranteedInvoicePaymentSettings novalnetGuaranteedInvoicePaymentSettings,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        IWorkContext workContext,
        ICustomerService customerService)
    {
        _novalnetGuaranteedInvoicePaymentSettings = novalnetGuaranteedInvoicePaymentSettings;
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

        var hasCompany = !string.IsNullOrEmpty(billingAddress?.Company); 

        var model = new PaymentInfoModel()
        {
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetGuaranteedInvoicePaymentSettings,
                x => x.DescriptionText, (await _workContext.GetWorkingLanguageAsync()).Id, store.Id),
            HasCompany = hasCompany,
            TestMode = _novalnetGuaranteedInvoicePaymentSettings.TestMode
        };

        return View("~/Plugins/Payments.NovalnetGuaranteedInvoice/Views/PaymentInfo.cshtml", model);
    }
}

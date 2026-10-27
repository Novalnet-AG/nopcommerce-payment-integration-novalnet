using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NovalnetSepa.Models;
using Nop.Web.Framework.Components;
using Nop.Services.Localization;
using Nop.Services.Customers;

namespace Nop.Plugin.Payments.NovalnetSepa.Components;

public class NovalnetSepaViewComponent : NopViewComponent
{
    protected readonly NovalnetSepaPaymentSettings _novalnetSepaPaymentSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;
    private readonly ICustomerService _customerService;

    public NovalnetSepaViewComponent(NovalnetSepaPaymentSettings novalnetSepaPaymentSettings,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        IWorkContext workContext,
        ICustomerService customerService)
    {
        _novalnetSepaPaymentSettings = novalnetSepaPaymentSettings;
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

        var model = new PaymentInfoModel()
        {
            DescriptionText = await _localizationService.GetLocalizedSettingAsync(_novalnetSepaPaymentSettings,
                x => x.DescriptionText, (await _workContext.GetWorkingLanguageAsync()).Id, store.Id),
            TestMode = _novalnetSepaPaymentSettings.TestMode,
            CustomerName = customerName
        };

        return View("~/Plugins/Payments.NovalnetSepa/Views/PaymentInfo.cshtml", model);
    }
}

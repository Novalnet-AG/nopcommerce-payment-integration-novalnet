using Nop.Core.Domain.Directory;
using Nop.Core.Events;
using Nop.Services.Events;
using Nop.Services.Payments;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.UI;

namespace Nop.Plugin.Payments.NovalnetGooglePay.Services;

/// <summary>
/// Represents plugin event consumer
/// </summary>
public class EventConsumer :
    IConsumer<PageRenderingEvent>
{
    #region Fields
    private readonly INopHtmlHelper _nopHtmlHelper;
    private readonly IPaymentPluginManager _paymentPluginManager;

    #endregion

    #region Ctor

    public EventConsumer(
        INopHtmlHelper nopHtmlHelper,
        IPaymentPluginManager paymentPluginManager)
    {
        _nopHtmlHelper = nopHtmlHelper;
        _paymentPluginManager = paymentPluginManager;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Handle event
    /// </summary>
    /// <param name="eventMessage">Event</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(PageRenderingEvent eventMessage)
    {
        if (!await _paymentPluginManager.IsPluginActiveAsync("Payments.NovalnetGooglePay"))
            return;

        var routeName = eventMessage.GetRouteName() ?? string.Empty;

        // if (routeName == "CheckoutOnePage")
        // {
            _nopHtmlHelper.AddScriptParts(
                ResourceLocation.Footer,
                "https://cdn.novalnet.de/js/v3/payment.js",
                excludeFromBundle: true);

            //shared Google Pay logic used by PaymentInfo.cshtml, which is loaded into the page via AJAX
            _nopHtmlHelper.AddScriptParts(
                ResourceLocation.Footer,
                "~/Plugins/Payments.NovalnetGooglePay/Content/js/novalnet-googlepay.js",
                excludeFromBundle: true);
        // }
    }

    #endregion
}
using Nop.Core.Domain.Directory;
using Nop.Core.Events;
using Nop.Services.Events;
using Nop.Services.Payments;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.UI;

namespace Nop.Plugin.Payments.NovalnetCreditcard.Services;

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
        if (!await _paymentPluginManager.IsPluginActiveAsync("Payments.NovalnetCreditcard"))
            return;

        //add js script to one page checkout
        var routeName = eventMessage.GetRouteName() ?? string.Empty;
        if (routeName == "CheckoutOnePage")
        {
            _nopHtmlHelper.AddScriptParts(ResourceLocation.Footer, "https://cdn.novalnet.de/js/v2/NovalnetUtility.js", excludeFromBundle: true);
        }
    }

    #endregion
}
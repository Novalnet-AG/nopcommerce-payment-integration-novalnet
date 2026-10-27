using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.NovalnetAPIConfiguration.Infrastructure;

public class RouteProvider : IRouteProvider
{
    /// <summary>
    /// Register routes
    /// </summary>
    /// <param name="endpointRouteBuilder">Route builder</param>
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapControllerRoute(name: "Plugin.Payments.NovalnetAPIConfiguration.RedirectResponseHandler",
        pattern: "NovalnetPayment/RedirectResponseHandler",
        defaults: new { controller = "NovalnetPayment", action = "RedirectResponseHandler" });
            

        endpointRouteBuilder.MapControllerRoute(name: "Plugin.Payments.NovalnetAPIConfiguration.WebhookHandler",
        pattern: "NovalnetWebhook/WebhookHandler",
        defaults: new { controller = "NovalnetWebhook", action = "WebhookHandler" });
    }

    /// <summary>
    /// Gets a priority of route provider
    /// </summary>
    public int Priority => 0;
}

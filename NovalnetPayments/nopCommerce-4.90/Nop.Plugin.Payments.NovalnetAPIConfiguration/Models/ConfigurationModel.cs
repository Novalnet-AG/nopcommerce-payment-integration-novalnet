using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using Nop.Web.Framework.Validators;


namespace Nop.Plugin.Payments.NovalnetAPIConfiguration.Models;

public record ConfigurationModel : BaseNopModel
{
    public int ActiveStoreScopeConfiguration { get; set; }

    [NopResourceDisplayName("Plugins.Payment.NovalnetAPIConfiguration.Fields.ProductActivationKey")]
    public string ProductActivationKey { get; set; }
    public bool ProductActivationKey_OverrideForStore { get; set; }


    [NopResourceDisplayName("Plugins.Payment.NovalnetAPIConfiguration.Fields.PaymentAccessKey")]
    public string PaymentAccessKey { get; set; }
    public bool PaymentAccessKey_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payment.NovalnetAPIConfiguration.Fields.ClientKey")]
    public string ClientKey { get; set; }
    public bool ClientKey_OverrideForStore { get; set; }


    [NopResourceDisplayName("Plugins.Payment.NovalnetAPIConfiguration.Fields.TariffID")]
    public string TariffID { get; set; }
    public bool TariffID_OverrideForStore { get; set; }


    [NopResourceDisplayName("Plugins.Payment.NovalnetAPIConfiguration.Fields.WebhookTestMode")]
    public bool WebhookTestMode { get; set; }
    public bool WebhookTestMode_OverrideForStore { get; set; }


    [NopResourceDisplayName("Plugins.Payment.NovalnetAPIConfiguration.Fields.WebhookEmailTo")]
    public string WebhookEmailTo { get; set; }
    public bool WebhookEmailTo_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payment.NovalnetAPIConfiguration.Fields.WebhookUrl")]
    public string WebhookUrl { get; set; }
    public bool WebhookUrl_OverrideForStore { get; set; }
     
}

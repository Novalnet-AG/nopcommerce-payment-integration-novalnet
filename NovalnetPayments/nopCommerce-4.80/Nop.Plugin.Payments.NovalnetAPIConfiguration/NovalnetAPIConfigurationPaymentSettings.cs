using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.NovalnetAPIConfiguration;

/// <summary>
/// Represents settings of payment plugin
/// </summary>
public class NovalnetAPIConfigurationPaymentSettings : ISettings
{
    /// <summary>
    /// Gets or sets a description text
    /// </summary>
    public string ProductActivationKey { get; set; }

    /// <summary>
    /// Gets or sets payment transaction mode
    /// </summary>
    public string PaymentAccessKey { get; set; }

    public string ClientKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value.
    /// </summary>
    public string TariffID { get; set; }

    /// <summary>
    /// Gets or sets an additional fee
    /// </summary>
    public bool WebhookTestMode { get; set; }

    public string WebhookEmailTo { get; set; }
    
    public string WebhookUrl { get; set; }
}

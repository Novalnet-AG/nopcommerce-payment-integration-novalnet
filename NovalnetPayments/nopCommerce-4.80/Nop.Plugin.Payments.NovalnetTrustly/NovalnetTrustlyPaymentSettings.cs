using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.NovalnetTrustly;

/// <summary>
/// Represents settings of payment plugin
/// </summary>
public class NovalnetTrustlyPaymentSettings : ISettings
{
    public bool TestMode { get; set; }
    
    /// <summary>
    /// Gets or sets a description text
    /// </summary>
    public string DescriptionText { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value.
    /// </summary>
    public bool AdditionalFeePercentage { get; set; }

    /// <summary>
    /// Gets or sets an additional fee
    /// </summary>
    public decimal AdditionalFee { get; set; }
}

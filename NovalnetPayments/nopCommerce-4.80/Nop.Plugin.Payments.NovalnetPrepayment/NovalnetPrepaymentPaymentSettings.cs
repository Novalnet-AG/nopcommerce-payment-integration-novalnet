using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.NovalnetPrepayment;

/// <summary>
/// Represents settings of payment plugin
/// </summary>
public class NovalnetPrepaymentPaymentSettings : ISettings
{
    /// <summary>
    /// Gets or sets a description text
    /// </summary>
    public string DescriptionText { get; set; }
    
    public bool TestMode { get; set; }

    public int DueDate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value.
    /// </summary>
    public bool AdditionalFeePercentage { get; set; }

    /// <summary>
    /// Gets or sets an additional fee
    /// </summary>
    public decimal AdditionalFee { get; set; }
}

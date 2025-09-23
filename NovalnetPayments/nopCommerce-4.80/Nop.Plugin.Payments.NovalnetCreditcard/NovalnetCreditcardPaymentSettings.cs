using Nop.Core.Configuration;
using Nop.Plugin.Payments.NovalnetAPIConfiguration;

namespace Nop.Plugin.Payments.NovalnetCreditcard;

/// <summary>
/// Represents settings of payment plugin
/// </summary>
public class NovalnetCreditcardPaymentSettings : ISettings
{
    /// <summary>
    /// Gets or sets a description text
    /// </summary>
    public string DescriptionText { get; set; }

    public bool TestMode { get; set; }
    public PaymentAction PaymentAction { get; set; }
    public int OnholdMinAmount { get; set; }

    public bool Enforce3d { get; set; }
    public bool InlineForm { get; set; }
    public string LabelStyle { get; set; }
    public string InputStyle { get; set; }
    public string TextStyle { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value.
    /// </summary>
    public bool AdditionalFeePercentage { get; set; }

    /// <summary>
    /// Gets or sets an additional fee
    /// </summary>
    public decimal AdditionalFee { get; set; }
}

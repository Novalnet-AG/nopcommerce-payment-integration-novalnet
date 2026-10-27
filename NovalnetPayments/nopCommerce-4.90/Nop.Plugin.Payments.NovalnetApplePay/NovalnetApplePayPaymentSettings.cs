using Nop.Core.Configuration;
using Nop.Plugin.Payments.NovalnetAPIConfiguration;

namespace Nop.Plugin.Payments.NovalnetApplePay;

/// <summary>
/// Represents settings of payment plugin
/// </summary>
public class NovalnetNovalnetApplePayPaymentSettings : ISettings
{
    /// <summary>
    /// Gets or sets a description text
    /// </summary>
    public string DescriptionText { get; set; }

    public bool TestMode { get; set; }
    public PaymentAction PaymentAction { get; set; }
    public int OnholdMinAmount { get; set; }

    /// <summary>
    /// Gets or sets the merchant's business name shown on the Apple Pay payment sheet
    /// </summary>
    public string BusinessName { get; set; }

    /// <summary>
    /// Gets or sets the Apple Pay button type (e.g. default, buy, donate, book, contribute, checkout, rent, support)
    /// </summary>
    public string ButtonType { get; set; }

    /// <summary>
    /// Gets or sets the Apple Pay button style (black, white, white-outline)
    /// </summary>
    public string ButtonTheme { get; set; }

    /// <summary>
    /// Gets or sets the Apple Pay button height, in pixels
    /// </summary>
    public int ButtonHeight { get; set; }

    /// <summary>
    /// Gets or sets the Apple Pay button corner radius, in pixels
    /// </summary>
    public int ButtonCornerRadius { get; set; }

    /// <summary>
    /// Gets or sets a comma-separated list of pages the Apple Pay button is displayed on
    /// (any combination of "cart", "product", "checkout")
    /// </summary>
    public string DisplayPages { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value.
    /// </summary>
    public bool AdditionalFeePercentage { get; set; }

    /// <summary>
    /// Gets or sets an additional fee
    /// </summary>
    public decimal AdditionalFee { get; set; }
}

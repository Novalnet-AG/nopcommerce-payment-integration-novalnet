using Nop.Core.Configuration;
using Nop.Plugin.Payments.NovalnetAPIConfiguration;

namespace Nop.Plugin.Payments.NovalnetGooglePay;

/// <summary>
/// Represents settings of payment plugin
/// </summary>
public class NovalnetNovalnetGooglePayPaymentSettings : ISettings
{
    /// <summary>
    /// Gets or sets a description text
    /// </summary>
    public string DescriptionText { get; set; }

    public bool TestMode { get; set; }
    public PaymentAction PaymentAction { get; set; }
    public int OnholdMinAmount { get; set; }

    /// <summary>
    /// Gets or sets the merchant's business name shown on the Google Pay payment sheet
    /// </summary>
    public string BusinessName { get; set; }

    /// <summary>
    /// Gets or sets the Google Merchant ID issued by the Google Pay & Wallet Console
    /// </summary>
    public string GoogleMerchantId { get; set; }

    /// <summary>
    /// Gets or sets a comma-separated list of pages the Google Pay express checkout button is displayed on
    /// (any combination of "cart", "product")
    /// </summary>
    public string ExpressCheckoutPages { get; set; }

    /// <summary>
    /// Gets or sets the Google Pay button height in pixels (40 - 100)
    /// </summary>
    public int ButtonHeight { get; set; }

    /// <summary>
    /// Gets or sets the Google Pay button type, which determines the button text (book, buy, checkout, ...)
    /// </summary>
    public string ButtonType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value.
    /// </summary>
    public bool AdditionalFeePercentage { get; set; }

    /// <summary>
    /// Gets or sets an additional fee
    /// </summary>
    public decimal AdditionalFee { get; set; }
}

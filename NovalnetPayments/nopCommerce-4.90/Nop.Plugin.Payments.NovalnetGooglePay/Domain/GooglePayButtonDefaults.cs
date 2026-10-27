namespace Nop.Plugin.Payments.NovalnetGooglePay.Domain;

/// <summary>
/// Defaults and allowed values for the Google Pay button appearance settings
/// </summary>
public static class GooglePayButtonDefaults
{
    public const int MinHeight = 40;
    public const int MaxHeight = 100;
    public const int DefaultHeight = 45;

    public const string DefaultType = "plain";

    /// <summary>
    /// Gets the supported Google Pay button types (value => display text)
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Types = new Dictionary<string, string>
    {
        ["book"] = "Book",
        ["buy"] = "Buy",
        ["checkout"] = "Checkout",
        ["donate"] = "Donate",
        ["order"] = "Order",
        ["pay"] = "Pay",
        ["plain"] = "Plain",
        ["subscribe"] = "Subscribe",
    };

    /// <summary>
    /// Pages the express checkout button is displayed on when the setting has never been saved
    /// </summary>
    public static readonly string[] DefaultExpressCheckoutPages = { "cart", "product" };

    /// <summary>
    /// Gets the configured button height, falling back to the default when unset or out of range
    /// (e.g. an existing install that has never saved this setting)
    /// </summary>
    public static int GetButtonHeight(NovalnetNovalnetGooglePayPaymentSettings settings)
    {
        return settings.ButtonHeight is >= MinHeight and <= MaxHeight ? settings.ButtonHeight : DefaultHeight;
    }

    /// <summary>
    /// Gets the configured button type, falling back to the default when unset or unknown
    /// </summary>
    public static string GetButtonType(NovalnetNovalnetGooglePayPaymentSettings settings)
    {
        return !string.IsNullOrEmpty(settings.ButtonType) && Types.ContainsKey(settings.ButtonType) ? settings.ButtonType : DefaultType;
    }

    /// <summary>
    /// Gets the pages the express checkout button is enabled for
    /// </summary>
    public static IList<string> GetExpressCheckoutPages(NovalnetNovalnetGooglePayPaymentSettings settings)
    {
        return settings.ExpressCheckoutPages == null
            ? DefaultExpressCheckoutPages.ToList()
            : settings.ExpressCheckoutPages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}

namespace Nop.Plugin.Payments.NovalnetGooglePay.Domain;

/// <summary>
/// Represents a page on which the Google Pay express button is displayed
/// </summary>
public enum ExpressButtonPlacement
{
    /// <summary>
    /// Product details page
    /// </summary>
    Product = 1,

    /// <summary>
    /// Shopping cart page
    /// </summary>
    Cart = 2
}

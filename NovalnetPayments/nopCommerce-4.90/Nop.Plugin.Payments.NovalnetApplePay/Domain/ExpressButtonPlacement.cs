namespace Nop.Plugin.Payments.NovalnetApplePay.Domain;

/// <summary>
/// Represents a page on which the Apple Pay express button is displayed
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
    Cart = 2,
}

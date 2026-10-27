using Nop.Plugin.Payments.NovalnetApplePay.Domain;

namespace Nop.Plugin.Payments.NovalnetApplePay.Models;

/// <summary>
/// Represents the model backing the Apple Pay express checkout button
/// (Product details / Shopping cart pages)
/// </summary>
public class ExpressCheckoutModel
{
    public ExpressButtonPlacement Placement { get; set; }

    public int? ProductId { get; set; }

    public int? IsVirtualProduct { get; set; }

    public bool TestMode { get; set; }

    /// <summary>
    /// Same shape as the payment-page PaymentJsonData (client_key, business_name,
    /// transaction), used to seed the Novalnet payment SDK before the real cart/product amount
    /// is fetched (see GetExpressTransactionInfo)
    /// </summary>
    public string PaymentJsonData { get; set; }
    public int? CheckoutDisabled { get; set; }
}

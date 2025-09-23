using Nop.Core;

namespace Nop.Plugin.Payments.NovalnetAPIConfiguration.Domain;

/// <summary>
/// Represents the Novalnet Transaction Details
/// </summary>
public class NovalnetTransactionDetails : BaseEntity
{
    /// <summary>
    /// Gets or sets the Novalnet Transaction ID
    /// </summary>
    public string Tid { get; set; }

    /// <summary>
    /// Gets or sets the Novalnet Transaction Status
    /// </summary>
    public string TidStatus { get; set; }

    /// <summary>
    /// Gets or sets the Order number
    /// </summary>
    public string OrderNo { get; set; }

    /// <summary>
    /// Gets or sets the order amount
    /// </summary>
    public int OrderAmount { get; set; }

    /// <summary>
    /// Gets or sets the followup transaction amount
    /// </summary>
    public int CallbackAmount { get; set; }
}

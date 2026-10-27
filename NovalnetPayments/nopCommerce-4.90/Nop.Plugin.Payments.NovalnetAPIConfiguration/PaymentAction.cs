
namespace Nop.Plugin.Payments.NovalnetAPIConfiguration;

/// <summary>
/// Represents manual payment processor transaction mode
/// </summary>
public enum PaymentAction
{
    /// <summary>
    /// Authorize
    /// </summary>
    Authorize = 1,

    /// <summary>
    /// Capture
    /// </summary>
    Capture = 0
}

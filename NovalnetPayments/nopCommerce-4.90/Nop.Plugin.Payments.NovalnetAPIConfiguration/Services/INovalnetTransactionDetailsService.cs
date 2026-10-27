using Nop.Core;
using Nop.Plugin.Payments.NovalnetAPIConfiguration.Domain;

namespace Nop.Plugin.Payments.NovalnetAPIConfiguration.Services;

/// <summary>
/// Represents service shipping by weight service
/// </summary>
public interface INovalnetTransactionDetailsService
{

    /// <summary>
    /// Insert the NovalnetTransactionDetails
    /// </summary>
    /// <param name="novalnetTransactionDetails">NovalnetTransactionDetails</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task InsertAsync(NovalnetTransactionDetails novalnetTransactionDetails);

    /// <summary>
    /// Update the NovalnetTransactionDetails
    /// </summary>
    /// <param name="novalnetTransactionDetails">NovalnetTransactionDetails</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task UpdateAsync(NovalnetTransactionDetails novalnetTransactionDetails);

    /// <summary>
    /// Get Novalnet transaction details by transaction ID
    /// </summary>
    /// <param name="tid">Novalnet transaction ID</param>
    /// <returns>The matching NovalnetTransactionDetails entity</returns>
    Task<NovalnetTransactionDetails> GetByTIDAsync(string tid);
}

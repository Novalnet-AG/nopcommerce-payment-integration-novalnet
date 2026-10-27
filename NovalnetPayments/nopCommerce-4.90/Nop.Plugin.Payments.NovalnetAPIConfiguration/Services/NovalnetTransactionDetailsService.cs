using Nop.Core;
using Nop.Core.Caching;
using Nop.Data;
using Nop.Plugin.Payments.NovalnetAPIConfiguration.Domain;

namespace Nop.Plugin.Payments.NovalnetAPIConfiguration.Services;

/// <summary>
/// Represents service novalnetTransactionDetails service implementation
/// </summary>
public class NovalnetTransactionDetailsService : INovalnetTransactionDetailsService
{
    #region Fields

    protected readonly IRepository<NovalnetTransactionDetails> _nnRepository;
    protected readonly IShortTermCacheManager _shortTermCacheManager;
    protected readonly IStaticCacheManager _staticCacheManager;

    #endregion

    #region Ctor

    public NovalnetTransactionDetailsService(IRepository<NovalnetTransactionDetails> nnRepository,
        IShortTermCacheManager shortTermCacheManager,
        IStaticCacheManager staticCacheManager)
    {
        _nnRepository = nnRepository;
        _shortTermCacheManager = shortTermCacheManager;
        _staticCacheManager = staticCacheManager;
    }

    #endregion


    #region Methods


    /// <summary>
    /// Insert the novalnetTransactionDetails
    /// </summary>
    /// <param name="novalnetTransactionDetails">novalnetTransactionDetails</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task InsertAsync(NovalnetTransactionDetails novalnetTransactionDetails)
    {
        await _nnRepository.InsertAsync(novalnetTransactionDetails, false);
    }

    /// <summary>
    /// Update the novalnetTransactionDetails
    /// </summary>
    /// <param name="novalnetTransactionDetails">novalnetTransactionDetails</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task UpdateAsync(NovalnetTransactionDetails novalnetTransactionDetails)
    {
        await _nnRepository.UpdateAsync(novalnetTransactionDetails, false);
    }
    
    /// <summary>
    /// Get Novalnet transaction details by transaction ID
    /// </summary>
    /// <param name="tid">Novalnet transaction ID</param>
    /// <returns>The matching NovalnetTransactionDetails entity</returns>
    public virtual async Task<NovalnetTransactionDetails> GetByTIDAsync(string tid)
    {
        if (string.IsNullOrWhiteSpace(tid))
            return null;

        return await _nnRepository.Table
            .Where(x => x.Tid == tid)
            .FirstOrDefaultAsync();
    }

    #endregion
}
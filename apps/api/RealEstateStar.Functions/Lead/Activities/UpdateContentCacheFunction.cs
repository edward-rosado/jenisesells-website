using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Functions.Lead.Models;

namespace RealEstateStar.Functions.Lead.Activities;

/// <summary>
/// Stores fresh CMA and HomeSearch results in <see cref="IDistributedContentCache"/>
/// so future leads for the same property/criteria can skip the expensive analysis.
/// </summary>
public sealed class UpdateContentCacheFunction(
    IDistributedContentCache contentCache,
    ILogger<UpdateContentCacheFunction> logger)
{
    /// <summary>Cache TTL for CMA results (property analysis is stable for 24h).</summary>
    private static readonly TimeSpan CmaCacheTtl = TimeSpan.FromHours(24);

    /// <summary>Cache TTL for HomeSearch results (listing inventory changes hourly).</summary>
    private static readonly TimeSpan HomeSearchCacheTtl = TimeSpan.FromHours(1);

    /// <summary>Exposed for unit tests only — verifies TTL constant values.</summary>
    internal static TimeSpan CmaCacheTtlForTests => CmaCacheTtl;
    internal static TimeSpan HomeSearchCacheTtlForTests => HomeSearchCacheTtl;

    [Function("UpdateContentCache")]
    public async Task RunAsync(
        [ActivityTrigger] UpdateContentCacheInput input,
        CancellationToken ct)
    {
        var tasks = new List<Task>();

        if (input.CmaResult?.Success == true)
        {
            tasks.Add(contentCache.SetAsync(input.CmaInputHash, input.CmaResult, CmaCacheTtl, ct));
            logger.LogInformation("[UCC-010] CMA result cached. Hash={Hash}, CorrelationId={CorrelationId}",
                input.CmaInputHash, input.CorrelationId);
        }

        if (input.HsResult?.Success == true)
        {
            tasks.Add(contentCache.SetAsync(input.HsInputHash, input.HsResult, HomeSearchCacheTtl, ct));
            logger.LogInformation("[UCC-011] HomeSearch result cached. Hash={Hash}, CorrelationId={CorrelationId}",
                input.HsInputHash, input.CorrelationId);
        }

        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
    }
}

using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Functions.Lead.Models;

namespace RealEstateStar.Functions.Lead.Activities;

/// <summary>
/// Checks <see cref="IDistributedContentCache"/> for existing CMA and HomeSearch results.
/// Cross-lead dedup: if the same property address was analyzed for another lead recently,
/// the cached result is returned so the expensive CMA/HS workers can be skipped.
/// </summary>
public sealed class CheckContentCacheFunction(
    IDistributedContentCache contentCache,
    ILogger<CheckContentCacheFunction> logger)
{
    [Function("CheckContentCache")]
    public async Task<string> RunAsync(
        [ActivityTrigger] CheckContentCacheInput input,
        CancellationToken ct)
    {
        var cmaTask = contentCache.GetAsync<CmaWorkerResult>(input.CmaInputHash, ct);
        var hsTask = contentCache.GetAsync<HomeSearchWorkerResult>(input.HsInputHash, ct);

        await Task.WhenAll(cmaTask, hsTask);

        var cachedCma = cmaTask.Result;
        var cachedHs = hsTask.Result;

        if (cachedCma is not null)
            logger.LogInformation("[CCC-010] CMA cache hit. Hash={Hash}, CorrelationId={CorrelationId}",
                input.CmaInputHash, input.CorrelationId);

        if (cachedHs is not null)
            logger.LogInformation("[CCC-011] HomeSearch cache hit. Hash={Hash}, CorrelationId={CorrelationId}",
                input.HsInputHash, input.CorrelationId);

        return JsonSerializer.Serialize(new CheckContentCacheOutput
        {
            CmaCacheHit = cachedCma is not null,
            HsCacheHit = cachedHs is not null,
            CachedCmaResult = cachedCma,
            CachedHsResult = cachedHs
        });
    }
}

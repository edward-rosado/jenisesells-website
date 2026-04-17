using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Functions.Activation.Dtos;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 5 activity: rehost external image URLs to Cloudflare R2 for production delivery.
///
/// Extracts image URLs from SiteFacts.RecentSales and copies them to R2 for reliable,
/// CDN-accelerated serving without external dependencies. Activity must complete successfully
/// or site deployment will fail.
///
/// Failure semantics: FATAL (not best-effort) — all exceptions propagate so DF retries.
/// R2 client integration pending. Currently logs extracted URLs for auditing.
///
/// Returns pre-serialized JSON string to work around Azure Durable Functions SDK
/// record.ToString() serialization bug (Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.2.3).
/// </summary>
public sealed class RehostAssetsToR2Activity(
    ILogger<RehostAssetsToR2Activity> logger)
{
    [Function(ActivityNames.RehostAssetsToR2)]
    public async Task<string> RunAsync(
        [ActivityTrigger] RehostAssetsToR2Input input,
        CancellationToken ct)
    {
        var activityName = nameof(RehostAssetsToR2Activity);
        var startTime = DateTime.UtcNow;

        logger.LogInformation(
            "[REHOST-000] {Activity} starting for accountId={AccountId} agentId={AgentId} correlationId={CorrelationId}",
            activityName, input.AccountId, input.AgentId, input.CorrelationId);

        try
        {
            if (input.Facts is null)
            {
                logger.LogError(
                    "[REHOST-001] SiteFacts is null for accountId={AccountId}. Activity cannot proceed.",
                    input.AccountId);
                throw new InvalidOperationException("RehostAssetsToR2 input Facts cannot be null");
            }

            // Extract all image URLs from SiteFacts.RecentSales
            var assetUrls = new Dictionary<string, string>();
            var urlCount = 0;

            if (input.Facts.RecentSales is not null && input.Facts.RecentSales.Count > 0)
            {
                foreach (var (idx, sale) in input.Facts.RecentSales.Select((s, i) => (i, s)))
                {
                    if (!string.IsNullOrEmpty(sale.ImageUrl))
                    {
                        try
                        {
                            var key = $"recent-sale-{idx}";
                            // Validate URL format before queuing
                            if (!Uri.TryCreate(sale.ImageUrl, UriKind.Absolute, out var _))
                            {
                                logger.LogWarning(
                                    "[REHOST-002] Invalid URL format for sale {Index} at {Address}: {Url}",
                                    idx, sale.Address, sale.ImageUrl);
                                continue;
                            }

                            // TODO: Call R2 client to copy sale.ImageUrl to R2 bucket
                            // For now, preserve URL in mapping for future rehosting
                            assetUrls[key] = sale.ImageUrl;
                            urlCount++;

                            logger.LogInformation(
                                "[REHOST-003] Extracted asset for rehosting: key={Key} sale={Index} address={Address}",
                                key, idx, sale.Address);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex,
                                "[REHOST-004] Error processing sale {Index} at {Address}: {Message}",
                                idx, sale.Address, ex.Message);
                            throw;
                        }
                    }
                }
            }
            else
            {
                logger.LogWarning(
                    "[REHOST-005] No RecentSales found in SiteFacts for accountId={AccountId}. Continuing with empty asset map.",
                    input.AccountId);
            }

            var elapsed = DateTime.UtcNow - startTime;
            logger.LogInformation(
                "[REHOST-010] {Activity} completed for accountId={AccountId}. " +
                "Extracted {Count} assets. Duration={DurationMs}ms",
                activityName, input.AccountId, urlCount, elapsed.TotalMilliseconds);

            // Return output with extracted assets (actual R2 URLs pending client integration)
            var output = new RehostAssetsToR2Output
            {
                AssetUrlsByKey = assetUrls,
                RehostedCount = urlCount
            };

            return await Task.FromResult(JsonSerializer.Serialize(output)).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogError(ex,
                "[REHOST-011] {Activity} cancelled for accountId={AccountId}. CorrelationId={CorrelationId}",
                activityName, input.AccountId, input.CorrelationId);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex,
                "[REHOST-012] {Activity} validation failed for accountId={AccountId}: {Message}. CorrelationId={CorrelationId}",
                activityName, input.AccountId, ex.Message, input.CorrelationId);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[REHOST-020] {Activity} FAILED for accountId={AccountId} agentId={AgentId}: {Message}. CorrelationId={CorrelationId}",
                activityName, input.AccountId, input.AgentId, ex.Message, input.CorrelationId);
            throw;
        }
    }
}

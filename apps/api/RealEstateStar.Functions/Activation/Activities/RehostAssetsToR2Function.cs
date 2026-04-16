using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Functions.Activation.Dtos;

namespace RealEstateStar.Functions.Activation.Activities;

/// <summary>
/// Phase 6 activity: downloads agent assets (headshot, logo, icon) from external URLs
/// and uploads them to Cloudflare R2 for reliable, CDN-backed delivery.
///
/// Memory safety:
/// - Max 2 concurrent downloads (SemaphoreSlim(2, 2))
/// - 5 MB per-asset size cap — assets exceeding the cap are skipped with a warning
/// - Streams released immediately after upload to avoid accumulating binary data
///
/// Failure semantics: BEST-EFFORT — a failed asset is skipped; the activity never throws
/// on a single-asset failure. The activity throws only on unexpected/unrecoverable errors.
///
/// R2 key format:  agents/{accountId}/{agentId}/{asset-type}.{ext}
/// Public URL:     https://assets.real-estate-star.com/agents/{accountId}/{agentId}/{asset-type}.{ext}
/// </summary>
public sealed class RehostAssetsToR2Function(
    ICloudflareR2Client r2Client,
    IHttpClientFactory httpClientFactory,
    IOptions<RehostAssetsOptions> options,
    ILogger<RehostAssetsToR2Function> logger)
{
    private const int MaxConcurrentDownloads = 2;
    private const long MaxAssetBytes = 5 * 1024 * 1024; // 5 MB
    private const string AssetsBaseUrl = "https://assets.real-estate-star.com";

    [Function(ActivityNames.RehostAssetsToR2)]
    public async Task<RehostAssetsResult> RunAsync(
        [ActivityTrigger] RehostAssetsInput input,
        CancellationToken ct)
    {
        logger.LogInformation(
            "[REHOST-001] RehostAssetsToR2 starting. accountId={AccountId} agentId={AgentId} " +
            "correlationId={CorrelationId}",
            input.AccountId, input.AgentId, input.CorrelationId);

        try
        {
            var bucket = options.Value.BucketName;
            var semaphore = new SemaphoreSlim(MaxConcurrentDownloads, MaxConcurrentDownloads);
            var resultLock = new object();

            string? headshotR2Url = null;
            string? logoR2Url = null;
            string? iconR2Url = null;
            var assetsRehosted = 0;

            // Build the candidate list — skip null URLs up front
            var candidates = new List<(string AssetType, string SourceUrl, Action<string> SetResult)>();

            if (!string.IsNullOrWhiteSpace(input.HeadshotUrl))
                candidates.Add(("headshot", input.HeadshotUrl, url => headshotR2Url = url));

            if (!string.IsNullOrWhiteSpace(input.LogoUrl))
                candidates.Add(("logo", input.LogoUrl, url => logoR2Url = url));

            if (!string.IsNullOrWhiteSpace(input.IconUrl))
                candidates.Add(("icon", input.IconUrl, url => iconR2Url = url));

            if (candidates.Count == 0)
            {
                logger.LogInformation(
                    "[REHOST-001] No asset URLs provided for agentId={AgentId}; nothing to rehost",
                    input.AgentId);
                return new RehostAssetsResult { AssetsRehosted = 0 };
            }

            // Fan out: max 2 concurrent downloads
            var tasks = candidates.Select(async candidate =>
            {
                var (assetType, sourceUrl, setResult) = candidate;

                await semaphore.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var r2Url = await RehostSingleAssetAsync(
                        bucket, input.AccountId, input.AgentId, assetType, sourceUrl, ct)
                        .ConfigureAwait(false);

                    if (r2Url is not null)
                    {
                        lock (resultLock)
                        {
                            setResult(r2Url);
                            assetsRehosted++;
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);

            logger.LogInformation(
                "[REHOST-020] RehostAssetsToR2 complete. agentId={AgentId} " +
                "rehosted={AssetsRehosted}/{TotalCandidates}",
                input.AgentId, assetsRehosted, candidates.Count);

            return new RehostAssetsResult
            {
                HeadshotR2Url = headshotR2Url,
                LogoR2Url = logoR2Url,
                IconR2Url = iconR2Url,
                AssetsRehosted = assetsRehosted,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[REHOST-030] RehostAssetsToR2 FAILED for agentId={AgentId}: {Message}",
                input.AgentId, ex.Message);
            throw;
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Downloads a single asset from <paramref name="sourceUrl"/>, enforces the size cap,
    /// and uploads it to R2. Returns the public R2 URL on success, or null if the asset
    /// should be skipped (failure, size exceeded, etc.).
    /// </summary>
    internal async Task<string?> RehostSingleAssetAsync(
        string bucket,
        string accountId,
        string agentId,
        string assetType,
        string sourceUrl,
        CancellationToken ct)
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient("RehostAssets");

            using var response = await httpClient.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "[REHOST-010] Download failed for asset={AssetType} agentId={AgentId} " +
                    "url={SourceUrl} status={StatusCode}; skipping",
                    assetType, agentId, sourceUrl, (int)response.StatusCode);
                return null;
            }

            // Size cap check via Content-Length header (fast path — avoids downloading oversized assets)
            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength.HasValue && contentLength.Value > MaxAssetBytes)
            {
                logger.LogWarning(
                    "[REHOST-011] Asset exceeds 5 MB size cap. asset={AssetType} agentId={AgentId} " +
                    "size={Bytes}; skipping",
                    assetType, agentId, contentLength.Value);
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType
                              ?? "application/octet-stream";
            var ext = GetExtensionFromContentType(contentType);
            var r2Key = $"agents/{accountId}/{agentId}/{assetType}{ext}";

            // Stream with bounded read: copy into a MemoryStream capped at MaxAssetBytes + 1
            // so we can detect an oversized body even without a Content-Length header.
            await using var responseStream = await response.Content.ReadAsStreamAsync(ct)
                .ConfigureAwait(false);
            await using var boundedStream = new MemoryStream();

            var buffer = new byte[81920]; // 80 KB read buffer
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await responseStream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                totalRead += bytesRead;
                if (totalRead > MaxAssetBytes)
                {
                    logger.LogWarning(
                        "[REHOST-012] Asset body exceeds 5 MB size cap after streaming. " +
                        "asset={AssetType} agentId={AgentId}; skipping",
                        assetType, agentId);
                    return null;
                }

                await boundedStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct)
                    .ConfigureAwait(false);
            }

            boundedStream.Position = 0;

            await r2Client.PutObjectAsync(bucket, r2Key, boundedStream, contentType, ct)
                .ConfigureAwait(false);

            var publicUrl = $"{AssetsBaseUrl}/{r2Key}";

            logger.LogInformation(
                "[REHOST-001] Asset rehosted successfully. asset={AssetType} agentId={AgentId} " +
                "r2Key={R2Key} bytes={Bytes}",
                assetType, agentId, r2Key, totalRead);

            return publicUrl;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "[REHOST-010] RehostSingleAsset threw for asset={AssetType} agentId={AgentId}; skipping",
                assetType, agentId);
            return null;
        }
    }

    /// <summary>
    /// Maps common image content-type values to file extensions.
    /// Returns ".bin" for unknown types so the key is always valid.
    /// </summary>
    internal static string GetExtensionFromContentType(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png"                 => ".png",
            "image/gif"                 => ".gif",
            "image/webp"                => ".webp",
            "image/svg+xml"             => ".svg",
            "image/x-icon"
                or "image/vnd.microsoft.icon" => ".ico",
            "image/avif"                => ".avif",
            _                           => ".bin",
        };
}

/// <summary>
/// Configuration for the RehostAssetsToR2 activity.
/// Bound from the "RehostAssets" configuration section.
/// </summary>
public sealed class RehostAssetsOptions
{
    /// <summary>The R2 bucket name that stores agent assets.</summary>
    public string BucketName { get; set; } = "agent-assets";
}

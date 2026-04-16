using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.Cloudflare;

internal sealed class CloudflareR2Client(
    HttpClient httpClient,
    ILogger<CloudflareR2Client> logger) : ICloudflareR2Client
{
    public async Task PutObjectAsync(string bucket, string key, Stream content, string contentType, CancellationToken ct)
    {
        var url = $"buckets/{bucket}/objects/{Uri.EscapeDataString(key)}";
        var sw = Stopwatch.GetTimestamp();

        using var activity = CloudflareDiagnostics.ActivitySource.StartActivity("cloudflare.r2.put");
        activity?.SetTag("cloudflare.r2.bucket", bucket);
        CloudflareDiagnostics.R2Writes.Add(1);

        try
        {
            using var streamContent = new StreamContent(content);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

            var response = await httpClient.PutAsync(url, streamContent, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("[CF-R2-001] R2 put object failed. Bucket={Bucket} Key={Key} Status={Status} Body={Body}",
                    bucket, key, (int)response.StatusCode, body);
                CloudflareDiagnostics.CallsFailed.Add(1);
                throw new HttpRequestException(
                    $"Cloudflare R2 PUT returned {(int)response.StatusCode} for key '{key}' in bucket '{bucket}'");
            }

            logger.LogDebug("[CF-R2-002] R2 put object succeeded. Bucket={Bucket} Key={Key}",
                bucket, key);
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            logger.LogError(ex, "[CF-R2-009] R2 put object unexpected error. Bucket={Bucket} Key={Key}",
                bucket, key);
            CloudflareDiagnostics.CallsFailed.Add(1);
            throw;
        }
        finally
        {
            CloudflareDiagnostics.CallDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }

    public async Task<Stream?> GetObjectAsync(string bucket, string key, CancellationToken ct)
    {
        var url = $"buckets/{bucket}/objects/{Uri.EscapeDataString(key)}";
        var sw = Stopwatch.GetTimestamp();

        using var activity = CloudflareDiagnostics.ActivitySource.StartActivity("cloudflare.r2.get");
        activity?.SetTag("cloudflare.r2.bucket", bucket);
        CloudflareDiagnostics.R2Reads.Add(1);

        try
        {
            var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogDebug("[CF-R2-003] R2 object not found. Bucket={Bucket} Key={Key}", bucket, key);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("[CF-R2-004] R2 get object failed. Bucket={Bucket} Key={Key} Status={Status} Body={Body}",
                    bucket, key, (int)response.StatusCode, body);
                CloudflareDiagnostics.CallsFailed.Add(1);
                throw new HttpRequestException(
                    $"Cloudflare R2 GET returned {(int)response.StatusCode} for key '{key}' in bucket '{bucket}'");
            }

            logger.LogDebug("[CF-R2-005] R2 get object succeeded. Bucket={Bucket} Key={Key}", bucket, key);
            // Caller is responsible for disposing the stream
            return await response.Content.ReadAsStreamAsync(ct);
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            logger.LogError(ex, "[CF-R2-009] R2 get object unexpected error. Bucket={Bucket} Key={Key}",
                bucket, key);
            CloudflareDiagnostics.CallsFailed.Add(1);
            throw;
        }
        finally
        {
            CloudflareDiagnostics.CallDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }

    public async Task DeleteObjectAsync(string bucket, string key, CancellationToken ct)
    {
        var url = $"buckets/{bucket}/objects/{Uri.EscapeDataString(key)}";
        var sw = Stopwatch.GetTimestamp();

        using var activity = CloudflareDiagnostics.ActivitySource.StartActivity("cloudflare.r2.delete");
        activity?.SetTag("cloudflare.r2.bucket", bucket);
        CloudflareDiagnostics.R2Deletes.Add(1);

        try
        {
            var response = await httpClient.DeleteAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("[CF-R2-006] R2 delete object failed. Bucket={Bucket} Key={Key} Status={Status} Body={Body}",
                    bucket, key, (int)response.StatusCode, body);
                CloudflareDiagnostics.CallsFailed.Add(1);
                throw new HttpRequestException(
                    $"Cloudflare R2 DELETE returned {(int)response.StatusCode} for key '{key}' in bucket '{bucket}'");
            }

            logger.LogDebug("[CF-R2-007] R2 delete object succeeded. Bucket={Bucket} Key={Key}", bucket, key);
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            logger.LogError(ex, "[CF-R2-009] R2 delete object unexpected error. Bucket={Bucket} Key={Key}",
                bucket, key);
            CloudflareDiagnostics.CallsFailed.Add(1);
            throw;
        }
        finally
        {
            CloudflareDiagnostics.CallDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }
}

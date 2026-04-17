using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.Cloudflare;

internal sealed class CloudflareKvClient(
    HttpClient httpClient,
    ILogger<CloudflareKvClient> logger) : ICloudflareKvClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<string?> GetAsync(string namespaceId, string key, CancellationToken ct)
    {
        var url = $"namespaces/{namespaceId}/values/{Uri.EscapeDataString(key)}";
        var sw = Stopwatch.GetTimestamp();

        using var activity = CloudflareDiagnostics.ActivitySource.StartActivity("cloudflare.kv.get");
        activity?.SetTag("cloudflare.kv.namespace_id", namespaceId);
        CloudflareDiagnostics.KvReads.Add(1);

        try
        {
            var response = await httpClient.GetAsync(url, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogDebug("[CF-KV-001] KV key not found. NamespaceId={NamespaceId} Key={Key}",
                    namespaceId, key);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("[CF-KV-002] KV get failed. NamespaceId={NamespaceId} Key={Key} Status={Status} Body={Body}",
                    namespaceId, key, (int)response.StatusCode, body);
                CloudflareDiagnostics.CallsFailed.Add(1);
                throw new HttpRequestException(
                    $"Cloudflare KV GET returned {(int)response.StatusCode} for key '{key}'");
            }

            var value = await response.Content.ReadAsStringAsync(ct);
            logger.LogDebug("[CF-KV-003] KV get succeeded. NamespaceId={NamespaceId} Key={Key}",
                namespaceId, key);
            return value;
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CF-KV-009] KV get unexpected error. NamespaceId={NamespaceId} Key={Key}",
                namespaceId, key);
            CloudflareDiagnostics.CallsFailed.Add(1);
            throw;
        }
        finally
        {
            CloudflareDiagnostics.CallDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }

    public async Task PutAsync(string namespaceId, string key, string value, CancellationToken ct)
    {
        var url = $"namespaces/{namespaceId}/values/{Uri.EscapeDataString(key)}";
        var sw = Stopwatch.GetTimestamp();

        using var activity = CloudflareDiagnostics.ActivitySource.StartActivity("cloudflare.kv.put");
        activity?.SetTag("cloudflare.kv.namespace_id", namespaceId);
        CloudflareDiagnostics.KvWrites.Add(1);

        try
        {
            using var content = new StringContent(value, Encoding.UTF8, "text/plain");
            var response = await httpClient.PutAsync(url, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("[CF-KV-004] KV put failed. NamespaceId={NamespaceId} Key={Key} Status={Status} Body={Body}",
                    namespaceId, key, (int)response.StatusCode, body);
                CloudflareDiagnostics.CallsFailed.Add(1);
                throw new HttpRequestException(
                    $"Cloudflare KV PUT returned {(int)response.StatusCode} for key '{key}'");
            }

            logger.LogDebug("[CF-KV-005] KV put succeeded. NamespaceId={NamespaceId} Key={Key}",
                namespaceId, key);
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            logger.LogError(ex, "[CF-KV-009] KV put unexpected error. NamespaceId={NamespaceId} Key={Key}",
                namespaceId, key);
            CloudflareDiagnostics.CallsFailed.Add(1);
            throw;
        }
        finally
        {
            CloudflareDiagnostics.CallDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }

    public async Task DeleteAsync(string namespaceId, string key, CancellationToken ct)
    {
        var url = $"namespaces/{namespaceId}/values/{Uri.EscapeDataString(key)}";
        var sw = Stopwatch.GetTimestamp();

        using var activity = CloudflareDiagnostics.ActivitySource.StartActivity("cloudflare.kv.delete");
        activity?.SetTag("cloudflare.kv.namespace_id", namespaceId);
        CloudflareDiagnostics.KvDeletes.Add(1);

        try
        {
            var response = await httpClient.DeleteAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("[CF-KV-006] KV delete failed. NamespaceId={NamespaceId} Key={Key} Status={Status} Body={Body}",
                    namespaceId, key, (int)response.StatusCode, body);
                CloudflareDiagnostics.CallsFailed.Add(1);
                throw new HttpRequestException(
                    $"Cloudflare KV DELETE returned {(int)response.StatusCode} for key '{key}'");
            }

            logger.LogDebug("[CF-KV-007] KV delete succeeded. NamespaceId={NamespaceId} Key={Key}",
                namespaceId, key);
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            logger.LogError(ex, "[CF-KV-009] KV delete unexpected error. NamespaceId={NamespaceId} Key={Key}",
                namespaceId, key);
            CloudflareDiagnostics.CallsFailed.Add(1);
            throw;
        }
        finally
        {
            CloudflareDiagnostics.CallDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(string namespaceId, string? prefix, CancellationToken ct)
    {
        var url = $"namespaces/{namespaceId}/keys";
        if (!string.IsNullOrEmpty(prefix))
            url += $"?prefix={Uri.EscapeDataString(prefix)}";

        var sw = Stopwatch.GetTimestamp();

        using var activity = CloudflareDiagnostics.ActivitySource.StartActivity("cloudflare.kv.list");
        activity?.SetTag("cloudflare.kv.namespace_id", namespaceId);
        CloudflareDiagnostics.KvReads.Add(1);

        try
        {
            var response = await httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("[CF-KV-008] KV list keys failed. NamespaceId={NamespaceId} Prefix={Prefix} Status={Status} Body={Body}",
                    namespaceId, prefix, (int)response.StatusCode, body);
                CloudflareDiagnostics.CallsFailed.Add(1);
                throw new HttpRequestException(
                    $"Cloudflare KV LIST returned {(int)response.StatusCode} for namespace '{namespaceId}'");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var envelope = JsonSerializer.Deserialize<KvListResponse>(json, JsonOptions);
            var keys = envelope?.Result?.Select(r => r.Name).ToList() ?? [];

            logger.LogDebug("[CF-KV-003] KV list keys succeeded. NamespaceId={NamespaceId} Prefix={Prefix} Count={Count}",
                namespaceId, prefix, keys.Count);

            return keys.AsReadOnly();
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[CF-KV-010] KV list keys JSON parse error. NamespaceId={NamespaceId}", namespaceId);
            CloudflareDiagnostics.CallsFailed.Add(1);
            throw;
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            logger.LogError(ex, "[CF-KV-009] KV list keys unexpected error. NamespaceId={NamespaceId}", namespaceId);
            CloudflareDiagnostics.CallsFailed.Add(1);
            throw;
        }
        finally
        {
            CloudflareDiagnostics.CallDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }

    // Internal DTOs — mirror Cloudflare KV API JSON shape
    private sealed record KvListResponse(
        [property: JsonPropertyName("result")] List<KvKeyEntry>? Result);

    private sealed record KvKeyEntry(
        [property: JsonPropertyName("name")] string Name);
}

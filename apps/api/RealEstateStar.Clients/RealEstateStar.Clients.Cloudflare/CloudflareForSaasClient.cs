using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.Cloudflare;

internal sealed class CloudflareForSaasClient(
    HttpClient httpClient,
    string zoneId,
    ILogger<CloudflareForSaasClient> logger) : ICloudflareForSaasClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<CustomHostnameResult> CreateCustomHostnameAsync(string hostname, CancellationToken ct)
    {
        var url = $"zones/{zoneId}/custom_hostnames";
        var sw = Stopwatch.GetTimestamp();

        using var activity = CloudflareDiagnostics.ActivitySource.StartActivity("cloudflare.saas.create_hostname");
        activity?.SetTag("cloudflare.saas.hostname", hostname);
        CloudflareDiagnostics.SaasOperations.Add(1);

        try
        {
            var payload = JsonSerializer.Serialize(new { hostname, ssl = new { method = "http", type = "dv" } });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(url, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("[CF-SAAS-001] Create custom hostname failed. Hostname={Hostname} Status={Status} Body={Body}",
                    hostname, (int)response.StatusCode, body);
                CloudflareDiagnostics.CallsFailed.Add(1);
                throw new HttpRequestException(
                    $"Cloudflare ForSaaS POST returned {(int)response.StatusCode} for hostname '{hostname}'");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var envelope = JsonSerializer.Deserialize<CloudflareEnvelope<CustomHostnameDto>>(json, JsonOptions);

            if (envelope?.Result is null)
            {
                logger.LogError("[CF-SAAS-002] Create custom hostname returned null result. Hostname={Hostname}", hostname);
                CloudflareDiagnostics.CallsFailed.Add(1);
                throw new InvalidOperationException(
                    $"Cloudflare ForSaaS create hostname returned null result for '{hostname}'");
            }

            var result = MapToResult(envelope.Result);
            logger.LogInformation("[CF-SAAS-003] Custom hostname created. Hostname={Hostname} Id={Id} Status={Status}",
                result.Hostname, result.Id, result.Status);
            return result;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[CF-SAAS-010] Create custom hostname JSON parse error. Hostname={Hostname}", hostname);
            CloudflareDiagnostics.CallsFailed.Add(1);
            throw;
        }
        catch (Exception ex) when (ex is not HttpRequestException and not InvalidOperationException)
        {
            logger.LogError(ex, "[CF-SAAS-009] Create custom hostname unexpected error. Hostname={Hostname}", hostname);
            CloudflareDiagnostics.CallsFailed.Add(1);
            throw;
        }
        finally
        {
            CloudflareDiagnostics.CallDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }

    public async Task DeleteCustomHostnameAsync(string hostnameId, CancellationToken ct)
    {
        var url = $"zones/{zoneId}/custom_hostnames/{Uri.EscapeDataString(hostnameId)}";
        var sw = Stopwatch.GetTimestamp();

        using var activity = CloudflareDiagnostics.ActivitySource.StartActivity("cloudflare.saas.delete_hostname");
        activity?.SetTag("cloudflare.saas.hostname_id", hostnameId);
        CloudflareDiagnostics.SaasOperations.Add(1);

        try
        {
            var response = await httpClient.DeleteAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("[CF-SAAS-004] Delete custom hostname failed. HostnameId={HostnameId} Status={Status} Body={Body}",
                    hostnameId, (int)response.StatusCode, body);
                CloudflareDiagnostics.CallsFailed.Add(1);
                throw new HttpRequestException(
                    $"Cloudflare ForSaaS DELETE returned {(int)response.StatusCode} for hostnameId '{hostnameId}'");
            }

            logger.LogInformation("[CF-SAAS-005] Custom hostname deleted. HostnameId={HostnameId}", hostnameId);
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            logger.LogError(ex, "[CF-SAAS-009] Delete custom hostname unexpected error. HostnameId={HostnameId}", hostnameId);
            CloudflareDiagnostics.CallsFailed.Add(1);
            throw;
        }
        finally
        {
            CloudflareDiagnostics.CallDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }

    public async Task<CustomHostnameResult?> GetCustomHostnameAsync(string hostnameId, CancellationToken ct)
    {
        var url = $"zones/{zoneId}/custom_hostnames/{Uri.EscapeDataString(hostnameId)}";
        var sw = Stopwatch.GetTimestamp();

        using var activity = CloudflareDiagnostics.ActivitySource.StartActivity("cloudflare.saas.get_hostname");
        activity?.SetTag("cloudflare.saas.hostname_id", hostnameId);
        CloudflareDiagnostics.SaasOperations.Add(1);

        try
        {
            var response = await httpClient.GetAsync(url, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogDebug("[CF-SAAS-006] Custom hostname not found. HostnameId={HostnameId}", hostnameId);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("[CF-SAAS-007] Get custom hostname failed. HostnameId={HostnameId} Status={Status} Body={Body}",
                    hostnameId, (int)response.StatusCode, body);
                CloudflareDiagnostics.CallsFailed.Add(1);
                throw new HttpRequestException(
                    $"Cloudflare ForSaaS GET returned {(int)response.StatusCode} for hostnameId '{hostnameId}'");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var envelope = JsonSerializer.Deserialize<CloudflareEnvelope<CustomHostnameDto>>(json, JsonOptions);

            if (envelope?.Result is null)
            {
                logger.LogDebug("[CF-SAAS-006] Get custom hostname returned null result. HostnameId={HostnameId}", hostnameId);
                return null;
            }

            var result = MapToResult(envelope.Result);
            logger.LogDebug("[CF-SAAS-008] Get custom hostname succeeded. HostnameId={HostnameId} Status={Status}",
                result.Id, result.Status);
            return result;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[CF-SAAS-010] Get custom hostname JSON parse error. HostnameId={HostnameId}", hostnameId);
            CloudflareDiagnostics.CallsFailed.Add(1);
            throw;
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            logger.LogError(ex, "[CF-SAAS-009] Get custom hostname unexpected error. HostnameId={HostnameId}", hostnameId);
            CloudflareDiagnostics.CallsFailed.Add(1);
            throw;
        }
        finally
        {
            CloudflareDiagnostics.CallDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }

    private static CustomHostnameResult MapToResult(CustomHostnameDto dto) =>
        new(dto.Id, dto.Hostname, dto.Status, dto.Ssl?.Status);

    // Internal DTOs — mirror Cloudflare API JSON shape
    private sealed record CloudflareEnvelope<T>(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("result")] T? Result,
        [property: JsonPropertyName("errors")] List<CloudflareError>? Errors);

    private sealed record CloudflareError(
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("message")] string Message);

    private sealed record CustomHostnameDto(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("hostname")] string Hostname,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("ssl")] SslDto? Ssl);

    private sealed record SslDto(
        [property: JsonPropertyName("status")] string? Status);
}

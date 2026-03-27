using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RealEstateStar.Api.Health;

/// <summary>
/// Verifies the OTLP exporter can reach the telemetry backend (Grafana Cloud).
/// Makes a real HTTP POST to the traces endpoint with an empty protobuf payload.
/// A non-2xx response (especially 404 or 401) means telemetry is silently lost.
/// </summary>
public sealed class OtlpExportHealthCheck(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<OtlpExportHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct)
    {
        var endpoint = configuration["Otel:Endpoint"];
        var headers = configuration["Otel:Headers"];

        if (string.IsNullOrWhiteSpace(endpoint) ||
            endpoint.Contains("localhost") ||
            endpoint.Contains("127.0.0.1"))
        {
            return HealthCheckResult.Degraded(
                "OTLP endpoint not configured for production",
                data: new Dictionary<string, object> { ["endpoint"] = endpoint ?? "(null)" });
        }

        if (string.IsNullOrWhiteSpace(headers))
        {
            return HealthCheckResult.Unhealthy(
                "OTLP headers not configured — auth will fail",
                data: new Dictionary<string, object> { ["endpoint"] = endpoint });
        }

        // Probe the traces endpoint with an empty payload to verify connectivity + auth
        var baseUrl = endpoint.TrimEnd('/') + "/";
        var tracesUrl = new Uri(new Uri(baseUrl), "v1/traces").ToString();

        try
        {
            using var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, tracesUrl);
            request.Content = new ByteArrayContent([]);
            request.Content.Headers.ContentType = new("application/x-protobuf");

            // Parse and apply auth headers (format: "Key1=Value1,Key2=Value2")
            foreach (var header in headers.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var eqIndex = header.IndexOf('=');
                if (eqIndex > 0)
                {
                    var key = header[..eqIndex].Trim();
                    var value = header[(eqIndex + 1)..].Trim();
                    request.Headers.TryAddWithoutValidation(key, value);
                }
            }

            var response = await client.SendAsync(request, ct);
            var statusCode = (int)response.StatusCode;

            var data = new Dictionary<string, object>
            {
                ["endpoint"] = tracesUrl,
                ["statusCode"] = statusCode
            };

            // 200, 204, or even 400 (bad payload but auth worked) = connectivity is fine
            // 401, 403 = auth broken
            // 404 = wrong endpoint path
            // 5xx = provider issue
            return statusCode switch
            {
                >= 200 and < 300 => HealthCheckResult.Healthy(
                    $"OTLP endpoint reachable ({statusCode})", data),
                400 => HealthCheckResult.Healthy(
                    "OTLP endpoint reachable (400 expected for empty probe payload)", data),
                401 or 403 => LogAndReturn(HealthCheckResult.Unhealthy(
                    $"[OTEL-HC-001] OTLP auth failed ({statusCode}) — check Otel:Headers secret", data: data)),
                404 => LogAndReturn(HealthCheckResult.Unhealthy(
                    $"[OTEL-HC-002] OTLP endpoint not found ({statusCode}) — wrong URL path", data: data)),
                _ => LogAndReturn(HealthCheckResult.Degraded(
                    $"[OTEL-HC-003] OTLP endpoint returned unexpected status ({statusCode})", data: data))
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[OTEL-HC-004] OTLP endpoint unreachable at {TracesUrl}", tracesUrl);
            return HealthCheckResult.Unhealthy(
                $"[OTEL-HC-004] OTLP endpoint unreachable",
                ex,
                new Dictionary<string, object> { ["endpoint"] = tracesUrl });
        }
    }

    private HealthCheckResult LogAndReturn(HealthCheckResult result)
    {
        logger.LogError("{Description} — endpoint: {Endpoint}",
            result.Description,
            result.Data.GetValueOrDefault("endpoint"));
        return result;
    }
}

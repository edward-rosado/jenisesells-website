using System.Diagnostics;
using System.Net;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.Scraper;

public class ScraperClient(
    IHttpClientFactory httpClientFactory,
    IOptions<ScraperOptions> options,
    ILogger<ScraperClient> logger) : IScraperClient
{
    private long _rateLimitedAtTicks; // 0 = available, >0 = rate-limited at UTC ticks

    public bool IsAvailable
    {
        get
        {
            var ticks = Interlocked.Read(ref _rateLimitedAtTicks);
            if (ticks == 0) return true;
            var resetTicks = TimeSpan.FromSeconds(options.Value.CircuitBreakerResetSeconds).Ticks;
            return (DateTime.UtcNow.Ticks - ticks) > resetTicks;
        }
    }

    public async Task<string?> FetchAsync(string targetUrl, string source, string agentId, CancellationToken ct)
    {
        var rateLimitedAt = Interlocked.Read(ref _rateLimitedAtTicks);
        if (rateLimitedAt != 0)
        {
            var resetTicks = TimeSpan.FromSeconds(options.Value.CircuitBreakerResetSeconds).Ticks;
            if ((DateTime.UtcNow.Ticks - rateLimitedAt) > resetTicks)
            {
                Interlocked.Exchange(ref _rateLimitedAtTicks, 0);
                logger.LogInformation("[SCRAPER-050] Circuit breaker reset after {Seconds}s. Scraper re-enabled.",
                    options.Value.CircuitBreakerResetSeconds);
            }
            else
            {
                logger.LogWarning("[SCRAPER-010] Scraper unavailable (rate limited). Skipping {Source} for agent {AgentId}",
                    source, agentId);
                return null;
            }
        }

        var opts = options.Value;
        var url = $"{opts.BaseUrl}?api_key={opts.ApiKey}&url={Uri.EscapeDataString(targetUrl)}" +
                  (opts.RenderJavaScript ? "&render=true" : "");

        using var activity = ScraperDiagnostics.ActivitySource.StartActivity("scraper.fetch");
        activity?.SetTag("scraper.source", source);
        activity?.SetTag("scraper.agent_id", agentId);

        var sw = Stopwatch.GetTimestamp();
        ScraperDiagnostics.CallsTotal.Add(1, new KeyValuePair<string, object?>("source", source));

        try
        {
            var client = httpClientFactory.CreateClient("ScraperAPI");
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);

            var response = await client.GetAsync(url, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Interlocked.Exchange(ref _rateLimitedAtTicks, DateTime.UtcNow.Ticks);
                ScraperDiagnostics.CallsRateLimited.Add(1, new KeyValuePair<string, object?>("source", source));
                logger.LogError("[SCRAPER-020] ScraperAPI rate limit reached. Disabling scraper. Source: {Source}, Agent: {AgentId}", source, agentId);
                return null;
            }

            response.EnsureSuccessStatusCode();

            ScraperDiagnostics.CallsSucceeded.Add(1, new KeyValuePair<string, object?>("source", source));
            ScraperDiagnostics.CreditsUsed.Add(opts.RenderJavaScript ? 10 : 1);

            var html = await response.Content.ReadAsStringAsync(ct);
            logger.LogInformation("[SCRAPER-001] Fetched {Source} for agent {AgentId}. Size: {Size}KB. Duration: {Duration}ms",
                source, agentId, html.Length / 1024, Stopwatch.GetElapsedTime(sw).TotalMilliseconds);

            return html;
        }
        catch (TaskCanceledException)
        {
            ScraperDiagnostics.CallsFailed.Add(1, new KeyValuePair<string, object?>("source", source));
            logger.LogWarning("[SCRAPER-030] Timeout fetching {Source} for agent {AgentId}", source, agentId);
            return null;
        }
        catch (HttpRequestException ex)
        {
            ScraperDiagnostics.CallsFailed.Add(1, new KeyValuePair<string, object?>("source", source));
            logger.LogError(ex, "[SCRAPER-040] Failed fetching {Source} for agent {AgentId}", source, agentId);
            return null;
        }
        finally
        {
            ScraperDiagnostics.CallDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds,
                new KeyValuePair<string, object?>("source", source));
        }
    }
}

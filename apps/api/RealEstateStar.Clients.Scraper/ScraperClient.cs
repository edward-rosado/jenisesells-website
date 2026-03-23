using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.Scraper;

public class ScraperClient(
    IHttpClientFactory httpClientFactory,
    IOptions<ScraperOptions> options,
    ILogger<ScraperClient> logger) : IScraperClient
{
    private volatile bool _available = true;
    public bool IsAvailable => _available;

    public async Task<string?> FetchAsync(string targetUrl, string source, string agentId, CancellationToken ct)
    {
        if (!_available)
        {
            logger.LogWarning("[SCRAPER-010] Scraper unavailable (rate limited). Skipping {Source} for agent {AgentId}", source, agentId);
            return null;
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
                _available = false;
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

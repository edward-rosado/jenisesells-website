using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.Zillow;

/// <summary>
/// Fetches agent reviews from the Zillow Reviews API via Bridge Interactive's OData endpoint.
///
/// Endpoint: GET /api/v2/OData/reviews/Reviewees?$filter=RevieweeEmail eq '{email}'&amp;$expand=Reviews
/// Auth: Bearer {serverToken}
/// </summary>
public class ZillowReviewsClient(
    IHttpClientFactory httpClientFactory,
    IOptions<ZillowOptions> options,
    ILogger<ZillowReviewsClient> logger) : IZillowReviewsClient
{
    private static readonly ZillowAgentReviews Empty = new([], null, 0, null);

    public bool IsAvailable => !string.IsNullOrWhiteSpace(options.Value.ApiToken);

    public async Task<ZillowAgentReviews> GetReviewsByEmailAsync(string agentEmail, string agentId, CancellationToken ct)
    {
        if (!IsAvailable)
        {
            logger.LogWarning(
                "[ZILLOW-010] Zillow API unavailable (no API token configured). Agent: {AgentId}. " +
                "Impact: no Zillow reviews. Configure Zillow:ApiToken to enable.",
                agentId);
            return Empty;
        }

        var filter = $"RevieweeEmail eq '{EscapeODataString(agentEmail)}'";
        return await FetchRevieweesAsync(filter, agentId, "email", ct);
    }

    public async Task<ZillowAgentReviews> GetReviewsByNameAsync(string agentName, string agentId, CancellationToken ct)
    {
        if (!IsAvailable)
        {
            logger.LogWarning(
                "[ZILLOW-011] Zillow API unavailable (no API token configured). Agent: {AgentId}. " +
                "Impact: no Zillow reviews. Configure Zillow:ApiToken to enable.",
                agentId);
            return Empty;
        }

        var filter = $"contains(RevieweeFullName, '{EscapeODataString(agentName)}')";
        return await FetchRevieweesAsync(filter, agentId, "name", ct);
    }

    private async Task<ZillowAgentReviews> FetchRevieweesAsync(
        string filter, string agentId, string lookupMethod, CancellationToken ct)
    {
        var opts = options.Value;
        var url = $"{opts.BaseUrl}/Reviewees?$filter={Uri.EscapeDataString(filter)}&$expand=Reviews&$top=1";

        using var activity = ZillowDiagnostics.ActivitySource.StartActivity("zillow.reviews.fetch");
        activity?.SetTag("zillow.agent_id", agentId);
        activity?.SetTag("zillow.lookup_method", lookupMethod);

        var sw = Stopwatch.GetTimestamp();
        ZillowDiagnostics.CallsTotal.Add(1,
            new KeyValuePair<string, object?>("method", lookupMethod));

        try
        {
            var client = httpClientFactory.CreateClient("ZillowAPI");
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opts.ApiToken);

            using var response = await client.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                logger.LogError(
                    "[ZILLOW-020] Zillow API returned 401 Unauthorized. Agent: {AgentId}. " +
                    "Reason: API token may be expired or invalid. " +
                    "Impact: no Zillow reviews for this activation.",
                    agentId);
                ZillowDiagnostics.CallsFailed.Add(1);
                return Empty;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.LogWarning(
                    "[ZILLOW-021] Zillow API rate limited (429). Agent: {AgentId}. " +
                    "Impact: no Zillow reviews for this activation. Will retry on next activation.",
                    agentId);
                ZillowDiagnostics.CallsFailed.Add(1);
                return Empty;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "[ZILLOW-022] Zillow API returned HTTP {Status}. Agent: {AgentId}, Lookup: {Method}. " +
                    "Impact: no Zillow reviews.",
                    (int)response.StatusCode, agentId, lookupMethod);
                ZillowDiagnostics.CallsFailed.Add(1);
                return Empty;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = ParseODataResponse(json, agentId);

            ZillowDiagnostics.CallsSucceeded.Add(1,
                new KeyValuePair<string, object?>("method", lookupMethod));
            ZillowDiagnostics.ReviewsFetched.Add(result.TotalReviewCount,
                new KeyValuePair<string, object?>("agent_id", agentId));

            logger.LogInformation(
                "[ZILLOW-001] Fetched {ReviewCount} reviews (avg {AvgRating}) for agent {AgentId} via {Method}. Duration: {Duration}ms",
                result.TotalReviewCount, result.AverageRating?.ToString("F1") ?? "N/A",
                agentId, lookupMethod, Stopwatch.GetElapsedTime(sw).TotalMilliseconds);

            return result;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(
                "[ZILLOW-030] Zillow API timeout for agent {AgentId}, lookup={Method}. " +
                "Impact: no Zillow reviews.",
                agentId, lookupMethod);
            ZillowDiagnostics.CallsFailed.Add(1);
            return Empty;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex,
                "[ZILLOW-031] Zillow API request failed for agent {AgentId}, lookup={Method}. " +
                "Impact: no Zillow reviews.",
                agentId, lookupMethod);
            ZillowDiagnostics.CallsFailed.Add(1);
            return Empty;
        }
        finally
        {
            ZillowDiagnostics.CallDuration.Record(
                Stopwatch.GetElapsedTime(sw).TotalMilliseconds,
                new KeyValuePair<string, object?>("method", lookupMethod));
        }
    }

    /// <summary>
    /// Parses the Bridge Interactive OData JSON response into structured review data.
    ///
    /// Expected shape:
    /// { "value": [{ "RevieweeKey": "...", "Reviews": [{ "ReviewerFullName": "...", "Rating": 5, "Description": "...", "ReviewDate": "..." }] }] }
    /// </summary>
    internal static ZillowAgentReviews ParseODataResponse(string json, string agentId)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("value", out var valueArray) ||
                valueArray.GetArrayLength() == 0)
            {
                return Empty;
            }

            var reviewee = valueArray[0];
            var revieweeKey = reviewee.TryGetProperty("RevieweeKey", out var keyEl)
                ? keyEl.GetString() : null;

            var reviews = new List<Review>();

            if (reviewee.TryGetProperty("Reviews", out var reviewsArray))
            {
                foreach (var r in reviewsArray.EnumerateArray())
                {
                    var text = r.TryGetProperty("Description", out var descEl)
                        ? descEl.GetString() : null;
                    var reviewer = r.TryGetProperty("ReviewerFullName", out var nameEl)
                        ? nameEl.GetString() : null;
                    var rating = r.TryGetProperty("Rating", out var ratingEl) && ratingEl.TryGetInt32(out var rv)
                        ? rv : 5;
                    DateTime? date = r.TryGetProperty("ReviewDate", out var dateEl) &&
                                     DateTime.TryParse(dateEl.GetString(), out var parsedDate)
                        ? parsedDate : null;

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        reviews.Add(new Review(
                            Text: text,
                            Rating: rating,
                            Reviewer: reviewer ?? "Anonymous",
                            Source: "Zillow",
                            Date: date));
                    }
                }
            }

            var avgRating = reviews.Count > 0
                ? reviews.Average(r => r.Rating)
                : (double?)null;

            return new ZillowAgentReviews(reviews, avgRating, reviews.Count, revieweeKey);
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    /// <summary>
    /// Escapes single quotes in OData filter values to prevent injection.
    /// OData uses doubled single quotes as escape: O'Brien → O''Brien
    /// </summary>
    internal static string EscapeODataString(string value) =>
        value.Replace("'", "''");
}

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.GooglePlaces;

/// <summary>
/// Fetches business reviews from the Google Places API (New).
///
/// Two-step flow:
/// 1. Text Search — finds the business by name + location → returns placeId
/// 2. Place Details — fetches reviews by placeId
///
/// Auth: API key passed via X-Goog-Api-Key header (Places API New)
/// </summary>
public class GoogleReviewsClient(
    IHttpClientFactory httpClientFactory,
    IOptions<GooglePlacesOptions> options,
    ILogger<GoogleReviewsClient> logger) : IGoogleReviewsClient
{
    private const string SearchUrl = "https://places.googleapis.com/v1/places:searchText";
    private const string DetailsUrlTemplate = "https://places.googleapis.com/v1/places/{0}";
    private static readonly GooglePlaceReviews Empty = new([], null, 0, null, null, null);

    public bool IsAvailable => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    public async Task<GooglePlaceReviews> GetReviewsAsync(
        string searchQuery, string agentId, CancellationToken ct)
    {
        if (!IsAvailable)
        {
            logger.LogWarning(
                "[GPLACES-010] Google Places API unavailable (no API key). Agent: {AgentId}. " +
                "Configure GooglePlaces:ApiKey to enable Google reviews.",
                agentId);
            return Empty;
        }

        using var activity = GooglePlacesDiagnostics.ActivitySource.StartActivity("google_places.reviews");
        activity?.SetTag("agent_id", agentId);
        var sw = Stopwatch.GetTimestamp();
        GooglePlacesDiagnostics.CallsTotal.Add(1);

        try
        {
            // Step 1: Find the place
            var placeId = await SearchPlaceAsync(searchQuery, agentId, ct);
            if (placeId is null)
            {
                logger.LogInformation(
                    "[GPLACES-020] No Google Place found for query '{Query}', agent {AgentId}.",
                    searchQuery, agentId);
                return Empty;
            }

            // Step 2: Get reviews
            var result = await GetPlaceDetailsAsync(placeId, agentId, ct);

            GooglePlacesDiagnostics.CallsSucceeded.Add(1);
            GooglePlacesDiagnostics.ReviewsFetched.Add(result.TotalReviewCount);

            logger.LogInformation(
                "[GPLACES-001] Fetched {ReviewCount} Google reviews (avg {AvgRating}) for agent {AgentId}. " +
                "Business: {Business}. Duration: {Duration}ms",
                result.TotalReviewCount, result.AverageRating?.ToString("F1") ?? "N/A",
                agentId, result.BusinessName, Stopwatch.GetElapsedTime(sw).TotalMilliseconds);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GooglePlacesDiagnostics.CallsFailed.Add(1);
            logger.LogError(ex,
                "[GPLACES-030] Google Places API failed for agent {AgentId}, query '{Query}'.",
                agentId, searchQuery);
            return Empty;
        }
        finally
        {
            GooglePlacesDiagnostics.CallDuration.Record(Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }

    /// <summary>
    /// Text Search — finds a business by name + location.
    /// Returns the placeId of the first result, or null if not found.
    /// </summary>
    private async Task<string?> SearchPlaceAsync(string query, string agentId, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("GooglePlaces");
        client.Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);

        var requestBody = JsonSerializer.Serialize(new { textQuery = query });

        using var request = new HttpRequestMessage(HttpMethod.Post, SearchUrl)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Goog-Api-Key", options.Value.ApiKey);
        request.Headers.Add("X-Goog-FieldMask", "places.id,places.displayName,places.rating,places.userRatingCount,places.googleMapsUri");

        using var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning(
                "[GPLACES-021] Text Search returned HTTP {Status} for agent {AgentId}. Error: {Error}",
                (int)response.StatusCode, agentId, errorBody.Length > 200 ? errorBody[..200] : errorBody);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("places", out var places) || places.GetArrayLength() == 0)
            return null;

        var first = places[0];
        var placeId = first.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;

        if (placeId is not null)
        {
            var name = first.TryGetProperty("displayName", out var nameEl) &&
                       nameEl.TryGetProperty("text", out var textEl)
                ? textEl.GetString() : null;
            logger.LogInformation(
                "[GPLACES-002] Found Google Place: '{Name}' (id={PlaceId}) for agent {AgentId}",
                name, placeId, agentId);
        }

        return placeId;
    }

    /// <summary>
    /// Place Details — fetches reviews by placeId.
    /// </summary>
    private async Task<GooglePlaceReviews> GetPlaceDetailsAsync(string placeId, string agentId, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("GooglePlaces");
        client.Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);

        var url = string.Format(DetailsUrlTemplate, placeId);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Goog-Api-Key", options.Value.ApiKey);
        request.Headers.Add("X-Goog-FieldMask",
            "id,displayName,rating,userRatingCount,reviews,googleMapsUri");

        using var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "[GPLACES-022] Place Details returned HTTP {Status} for placeId={PlaceId}, agent {AgentId}.",
                (int)response.StatusCode, placeId, agentId);
            return Empty;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParsePlaceDetails(json, placeId);
    }

    /// <summary>
    /// Parses Google Places API (New) Place Details response into structured reviews.
    /// </summary>
    internal static GooglePlaceReviews ParsePlaceDetails(string json, string placeId)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var businessName = root.TryGetProperty("displayName", out var nameEl) &&
                               nameEl.TryGetProperty("text", out var textEl)
                ? textEl.GetString() : null;
            var rating = root.TryGetProperty("rating", out var ratingEl)
                ? ratingEl.GetDouble() : (double?)null;
            var totalCount = root.TryGetProperty("userRatingCount", out var countEl)
                ? countEl.GetInt32() : 0;
            var mapsUrl = root.TryGetProperty("googleMapsUri", out var mapsEl)
                ? mapsEl.GetString() : null;

            var reviews = new List<Review>();

            if (root.TryGetProperty("reviews", out var reviewsArray))
            {
                foreach (var r in reviewsArray.EnumerateArray())
                {
                    var text = r.TryGetProperty("text", out var tEl) &&
                               tEl.TryGetProperty("text", out var ttEl)
                        ? ttEl.GetString() : null;
                    var reviewer = r.TryGetProperty("authorAttribution", out var authEl) &&
                                   authEl.TryGetProperty("displayName", out var dnEl)
                        ? dnEl.GetString() : null;
                    var reviewRating = r.TryGetProperty("rating", out var rrEl)
                        ? rrEl.GetInt32() : 5;
                    DateTime? date = r.TryGetProperty("publishTime", out var ptEl) &&
                                     DateTime.TryParse(ptEl.GetString(), out var parsedDate)
                        ? parsedDate : null;

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        reviews.Add(new Review(
                            Text: text,
                            Rating: reviewRating,
                            Reviewer: reviewer ?? "Anonymous",
                            Source: "Google",
                            Date: date));
                    }
                }
            }

            return new GooglePlaceReviews(reviews, rating, totalCount, placeId, businessName, mapsUrl);
        }
        catch (JsonException)
        {
            return Empty;
        }
    }
}

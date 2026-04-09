using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Shared.Interfaces.External;

/// <summary>
/// Fetches business reviews from the Google Places API.
/// Searches for the agent's brokerage or personal business listing,
/// then retrieves reviews from the Place Details endpoint.
/// </summary>
public interface IGoogleReviewsClient
{
    /// <summary>
    /// Searches for the agent's business on Google Places and returns reviews.
    /// Uses the agent's name + brokerage + location as search terms.
    /// Returns empty if no matching place is found or the API is unavailable.
    /// </summary>
    Task<GooglePlaceReviews> GetReviewsAsync(
        string searchQuery,
        string agentId,
        CancellationToken ct);

    /// <summary>Whether the client is configured with a valid API key.</summary>
    bool IsAvailable { get; }
}

/// <summary>
/// Structured response from the Google Places API containing reviews and business info.
/// </summary>
public sealed record GooglePlaceReviews(
    IReadOnlyList<Review> Reviews,
    double? AverageRating,
    int TotalReviewCount,
    string? PlaceId,
    string? BusinessName,
    string? GoogleMapsUrl);

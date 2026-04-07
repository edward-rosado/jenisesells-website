using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Shared.Interfaces.External;

/// <summary>
/// Fetches agent reviews from the Zillow Reviews API (via Bridge Interactive OData endpoint).
/// Returns structured review data — ratings, text, reviewer names, dates.
/// </summary>
public interface IZillowReviewsClient
{
    /// <summary>
    /// Fetches reviews for an agent by their email address.
    /// Returns an empty list if the agent has no reviews or the API is unavailable.
    /// </summary>
    Task<ZillowAgentReviews> GetReviewsByEmailAsync(string agentEmail, string agentId, CancellationToken ct);

    /// <summary>
    /// Fetches reviews for an agent by their full name (firstname-lastname format).
    /// Falls back to name search when email lookup returns no results.
    /// </summary>
    Task<ZillowAgentReviews> GetReviewsByNameAsync(string agentName, string agentId, CancellationToken ct);

    /// <summary>Whether the client is configured with valid API credentials.</summary>
    bool IsAvailable { get; }
}

/// <summary>
/// Structured response from the Zillow Reviews API containing agent reviews and aggregate stats.
/// </summary>
public sealed record ZillowAgentReviews(
    IReadOnlyList<Review> Reviews,
    double? AverageRating,
    int TotalReviewCount,
    string? RevieweeKey);

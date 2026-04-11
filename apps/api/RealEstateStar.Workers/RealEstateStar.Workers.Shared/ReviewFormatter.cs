using System.Text;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Workers.Shared;

/// <summary>
/// Formats <see cref="Review"/> records for inclusion in Claude prompts.
/// Replaces inline review formatting across Voice, Personality, Coaching, and new consumers.
/// </summary>
public static class ReviewFormatter
{
    /// <summary>
    /// Formats reviews with a given instruction for Claude.
    /// Falls back to profile-embedded reviews when the top-level list is empty.
    /// </summary>
    public static string FormatReviews(
        IReadOnlyList<Review> reviews,
        IReadOnlyList<ThirdPartyProfile> profiles,
        int maxCount,
        string instruction)
    {
        var effectiveReviews = reviews.Count > 0
            ? reviews
            : profiles.SelectMany(p => p.Reviews).ToList();

        if (effectiveReviews.Count == 0)
            return "(No client reviews available)";

        var count = Math.Min(effectiveReviews.Count, maxCount);
        var sb = new StringBuilder();
        sb.AppendLine($"--- Client Reviews ({count} of {effectiveReviews.Count}) ---");
        sb.AppendLine($"INSTRUCTION: {instruction}");
        sb.AppendLine();

        foreach (var review in effectiveReviews.Take(maxCount))
            sb.AppendLine($"[{review.Rating}/5 — {review.Source}] {review.Reviewer}: {review.Text}");

        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>
    /// Formats reviews without profile fallback — for workers that only receive the top-level review list.
    /// </summary>
    public static string FormatReviews(
        IReadOnlyList<Review> reviews,
        int maxCount,
        string instruction)
    {
        if (reviews.Count == 0)
            return "(No client reviews available)";

        var count = Math.Min(reviews.Count, maxCount);
        var sb = new StringBuilder();
        sb.AppendLine($"--- Client Reviews ({count} of {reviews.Count}) ---");
        sb.AppendLine($"INSTRUCTION: {instruction}");
        sb.AppendLine();

        foreach (var review in reviews.Take(maxCount))
            sb.AppendLine($"[{review.Rating}/5 — {review.Source}] {review.Reviewer}: {review.Text}");

        sb.AppendLine();
        return sb.ToString();
    }
}

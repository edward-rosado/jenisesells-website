using System.Text;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Workers.Shared;

/// <summary>
/// Formats pre-extracted <see cref="DocumentExtraction"/> records for inclusion in Claude prompts.
/// Used by workers that benefit from structured transaction data (Coaching, Pipeline, FeeStructure)
/// to avoid re-extracting facts from raw emails.
/// </summary>
public static class ExtractionFormatter
{
    public static string FormatExtractions(IReadOnlyList<DocumentExtraction> extractions, int maxCount = 20)
    {
        if (extractions.Count == 0)
            return "(No pre-extracted transaction data available)";

        var count = Math.Min(extractions.Count, maxCount);
        var sb = new StringBuilder();
        sb.AppendLine($"--- Pre-Extracted Transactions ({count} of {extractions.Count}) ---");

        foreach (var ext in extractions.Take(maxCount))
        {
            sb.AppendLine($"Type: {ext.Type} | Status: {ext.TransactionStatus ?? "Unknown"}");

            if (ext.Property is not null)
            {
                var parts = new[] { ext.Property.Address, ext.Property.City, ext.Property.State, ext.Property.Zip }
                    .Where(p => !string.IsNullOrWhiteSpace(p));
                sb.AppendLine($"  Property: {string.Join(", ", parts)}");
            }

            if (ext.KeyTerms is not null)
            {
                if (ext.KeyTerms.Price is not null)
                    sb.AppendLine($"  Price: {ext.KeyTerms.Price}");
                if (ext.KeyTerms.Commission is not null)
                    sb.AppendLine($"  Commission: {ext.KeyTerms.Commission}");
            }

            if (ext.Clients.Count > 0)
                sb.AppendLine($"  Clients: {string.Join(", ", ext.Clients.Select(c => $"{c.Name} ({c.Role})"))}");

            if (ext.ServiceAreas is { Count: > 0 })
                sb.AppendLine($"  Service Areas: {string.Join(", ", ext.ServiceAreas)}");

            if (ext.Date is not null)
                sb.AppendLine($"  Date: {ext.Date:yyyy-MM-dd}");

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats only extractions with non-null commission data — used by FeeStructure worker.
    /// </summary>
    public static string FormatFeeExtractions(IReadOnlyList<DocumentExtraction> extractions, int maxCount = 15)
    {
        var feeExtractions = extractions
            .Where(e => e.KeyTerms?.Commission is not null)
            .ToList();

        if (feeExtractions.Count == 0)
            return "(No pre-extracted commission data available)";

        return FormatExtractions(feeExtractions, maxCount);
    }
}

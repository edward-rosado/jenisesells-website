using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Models;
using System.Text.RegularExpressions;

namespace RealEstateStar.Workers.Lead.CMA;

public class CompAggregator(
    IEnumerable<ICompSource> sources,
    ILogger<CompAggregator> logger) : ICompAggregator
{
    public async Task<List<Comp>> FetchCompsAsync(CompSearchRequest request, CancellationToken ct)
    {
        var sourceList = sources.ToList();
        logger.LogInformation("[AGG-001] Starting comp fetch from {Count} sources for address={Address}", sourceList.Count, request.Address);

        var tasks = sourceList.Select(s => FetchSafeAsync(s, request, ct));
        var results = await Task.WhenAll(tasks);

        var allComps = results.SelectMany(r => r).ToList();
        logger.LogInformation("[AGG-002] Collected {Total} raw comps from all sources", allComps.Count);

        var deduplicated = Deduplicate(allComps);
        logger.LogInformation("[AGG-003] Deduplicated to {Count} unique comps", deduplicated.Count);

        var ranked = FilterAndRankComps(deduplicated, request.Zip, logger);
        logger.LogInformation("[AGG-005] After FilterAndRankComps: {Count} comps selected", ranked.Count);

        return ranked;
    }

    private async Task<List<Comp>> FetchSafeAsync(ICompSource source, CompSearchRequest request, CancellationToken ct)
    {
        try
        {
            var comps = await source.FetchAsync(request, ct);
            logger.LogInformation("[AGG-002] Source {Source} returned {Count} comps", source.Name, comps.Count);
            return comps;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AGG-004] Source {Source} failed; continuing with remaining sources", source.Name);
            return [];
        }
    }

    // Dedup by normalized address + sale date. When duplicates exist, prefer the source
    // with the lower CompSource enum value (Zillow < RealtorCom < Redfin).
    internal static List<Comp> Deduplicate(List<Comp> comps) =>
        comps
            .GroupBy(c => (NormalizeAddressForDedup(c.Address), c.SaleDate))
            .Select(g => g.OrderBy(c => (int)c.Source).First())
            .ToList();

    internal static string NormalizeAddress(string address) =>
        address.Trim().ToUpperInvariant().Replace(".", "").Replace(",", " ").Replace("  ", " ");

    /// <summary>
    /// Aggressive normalization for dedup: extract only street number + street name + zip.
    /// Strips municipality/township/city suffixes so that "123 Main St, Springfield" and
    /// "123 Main St, Springfield Township" hash to the same key when zip matches.
    /// </summary>
    internal static string NormalizeAddressForDedup(string address)
    {
        // Normalize separators first
        var normalized = address.Trim().ToUpperInvariant()
            .Replace(".", "")
            .Replace(",", " ")
            .Replace("  ", " ")
            .Replace("  ", " ");

        // Extract zip code (5 digits)
        var zipMatch = Regex.Match(normalized, @"\b(\d{5})\b");
        var zip = zipMatch.Success ? zipMatch.Groups[1].Value : "";

        // Extract street number + first two words of street (e.g. "123 MAIN ST")
        var streetMatch = Regex.Match(normalized, @"^(\d+\s+\S+(?:\s+\S+)?)");
        var streetPart = streetMatch.Success ? streetMatch.Groups[1].Value.Trim() : normalized;

        return $"{streetPart}|{zip}";
    }

    /// <summary>
    /// Filters and ranks comps by geographic proximity to the subject property zip code,
    /// deprioritizes comps beyond 10 miles, and returns the best 5.
    /// </summary>
    internal static List<Comp> FilterAndRankComps(List<Comp> comps, string? subjectZip, ILogger? logger = null)
    {
        if (comps.Count == 0)
            return comps;

        var subjectZipNorm = subjectZip?.Trim() ?? "";
        var subjectPrefix = subjectZipNorm.Length >= 3 ? subjectZipNorm[..3] : "";

        var scored = comps.Select(c =>
        {
            var compZip = ExtractZip(c.Address);
            int zipScore;

            if (!string.IsNullOrEmpty(subjectZipNorm) && compZip == subjectZipNorm)
                zipScore = 100;
            else if (!string.IsNullOrEmpty(subjectPrefix) && compZip.Length >= 3 && compZip[..3] == subjectPrefix)
                zipScore = 50;
            else
                zipScore = 10;

            // Deprioritize comps beyond 10 miles
            if (c.DistanceMiles > 10)
            {
                if (logger is not null)
                    logger.LogWarning("[AGG-006] Comp at {Address} is {Distance:F1} miles away — deprioritizing", c.Address, c.DistanceMiles);
                zipScore = Math.Min(zipScore, 5);
            }

            return (Comp: c, ZipScore: zipScore);
        }).ToList();

        // If >= 5 comps with same zip score of 100, use only those
        var sameZip = scored.Where(s => s.ZipScore == 100).ToList();
        if (sameZip.Count >= 5)
            return sameZip
                .OrderByDescending(s => s.Comp.Correlation ?? 0)
                .Take(5)
                .Select(s => s.Comp)
                .ToList();

        // Otherwise sort by zip score desc, then correlation desc, take top 5
        return scored
            .OrderByDescending(s => s.ZipScore)
            .ThenByDescending(s => s.Comp.Correlation ?? 0)
            .Take(5)
            .Select(s => s.Comp)
            .ToList();
    }

    /// <summary>Extracts a 5-digit zip code from an address string, or returns empty string.</summary>
    internal static string ExtractZip(string address)
    {
        var match = Regex.Match(address, @"\b(\d{5})\b");
        return match.Success ? match.Groups[1].Value : "";
    }
}

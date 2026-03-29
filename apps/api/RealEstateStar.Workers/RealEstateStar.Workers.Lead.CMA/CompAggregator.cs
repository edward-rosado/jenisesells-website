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

        var ranked = FilterAndRankComps(deduplicated, request.Zip, logger, request);
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

    // Dedup by normalized address (street+zip only). SaleDate is intentionally excluded
    // because RentCast can return the same physical property with different municipality names
    // AND slightly different sale dates for the same underlying transaction.
    // When duplicates exist, prefer the source with the lower CompSource enum value.
    //
    // Two-pass strategy:
    //   Pass 1: street+zip dedup (handles same-zip duplicates)
    //   Pass 2: street-only dedup (handles same-unit-different-zip duplicates,
    //           e.g., "49 Middlesex Rd Unit B, Matawan 07747" vs
    //                 "49 Middlesex Rd Unit B, Old Bridge 08857")
    internal static List<Comp> Deduplicate(List<Comp> comps)
    {
        // Pass 1: Exact dedup by street+zip
        var pass1 = comps
            .GroupBy(c => NormalizeAddressForDedup(c.Address))
            .Select(g => g.OrderBy(c => (int)c.Source).First())
            .ToList();

        // Pass 2: Street-only dedup (strips city, state, zip)
        return pass1
            .GroupBy(c => NormalizeStreetOnly(c.Address))
            .Select(g => g.OrderBy(c => (int)c.Source).First())
            .ToList();
    }

    internal static string NormalizeAddress(string address) =>
        address.Trim().ToUpperInvariant().Replace(".", "").Replace(",", " ").Replace("  ", " ");

    /// <summary>
    /// Aggressive normalization for dedup: extract only street number + street name + zip.
    /// Strips municipality/township/city suffixes so that "123 Main St, Springfield" and
    /// "123 Main St, Springfield Township" hash to the same key when zip matches.
    /// </summary>
    internal static string NormalizeAddressForDedup(string address)
    {
        var normalized = address.Trim().ToUpperInvariant()
            .Replace(".", "")
            .Replace(",", " ")
            .Replace("  ", " ")
            .Replace("  ", " ");

        // Strip municipality suffixes that cause false non-matches
        foreach (var suffix in new[] { " TOWNSHIP", " TWP", " BOROUGH", " BORO", " VILLAGE", " CITY" })
            normalized = normalized.Replace(suffix, "");

        var zipMatch = Regex.Match(normalized, @"\b(\d{5})\b");
        var zip = zipMatch.Success ? zipMatch.Groups[1].Value : "";

        var streetMatch = Regex.Match(normalized, @"^(\d+\s+\S+(?:\s+\S+)?)");
        var streetPart = streetMatch.Success ? streetMatch.Groups[1].Value.Trim() : normalized;

        return $"{streetPart}|{zip}";
    }

    /// <summary>
    /// Street-only normalization for cross-zip dedup.
    /// Extracts the street number + full street name (including unit), stripping
    /// city, state, and zip. Used in pass 2 of <see cref="Deduplicate"/>.
    /// Example: "49 Middlesex Rd Unit B, Matawan, NJ 07747" → "49 MIDDLESEX RD UNIT B"
    /// Example: "49 Middlesex Rd Unit B, Old Bridge, NJ 08857" → "49 MIDDLESEX RD UNIT B"
    /// Strategy: truncate at the 2-letter state code followed by a 5-digit zip.
    /// After truncation, strip the city (everything after the last street/unit token
    /// that is NOT a US state abbreviation). Since we truncate at "ST 00000", we
    /// then remove the state-abbreviated city by stripping the trailing "[A-Z]{2}" token.
    /// </summary>
    internal static string NormalizeStreetOnly(string address)
    {
        var normalized = address.Trim().ToUpperInvariant()
            .Replace(".", "")
            .Replace(",", " ")
            .Replace("  ", " ")
            .Replace("  ", " ");

        // Strip municipality suffixes
        foreach (var suffix in new[] { " TOWNSHIP", " TWP", " BOROUGH", " BORO", " VILLAGE", " CITY" })
            normalized = normalized.Replace(suffix, "");

        // Collapse any double spaces introduced by suffix stripping
        while (normalized.Contains("  "))
            normalized = normalized.Replace("  ", " ");

        // Truncate at "ST 00000" — the 2-letter state abbreviation followed by a 5-digit zip.
        // This reliably marks the boundary between address and city/state/zip.
        var stateZipMatch = Regex.Match(normalized, @"^(.*?)\s+[A-Z]{2}\s+\d{5}");
        if (stateZipMatch.Success)
        {
            var beforeStateZip = stateZipMatch.Groups[1].Value.Trim();

            // Strip trailing state abbreviation token (last 2-letter all-caps word that is a state)
            // e.g., "49 MIDDLESEX RD UNIT B MATAWAN" — "MATAWAN" is the city, strip it
            // e.g., "49 MIDDLESEX RD UNIT B OLD BRIDGE" — "OLD BRIDGE" is multi-word city, strip last word
            // Approach: strip tokens from the end until we hit a recognized street/unit token
            // Simplest reliable approach: strip all trailing tokens that do NOT look like
            // street suffixes, unit designators, or alphanumeric unit identifiers (single/few chars)
            var streetSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ST", "AVE", "BLVD", "DR", "LN", "RD", "CT", "PL", "WAY", "TER", "TERR",
                "CIR", "LOOP", "PATH", "PKWY", "HWY", "EXPY", "FWY", "RTE", "ROUTE",
                "UNIT", "APT", "SUITE", "STE", "#",
                "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K",
                "1A", "1B", "2A", "2B", "3A", "3B"
            };

            // Remove city tokens by stripping words from the end that are longer than 2 chars
            // and not known street suffixes/unit designators.
            // We stop as soon as we see a known street suffix or a short unit letter.
            var tokens = beforeStateZip.Split(' ');
            var streetEnd = tokens.Length;
            for (var i = tokens.Length - 1; i >= 1; i--)
            {
                var token = tokens[i];
                if (streetSuffixes.Contains(token) ||
                    (token.Length <= 2 && !Regex.IsMatch(token, @"^[A-Z]{2}$")))
                {
                    // This is a street suffix or unit identifier — stop here
                    streetEnd = i + 1;
                    break;
                }
                // This looks like a city word — continue stripping
                streetEnd = i;
            }

            return string.Join(" ", tokens[..streetEnd]).Trim();
        }

        // Fallback: just take the first two tokens (number + street name)
        var streetMatch = Regex.Match(normalized, @"^(\d+\s+\S+(?:\s+\S+)?)");
        return streetMatch.Success ? streetMatch.Groups[1].Value.Trim() : normalized;
    }

    /// <summary>
    /// Filters and ranks comps by geographic proximity to the subject property zip code,
    /// deprioritizes comps beyond 10 miles, removes the subject property itself,
    /// removes price/sqft outliers using the IQR method, and returns the best 5.
    /// </summary>
    internal static List<Comp> FilterAndRankComps(
        List<Comp> comps,
        string? subjectZip,
        ILogger? logger = null,
        CompSearchRequest? request = null)
    {
        if (comps.Count == 0)
            return comps;

        // --- Problem 1: Exclude the subject property itself ---
        // RentCast frequently returns the subject's own recent sale as Comp 1 at 0.0 miles.
        // request.Address is the street portion only (e.g. "308 Myrtle St"), so we check
        // whether the comp's normalized full address *starts with* or *contains* the
        // normalized subject street — in addition to the distance check.
        var subjectStreet = NormalizeAddress(request?.Address ?? "");
        var beforeSubjectExclusion = comps.Count;
        comps = comps.Where(c =>
        {
            if (c.DistanceMiles <= 0.01)
                return false; // At the exact same location — always exclude

            // If a subject street was provided, also exclude by address match
            if (subjectStreet.Length > 0)
            {
                var compNorm = NormalizeAddress(c.Address);
                if (compNorm.StartsWith(subjectStreet, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }).ToList();

        if (comps.Count < beforeSubjectExclusion)
            logger?.LogInformation(
                "[AGG-008] Excluded {Count} subject property comp(s) at {Address}",
                beforeSubjectExclusion - comps.Count, subjectStreet);

        if (comps.Count == 0)
            return comps;

        // --- Problem 3: Remove price/sqft outliers using IQR method ---
        if (comps.Count >= 3)
        {
            var pricesPerSqft = comps
                .Where(c => c.Sqft > 0)
                .Select(c => (double)(c.SalePrice / c.Sqft))
                .OrderBy(p => p)
                .ToList();

            if (pricesPerSqft.Count >= 3)
            {
                var q1 = pricesPerSqft[pricesPerSqft.Count / 4];
                var q3 = pricesPerSqft[3 * pricesPerSqft.Count / 4];
                var iqr = q3 - q1;
                var lowerBound = q1 - 1.5 * iqr;
                var upperBound = q3 + 1.5 * iqr;

                var beforeCount = comps.Count;
                comps = comps.Where(c =>
                {
                    if (c.Sqft <= 0) return true; // Can't evaluate without sqft, keep
                    var ppsf = (double)(c.SalePrice / c.Sqft);
                    return ppsf >= lowerBound && ppsf <= upperBound;
                }).ToList();

                if (comps.Count < beforeCount)
                    logger?.LogInformation(
                        "[AGG-007] Removed {Count} outlier comp(s) (price/sqft outside IQR bounds ${Low:F0}-${High:F0})",
                        beforeCount - comps.Count, lowerBound, upperBound);
            }
        }

        if (comps.Count == 0)
            return comps;

        // --- Zip-based proximity scoring and ranking ---
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

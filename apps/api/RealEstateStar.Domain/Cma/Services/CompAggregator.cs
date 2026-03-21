using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Models;

namespace RealEstateStar.Domain.Cma.Services;

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

        return deduplicated;
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
            .GroupBy(c => (NormalizeAddress(c.Address), c.SaleDate))
            .Select(g => g.OrderBy(c => (int)c.Source).First())
            .ToList();

    internal static string NormalizeAddress(string address) =>
        address.Trim().ToUpperInvariant().Replace(".", "").Replace(",", " ").Replace("  ", " ");
}

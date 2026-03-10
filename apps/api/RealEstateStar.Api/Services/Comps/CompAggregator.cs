using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Comps;

public class CompAggregator(IEnumerable<ICompSource> sources, ILogger<CompAggregator>? logger = null)
{
    public virtual async Task<List<Comp>> FetchCompsAsync(CompSearchRequest request, CancellationToken ct)
    {
        var tasks = sources.Select(source => FetchFromSourceAsync(source, request, ct));
        var results = await Task.WhenAll(tasks);

        var allComps = results.SelectMany(r => r).ToList();

        return Deduplicate(allComps);
    }

    private async Task<List<Comp>> FetchFromSourceAsync(
        ICompSource source, CompSearchRequest request, CancellationToken ct)
    {
        try
        {
            return await source.FetchAsync(request, ct);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Comp source {SourceName} failed; continuing with other sources", source.Name);
            return [];
        }
    }

    private static List<Comp> Deduplicate(List<Comp> comps) =>
        comps
            .GroupBy(c => (NormalizeAddress(c.Address), c.SaleDate))
            .Select(g => g.OrderBy(c => (int)c.Source).First())
            .ToList();

    private static string NormalizeAddress(string address) =>
        address.Trim().ToUpperInvariant().Replace(".", "").Replace(",", "");
}

using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Comps;

public class CompAggregator(IEnumerable<ICompSource> sources, ILogger<CompAggregator>? logger = null)
{
    public virtual async Task<List<Comp>> FetchCompsAsync(
        string address, string city, string state, string zip,
        int? beds, int? baths, int? sqft,
        CancellationToken ct)
    {
        var tasks = sources.Select(source => FetchFromSourceAsync(source, address, city, state, zip, beds, baths, sqft, ct));
        var results = await Task.WhenAll(tasks);

        var allComps = results.SelectMany(r => r).ToList();

        return Deduplicate(allComps);
    }

    private async Task<List<Comp>> FetchFromSourceAsync(
        ICompSource source, string address, string city, string state, string zip,
        int? beds, int? baths, int? sqft,
        CancellationToken ct)
    {
        try
        {
            return await source.FetchAsync(address, city, state, zip, beds, baths, sqft, ct);
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

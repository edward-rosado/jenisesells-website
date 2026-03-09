using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Comps;

public class ZillowCompSource(HttpClient httpClient, ILogger<ZillowCompSource>? logger = null) : ICompSource
{
    public string Name => "Zillow";

    public async Task<List<Comp>> FetchAsync(
        string address, string city, string state, string zip,
        int? beds, int? baths, int? sqft,
        CancellationToken ct)
    {
        var slug = $"{address.Replace(' ', '-')}-{city}-{state}-{zip}".ToLowerInvariant();
        var url = $"https://www.zillow.com/homedetails/{slug}";

        logger?.LogInformation("Fetching Zillow comps from {Url}", url);

        var html = await httpClient.GetStringAsync(url, ct);

        return ParseComps(html);
    }

    internal static List<Comp> ParseComps(string html) => [];
}

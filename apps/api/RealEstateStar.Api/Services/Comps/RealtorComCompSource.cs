using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Comps;

public class RealtorComCompSource(HttpClient httpClient, ILogger<RealtorComCompSource>? logger = null) : ICompSource
{
    public string Name => "Realtor.com";

    public async Task<List<Comp>> FetchAsync(
        string address, string city, string state, string zip,
        int? beds, int? baths, int? sqft,
        CancellationToken ct = default)
    {
        var slug = $"{address.Replace(' ', '-')}_{city}_{state}_{zip}".ToLowerInvariant();
        var url = $"https://www.realtor.com/realestateandhomes-detail/{slug}";

        logger?.LogInformation("Fetching Realtor.com comps from {Url}", url);

        var html = await httpClient.GetStringAsync(url, ct);

        return ParseComps(html);
    }

    internal static List<Comp> ParseComps(string html) => [];
}

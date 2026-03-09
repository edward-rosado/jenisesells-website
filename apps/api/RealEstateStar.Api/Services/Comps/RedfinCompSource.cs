using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Comps;

public class RedfinCompSource(HttpClient httpClient, ILogger<RedfinCompSource>? logger = null) : ICompSource
{
    public string Name => "Redfin";

    public async Task<List<Comp>> FetchAsync(
        string address, string city, string state, string zip,
        int? beds, int? baths, int? sqft)
    {
        var slug = $"{state}/{city}/{address.Replace(' ', '-')}-{zip}".ToLowerInvariant();
        var url = $"https://www.redfin.com/{slug}";

        logger?.LogInformation("Fetching Redfin comps from {Url}", url);

        var html = await httpClient.GetStringAsync(url);

        return ParseComps(html);
    }

    internal static List<Comp> ParseComps(string html) => [];
}

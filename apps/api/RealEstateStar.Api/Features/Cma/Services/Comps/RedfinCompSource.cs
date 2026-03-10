using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Features.Cma;

namespace RealEstateStar.Api.Features.Cma.Services.Comps;

public class RedfinCompSource(HttpClient httpClient, ILogger<RedfinCompSource>? logger = null) : ICompSource
{
    public string Name => "Redfin";

    public async Task<List<Comp>> FetchAsync(CompSearchRequest request, CancellationToken ct)
    {
        var slug = $"{request.State}/{request.City}/{request.Address.Replace(' ', '-')}-{request.Zip}".ToLowerInvariant();
        var url = $"https://www.redfin.com/{slug}";

        logger?.LogInformation("Fetching Redfin comps from {Url}", url);

        var html = await httpClient.GetStringAsync(url, ct);

        return ParseComps(html);
    }

    internal static List<Comp> ParseComps(string html) => [];
}

using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Features.Cma;

namespace RealEstateStar.Api.Features.Cma.Services.Comps;

public class RealtorComCompSource(HttpClient httpClient, ILogger<RealtorComCompSource>? logger = null) : ICompSource
{
    public string Name => "Realtor.com";

    public async Task<List<Comp>> FetchAsync(CompSearchRequest request, CancellationToken ct)
    {
        var slug = $"{request.Address.Replace(' ', '-')}_{request.City}_{request.State}_{request.Zip}".ToLowerInvariant();
        var url = $"https://www.realtor.com/realestateandhomes-detail/{slug}";

        logger?.LogInformation("Fetching Realtor.com comps from {Url}", url);

        var html = await httpClient.GetStringAsync(url, ct);

        return ParseComps(html);
    }

    internal static List<Comp> ParseComps(string html) => [];
}

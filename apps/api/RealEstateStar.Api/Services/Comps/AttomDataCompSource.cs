using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Features.Cma;

namespace RealEstateStar.Api.Services.Comps;

public class AttomDataCompSource(HttpClient httpClient, string apiKey, ILogger<AttomDataCompSource>? logger = null) : ICompSource
{
    public string Name => "ATTOM Data";

    public async Task<List<Comp>> FetchAsync(CompSearchRequest request, CancellationToken ct)
    {
        var encodedAddress = Uri.EscapeDataString(request.Address);
        var url = $"https://api.gateway.attomdata.com/propertyapi/v1.0.0/sale/snapshot?" +
                  $"address1={encodedAddress}&address2={Uri.EscapeDataString($"{request.City}, {request.State} {request.Zip}")}";

        logger?.LogInformation("Fetching ATTOM Data comps from {Url}", url);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        httpRequest.Headers.Add("apikey", apiKey);
        httpRequest.Headers.Add("Accept", "application/json");

        var response = await httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        return ParseResponse(await response.Content.ReadAsStringAsync(ct));
    }

    internal static List<Comp> ParseResponse(string json) => [];
}

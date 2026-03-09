using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Comps;

public class AttomDataCompSource(HttpClient httpClient, string apiKey, ILogger<AttomDataCompSource>? logger = null) : ICompSource
{
    public string Name => "ATTOM Data";

    public async Task<List<Comp>> FetchAsync(
        string address, string city, string state, string zip,
        int? beds, int? baths, int? sqft,
        CancellationToken ct)
    {
        var encodedAddress = Uri.EscapeDataString(address);
        var url = $"https://api.gateway.attomdata.com/propertyapi/v1.0.0/sale/snapshot?" +
                  $"address1={encodedAddress}&address2={Uri.EscapeDataString($"{city}, {state} {zip}")}";

        logger?.LogInformation("Fetching ATTOM Data comps from {Url}", url);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("apikey", apiKey);
        request.Headers.Add("Accept", "application/json");

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return ParseResponse(await response.Content.ReadAsStringAsync(ct));
    }

    internal static List<Comp> ParseResponse(string json) => [];
}

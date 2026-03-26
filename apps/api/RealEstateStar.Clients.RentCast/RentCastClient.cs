using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Shared;

namespace RealEstateStar.Clients.RentCast;

internal sealed class RentCastClient(
    IHttpClientFactory httpClientFactory,
    IOptions<RentCastOptions> options,
    ILogger<RentCastClient> logger) : IRentCastClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<RentCastValuation?> GetValuationAsync(string address, CancellationToken ct)
    {
        var opts = options.Value;
        var encoded = Uri.EscapeDataString(address);
        var url = $"{opts.BaseUrl}?address={encoded}";

        using var activity = RentCastDiagnostics.ActivitySource.StartActivity("rentcast.get_valuation");
        activity?.SetTag("rentcast.address_length", address.Length);

        var sw = Stopwatch.GetTimestamp();
        RentCastDiagnostics.CallsTotal.Add(1);

        try
        {
            var client = httpClientFactory.CreateClient("RentCast");

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Api-Key", opts.ApiKey);

            var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogError(
                    "[RENTCAST-010] RentCast API returned {StatusCode} for address. Body: {Body}",
                    (int)response.StatusCode, body);
                RentCastDiagnostics.CallsFailed.Add(1);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var dto = JsonSerializer.Deserialize<RentCastApiResponse>(json, JsonOptions);

            if (dto is null)
            {
                logger.LogError("[RENTCAST-010] RentCast response deserialized to null");
                RentCastDiagnostics.CallsFailed.Add(1);
                return null;
            }

            var valuation = MapToValuation(dto);

            logger.LogInformation(
                "[RENTCAST-001] RentCast returned valuation. Price={Price:C}, Comps={CompCount}",
                valuation.Price, valuation.Comparables.Count);

            RentCastDiagnostics.CompsReturned.Record(valuation.Comparables.Count);

            return valuation;
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("[RENTCAST-010] RentCast request timed out or was cancelled");
            RentCastDiagnostics.CallsFailed.Add(1);
            return null;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "[RENTCAST-010] RentCast HTTP request failed");
            RentCastDiagnostics.CallsFailed.Add(1);
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[RENTCAST-010] Failed to deserialize RentCast response");
            RentCastDiagnostics.CallsFailed.Add(1);
            return null;
        }
        finally
        {
            RentCastDiagnostics.CallDuration.Record(
                Stopwatch.GetElapsedTime(sw).TotalMilliseconds);
        }
    }

    private static RentCastValuation MapToValuation(RentCastApiResponse dto) =>
        new()
        {
            Price = dto.Price,
            PriceRangeLow = dto.PriceRangeLow,
            PriceRangeHigh = dto.PriceRangeHigh,
            Comparables = (dto.Comparables ?? [])
                .Select(c => new RentCastComp
                {
                    FormattedAddress = c.FormattedAddress,
                    PropertyType = c.PropertyType,
                    Bedrooms = c.Bedrooms,
                    Bathrooms = c.Bathrooms,
                    SquareFootage = c.SquareFootage,
                    Price = c.Price,
                    ListedDate = c.ListedDate,
                    RemovedDate = c.RemovedDate,
                    DaysOnMarket = c.DaysOnMarket,
                    Distance = c.Distance,
                    Correlation = c.Correlation,
                    Status = c.Status
                })
                .ToList()
                .AsReadOnly()
        };

    // Internal DTOs — mirror RentCast JSON shape, not exposed outside this file
    private record RentCastApiResponse(
        [property: JsonPropertyName("price")] decimal Price,
        [property: JsonPropertyName("priceRangeLow")] decimal PriceRangeLow,
        [property: JsonPropertyName("priceRangeHigh")] decimal PriceRangeHigh,
        [property: JsonPropertyName("comparables")] List<RentCastApiComp>? Comparables);

    private record RentCastApiComp(
        [property: JsonPropertyName("formattedAddress")] string FormattedAddress,
        [property: JsonPropertyName("propertyType")] string? PropertyType,
        [property: JsonPropertyName("bedrooms")] int? Bedrooms,
        [property: JsonPropertyName("bathrooms")] decimal? Bathrooms,
        [property: JsonPropertyName("squareFootage")] int? SquareFootage,
        [property: JsonPropertyName("price")] decimal? Price,
        [property: JsonPropertyName("listedDate")] DateTimeOffset? ListedDate,
        [property: JsonPropertyName("removedDate")] DateTimeOffset? RemovedDate,
        [property: JsonPropertyName("daysOnMarket")] int? DaysOnMarket,
        [property: JsonPropertyName("distance")] double? Distance,
        [property: JsonPropertyName("correlation")] double? Correlation,
        [property: JsonPropertyName("status")] string? Status);
}

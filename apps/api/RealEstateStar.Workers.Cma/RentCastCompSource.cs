using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Models;

namespace RealEstateStar.Workers.Cma;

public class RentCastCompSource(
    IRentCastClient rentCastClient,
    ILogger<RentCastCompSource> logger) : ICompSource
{
    private static readonly HashSet<string> ExcludedPropertyTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Multi Family",
            "Apartment",
            "Condominium",
            "Townhouse"
        };

    public string Name => "RentCast";

    public async Task<List<Comp>> FetchAsync(CompSearchRequest request, CancellationToken ct)
    {
        var fullAddress = $"{request.Address}, {request.City}, {request.State} {request.Zip}";
        logger.LogInformation("[COMP-001] Fetching comps from RentCast for address={Address}",
            fullAddress);

        var valuation = await rentCastClient.GetValuationAsync(fullAddress, ct);
        if (valuation is null)
        {
            logger.LogWarning("[COMP-004] RentCast returned null for address={Address}",
                fullAddress);
            return [];
        }

        var comps = MapComps(valuation.Comparables, request, logger);
        logger.LogInformation("[COMP-003] Mapped {Count} valid comps from RentCast", comps.Count);
        return comps;
    }

    internal static List<Comp> MapComps(
        IReadOnlyList<RentCastComp> comparables,
        CompSearchRequest request,
        ILogger logger)
    {
        var result = new List<Comp>();

        foreach (var rc in comparables)
        {
            // Skip if price invalid
            if (rc.Price is null or <= 0)
                continue;

            // Skip if square footage invalid
            if (rc.SquareFootage is null or <= 0)
                continue;

            // Skip if address blank
            if (string.IsNullOrWhiteSpace(rc.FormattedAddress))
                continue;

            // Resolve sale date: Inactive status → use RemovedDate, otherwise ListedDate
            DateTimeOffset? resolvedDate = rc.Status?.Equals("Inactive",
                StringComparison.OrdinalIgnoreCase) == true
                ? rc.RemovedDate
                : rc.ListedDate;

            if (resolvedDate is null)
                continue;

            var saleDate = DateOnly.FromDateTime(resolvedDate.Value.UtcDateTime);

            // Property type filter: when subject has SqFt (single-family indicator),
            // exclude known multi-unit property types. Null type is kept (permissive).
            if (request.SqFt.HasValue
                && rc.PropertyType is not null
                && ExcludedPropertyTypes.Contains(rc.PropertyType))
            {
                continue;
            }

            result.Add(new Comp
            {
                Address = rc.FormattedAddress,
                SalePrice = rc.Price.Value,
                SaleDate = saleDate,
                Beds = rc.Bedrooms ?? 0,
                Baths = (int)Math.Round(rc.Bathrooms ?? 0, MidpointRounding.AwayFromZero),
                Sqft = rc.SquareFootage.Value,
                DaysOnMarket = rc.DaysOnMarket,
                DistanceMiles = rc.Distance ?? 0.0,
                Source = CompSource.RentCast
            });
        }

        return result;
    }
}

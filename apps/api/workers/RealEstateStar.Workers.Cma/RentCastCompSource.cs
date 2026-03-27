using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Models;

namespace RealEstateStar.Workers.Cma;

public class RentCastCompSource(
    IRentCastClient rentCastClient,
    ILogger<RentCastCompSource> logger) : ICompSource
{
    private static readonly HashSet<string> SingleFamilyExclusions =
        new(StringComparer.OrdinalIgnoreCase) { "Condominium", "Apartment", "Multi-Family", "Multi Family", "Townhouse" };

    private static readonly HashSet<string> CondoExclusions =
        new(StringComparer.OrdinalIgnoreCase) { "Single Family", "Multi-Family", "Multi Family" };

    private static readonly HashSet<string> SingleFamilyTypes =
        new(StringComparer.OrdinalIgnoreCase) { "Single Family" };

    private static readonly HashSet<string> CondoTypes =
        new(StringComparer.OrdinalIgnoreCase) { "Condominium", "Condo", "Apartment" };

    public string Name => "RentCast";

    /// <summary>
    /// The valuation returned by the most recent <see cref="FetchAsync"/> call.
    /// Populated before comps are returned so callers can read SubjectProperty without a second API call.
    /// </summary>
    public RentCastValuation? LastValuation { get; private set; }

    public async Task<List<Comp>> FetchAsync(CompSearchRequest request, CancellationToken ct)
    {
        var fullAddress = $"{request.Address}, {request.City}, {request.State} {request.Zip}";
        logger.LogInformation("[COMP-001] Fetching comps from RentCast for address={Address}",
            fullAddress);

        var valuation = await rentCastClient.GetValuationAsync(fullAddress, ct);
        LastValuation = valuation;

        if (valuation is null)
        {
            logger.LogWarning("[COMP-004] RentCast returned null for address={Address}",
                fullAddress);
            return [];
        }

        var subjectType = valuation.SubjectProperty?.PropertyType;
        var comps = MapComps(valuation.Comparables, request, subjectType, logger);
        logger.LogInformation("[COMP-003] Mapped {Count} valid comps from RentCast (subject type: {SubjectType})",
            comps.Count, subjectType ?? "unknown");
        return comps;
    }

    internal static List<Comp> MapComps(
        IReadOnlyList<RentCastComp> comparables,
        CompSearchRequest request,
        string? subjectPropertyType,
        ILogger logger,
        DateTimeOffset? today = null)
    {
        var effectiveToday = today ?? DateTimeOffset.UtcNow;
        var sixMonthsAgo = effectiveToday.AddMonths(-6);

        var recent = new List<(RentCastComp Rc, DateOnly SaleDate)>();
        var older = new List<(RentCastComp Rc, DateOnly SaleDate)>();

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

            // Property type filter: match comps to subject property type.
            // If subject is Single Family → exclude condos/apartments/multi-family.
            // If subject is Condo → exclude single-family/multi-family.
            // If subject type is unknown → keep all (permissive).
            if (rc.PropertyType is not null && subjectPropertyType is not null)
            {
                if (SingleFamilyTypes.Contains(subjectPropertyType) && SingleFamilyExclusions.Contains(rc.PropertyType))
                    continue;
                if (CondoTypes.Contains(subjectPropertyType) && CondoExclusions.Contains(rc.PropertyType))
                    continue;
            }

            if (resolvedDate.Value >= sixMonthsAgo)
                recent.Add((rc, saleDate));
            else
                older.Add((rc, saleDate));
        }

        // Sort each group by Correlation descending (highest similarity first)
        recent.Sort((a, b) => (b.Rc.Correlation ?? 0).CompareTo(a.Rc.Correlation ?? 0));
        older.Sort((a, b) => (b.Rc.Correlation ?? 0).CompareTo(a.Rc.Correlation ?? 0));

        // Tiered selection: prefer recent comps, backfill with older up to 5 total
        var selected = new List<(RentCastComp Rc, DateOnly SaleDate, bool IsRecent)>();

        if (recent.Count >= 5)
        {
            foreach (var item in recent.Take(5))
                selected.Add((item.Rc, item.SaleDate, true));
        }
        else
        {
            foreach (var item in recent)
                selected.Add((item.Rc, item.SaleDate, true));

            var needed = 5 - recent.Count;
            foreach (var item in older.Take(needed))
                selected.Add((item.Rc, item.SaleDate, false));
        }

        var result = new List<Comp>(selected.Count);
        foreach (var (rc, saleDate, isRecent) in selected)
        {
            result.Add(new Comp
            {
                Address = rc.FormattedAddress,
                SalePrice = rc.Price!.Value,
                SaleDate = saleDate,
                Beds = rc.Bedrooms ?? 0,
                Baths = (int)Math.Round(rc.Bathrooms ?? 0, MidpointRounding.AwayFromZero),
                Sqft = rc.SquareFootage!.Value,
                DaysOnMarket = rc.DaysOnMarket,
                DistanceMiles = rc.Distance ?? 0.0,
                Source = CompSource.RentCast,
                IsRecent = isRecent,
                Correlation = rc.Correlation
            });
        }

        return result;
    }
}

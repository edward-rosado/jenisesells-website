namespace RealEstateStar.Domain.Leads.Models;

public abstract record WorkerResult(string LeadId, bool Success, string? Error);

public sealed record CmaWorkerResult(
    string LeadId, bool Success, string? Error,
    decimal? EstimatedValue, decimal? PriceRangeLow, decimal? PriceRangeHigh,
    IReadOnlyList<CompSummary>? Comps, string? MarketAnalysis
) : WorkerResult(LeadId, Success, Error);

public sealed record HomeSearchWorkerResult(
    string LeadId, bool Success, string? Error,
    IReadOnlyList<ListingSummary>? Listings, string? AreaSummary
) : WorkerResult(LeadId, Success, Error);

public sealed record PdfWorkerResult(
    string LeadId, bool Success, string? Error,
    string? StoragePath
) : WorkerResult(LeadId, Success, Error);

public record CompSummary(
    string Address, decimal Price, int? Beds, decimal? Baths,
    int? Sqft, int? DaysOnMarket, double? Distance);

public record ListingSummary(
    string Address, decimal Price, int? Beds, decimal? Baths,
    int? Sqft, string? Status, string? Url);

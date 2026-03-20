namespace RealEstateStar.Api.Features.Leads;

public record DeleteResult(bool Success, List<string> DeletedItems, string? Error = null);

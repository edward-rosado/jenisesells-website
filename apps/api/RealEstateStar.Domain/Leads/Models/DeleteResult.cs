namespace RealEstateStar.Domain.Leads.Models;

public record DeleteResult(bool Success, List<string> DeletedItems, string? Error = null);

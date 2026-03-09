namespace RealEstateStar.Api.Models.Responses;

public record CmaStatusResponse
{
    public required string Status { get; init; }
    public required int Step { get; init; }
    public required int TotalSteps { get; init; }
    public required string Message { get; init; }
    public string? ErrorMessage { get; init; }
}

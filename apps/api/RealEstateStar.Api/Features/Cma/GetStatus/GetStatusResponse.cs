namespace RealEstateStar.Api.Features.Cma.GetStatus;

public record GetStatusResponse
{
    public required CmaJobStatus Status { get; init; }
    public required int Step { get; init; }
    public required int TotalSteps { get; init; }
    public required string Message { get; init; }
    public string? ErrorMessage { get; init; }
}

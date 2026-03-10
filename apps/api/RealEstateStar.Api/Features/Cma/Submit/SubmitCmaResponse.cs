namespace RealEstateStar.Api.Features.Cma.Submit;

public record SubmitCmaResponse
{
    public required string JobId { get; init; }
    public required string Status { get; init; }
}

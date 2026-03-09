namespace RealEstateStar.Api.Models.Responses;

public record CreateCmaResponse
{
    public required string JobId { get; init; }
    public required string Status { get; init; }
}

using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Models.Responses;

public record LeadSummaryResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Address { get; init; }
    public required string Timeline { get; init; }
    public required CmaJobStatus CmaStatus { get; init; }
    public required DateTime SubmittedAt { get; init; }
    public string? DriveLink { get; init; }
}

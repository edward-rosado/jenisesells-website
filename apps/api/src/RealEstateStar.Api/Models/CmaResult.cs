namespace RealEstateStar.Api.Models;

/// <summary>
/// Returned from the CMA endpoint after job submission.
/// </summary>
public sealed record CmaResult
{
    public required string JobId { get; init; }
    public required string Status { get; init; }
}

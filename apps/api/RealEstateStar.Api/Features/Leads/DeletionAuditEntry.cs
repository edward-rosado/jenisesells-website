namespace RealEstateStar.Api.Features.Leads;

public record DeletionAuditEntry
{
    public required DateTime Timestamp { get; init; }
    public required string AgentId { get; init; }
    public required Guid LeadId { get; init; }
    public required string Email { get; init; }
    public required string Action { get; init; }
}

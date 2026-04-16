namespace RealEstateStar.Domain.Activation.Models;

/// <summary>
/// Result of a brokerage lead routing decision.
/// </summary>
public sealed record RoutingDecision(
    string AgentId,
    string Reason,
    int AttemptCount);

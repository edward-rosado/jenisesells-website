namespace RealEstateStar.Domain.Activation.Models;

/// <summary>
/// Azure Table row tracking round-robin state for brokerage lead routing.
/// PK = accountId, RK = "consumption". Single ETag CAS for both counter and override.
/// Human edits to Drive routing-policy.json take effect via PolicyContentHash change.
/// </summary>
public sealed record BrokerageRoutingConsumption
{
    public string AccountId { get; init; } = "";
    public string PolicyContentHash { get; init; } = "";
    public int Counter { get; init; }
    public bool OverrideConsumed { get; init; }
    public DateTime LastDecisionAt { get; init; }
    public string? ETag { get; init; }
}

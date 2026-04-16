using System.Text.Json.Serialization;

namespace RealEstateStar.Domain.Activation.Models;

/// <summary>
/// Mirrors routing-policy.json in the agent's Drive folder (read-only on routing path).
/// Human-editable; changes take effect via content hash change on next lead.
/// </summary>
public sealed record RoutingPolicy
{
    [JsonPropertyName("account_id")]
    public string AccountId { get; init; } = "";

    [JsonPropertyName("agents")]
    public IReadOnlyList<RoutingAgent> Agents { get; init; } = [];

    [JsonPropertyName("next_lead")]
    public string? NextLead { get; init; }

    [JsonPropertyName("strategy")]
    public string Strategy { get; init; } = "round-robin";
}

public sealed record RoutingAgent
{
    [JsonPropertyName("agent_id")]
    public string AgentId { get; init; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    [JsonPropertyName("service_areas")]
    public IReadOnlyList<string> ServiceAreas { get; init; } = [];

    [JsonPropertyName("specialties")]
    public IReadOnlyList<string> Specialties { get; init; } = [];

    [JsonPropertyName("weight")]
    public int Weight { get; init; } = 1;
}

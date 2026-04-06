using System.Text.Json.Serialization;

namespace RealEstateStar.Domain.Activation.Models;

/// <summary>
/// Structured pipeline state for an agent — the live source of truth for their lead pipeline.
/// Stored as pipeline.json at real-estate-star/{agentId}/.
/// Queryable by PipelineQueryService (pure C#) for WhatsApp fast-path responses.
/// Updated continuously by Gmail monitoring after initial activation population.
/// </summary>
public sealed record AgentPipeline(
    [property: JsonPropertyName("agentId")] string AgentId,
    [property: JsonPropertyName("updatedAt")] DateTime UpdatedAt,
    [property: JsonPropertyName("leads")] IReadOnlyList<PipelineLead> Leads);

/// <summary>
/// A single lead/client in the agent's pipeline. Fields are structured for
/// both programmatic querying (C# pattern matching) and Claude context injection.
/// </summary>
public sealed record PipelineLead(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("stage")] string Stage,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("property")] string? Property,
    [property: JsonPropertyName("source")] string? Source,
    [property: JsonPropertyName("firstSeen")] DateTime FirstSeen,
    [property: JsonPropertyName("lastActivity")] DateTime LastActivity,
    [property: JsonPropertyName("next")] string? Next,
    [property: JsonPropertyName("notes")] string? Notes);

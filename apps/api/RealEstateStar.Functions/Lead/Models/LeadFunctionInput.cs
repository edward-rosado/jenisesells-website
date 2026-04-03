using System.Text.Json.Serialization;
using RealEstateStar.Domain.Leads.Models;

namespace RealEstateStar.Functions.Lead.Models;

/// <summary>
/// Queue message and orchestrator input for the lead pipeline.
/// Serialized as JSON via Azure Queue Storage → Durable Functions.
/// </summary>
public sealed record LeadOrchestrationMessage
{
    [JsonPropertyName("agentId")]
    public required string AgentId { get; init; }

    [JsonPropertyName("leadId")]
    public required string LeadId { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }
}

/// <summary>
/// Orchestrator input — extends <see cref="LeadOrchestrationMessage"/> with lead routing flags
/// computed by <see cref="StartLeadProcessingFunction"/> after loading the lead.
/// </summary>
public sealed record LeadOrchestratorInput
{
    [JsonPropertyName("agentId")]
    public required string AgentId { get; init; }

    [JsonPropertyName("leadId")]
    public required string LeadId { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    /// <summary>True when this lead is a Seller or Both type with seller details present.</summary>
    [JsonPropertyName("shouldRunCma")]
    public required bool ShouldRunCma { get; init; }

    /// <summary>True when this lead is a Buyer or Both type with buyer details present.</summary>
    [JsonPropertyName("shouldRunHomeSearch")]
    public required bool ShouldRunHomeSearch { get; init; }

    /// <summary>SHA-256 hash of the CMA input (seller address fields). Used for cross-lead dedup.</summary>
    [JsonPropertyName("cmaInputHash")]
    public required string CmaInputHash { get; init; }

    /// <summary>SHA-256 hash of the HomeSearch input (buyer criteria fields). Used for cross-lead dedup.</summary>
    [JsonPropertyName("hsInputHash")]
    public required string HsInputHash { get; init; }

    /// <summary>BCP 47 locale from the lead form submission (e.g., "en", "es"). Null when not provided.</summary>
    [JsonPropertyName("locale")]
    public string? Locale { get; init; }
}

/// <summary>
/// Input to the LoadAgentConfig activity function.
/// </summary>
public sealed record LoadAgentConfigInput
{
    [JsonPropertyName("agentId")]
    public required string AgentId { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }
}

/// <summary>
/// Output of the LoadAgentConfig activity function.
/// Null when the agent config is not found.
/// </summary>
public sealed record LoadAgentConfigOutput
{
    [JsonPropertyName("found")]
    public required bool Found { get; init; }

    [JsonPropertyName("agentNotificationConfig")]
    public AgentNotificationConfig? AgentNotificationConfig { get; init; }
}

/// <summary>
/// Input to the ScoreLead activity function.
/// </summary>
public sealed record ScoreLeadInput
{
    [JsonPropertyName("agentId")]
    public required string AgentId { get; init; }

    [JsonPropertyName("leadId")]
    public required string LeadId { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }
}

/// <summary>
/// Output of the ScoreLead activity function.
/// </summary>
public sealed record ScoreLeadOutput
{
    [JsonPropertyName("score")]
    public required LeadScore Score { get; init; }
}

/// <summary>
/// Input to the CheckContentCache activity function.
/// </summary>
public sealed record CheckContentCacheInput
{
    [JsonPropertyName("cmaInputHash")]
    public required string CmaInputHash { get; init; }

    [JsonPropertyName("hsInputHash")]
    public required string HsInputHash { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }
}

/// <summary>
/// Output of the CheckContentCache activity function.
/// When a cache hit is found, the result is already computed and CMA/HS can be skipped.
/// </summary>
public sealed record CheckContentCacheOutput
{
    [JsonPropertyName("cmaCacheHit")]
    public required bool CmaCacheHit { get; init; }

    [JsonPropertyName("hsCacheHit")]
    public required bool HsCacheHit { get; init; }

    [JsonPropertyName("cachedCmaResult")]
    public CmaWorkerResult? CachedCmaResult { get; init; }

    [JsonPropertyName("cachedHsResult")]
    public HomeSearchWorkerResult? CachedHsResult { get; init; }
}

/// <summary>
/// Input to the CmaProcessing activity function.
/// Carries seller address info needed to fetch comps.
/// </summary>
public sealed record CmaFunctionInput
{
    [JsonPropertyName("agentId")]
    public required string AgentId { get; init; }

    [JsonPropertyName("leadId")]
    public required string LeadId { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    [JsonPropertyName("agentNotificationConfig")]
    public required AgentNotificationConfig AgentNotificationConfig { get; init; }
}

/// <summary>
/// Output of the CmaProcessing activity function.
/// </summary>
public sealed record CmaFunctionOutput
{
    [JsonPropertyName("result")]
    public required CmaWorkerResult Result { get; init; }
}

/// <summary>
/// Input to the HomeSearch activity function.
/// Carries buyer criteria needed to fetch listings.
/// </summary>
public sealed record HomeSearchFunctionInput
{
    [JsonPropertyName("agentId")]
    public required string AgentId { get; init; }

    [JsonPropertyName("leadId")]
    public required string LeadId { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    [JsonPropertyName("agentNotificationConfig")]
    public required AgentNotificationConfig AgentNotificationConfig { get; init; }
}

/// <summary>
/// Output of the HomeSearch activity function.
/// </summary>
public sealed record HomeSearchFunctionOutput
{
    [JsonPropertyName("result")]
    public required HomeSearchWorkerResult Result { get; init; }
}

/// <summary>
/// Input to the GeneratePdf activity function.
/// </summary>
public sealed record GeneratePdfInput
{
    [JsonPropertyName("agentId")]
    public required string AgentId { get; init; }

    [JsonPropertyName("leadId")]
    public required string LeadId { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    [JsonPropertyName("cmaResult")]
    public required CmaWorkerResult CmaResult { get; init; }

    /// <summary>BCP 47 locale from the lead form submission (e.g., "en", "es"). Null when not provided.</summary>
    [JsonPropertyName("locale")]
    public string? Locale { get; init; }
}

/// <summary>
/// Output of the GeneratePdf activity function.
/// </summary>
public sealed record GeneratePdfOutput
{
    [JsonPropertyName("pdfStoragePath")]
    public required string PdfStoragePath { get; init; }
}

/// <summary>
/// Input to the DraftLeadEmail activity function.
/// </summary>
public sealed record DraftLeadEmailInput
{
    [JsonPropertyName("agentId")]
    public required string AgentId { get; init; }

    [JsonPropertyName("leadId")]
    public required string LeadId { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    [JsonPropertyName("agentNotificationConfig")]
    public required AgentNotificationConfig AgentNotificationConfig { get; init; }

    [JsonPropertyName("score")]
    public required LeadScore Score { get; init; }

    [JsonPropertyName("cmaResult")]
    public CmaWorkerResult? CmaResult { get; init; }

    [JsonPropertyName("hsResult")]
    public HomeSearchWorkerResult? HsResult { get; init; }

    /// <summary>BCP 47 locale from the lead form submission (e.g., "en", "es"). Null when not provided.</summary>
    [JsonPropertyName("locale")]
    public string? Locale { get; init; }
}

/// <summary>
/// Output of the DraftLeadEmail activity function.
/// </summary>
public sealed record DraftLeadEmailOutput
{
    [JsonPropertyName("subject")]
    public required string Subject { get; init; }

    [JsonPropertyName("htmlBody")]
    public required string HtmlBody { get; init; }

    [JsonPropertyName("pdfAttachmentPath")]
    public string? PdfAttachmentPath { get; init; }
}

/// <summary>
/// Input to the SendLeadEmail activity function.
/// </summary>
public sealed record SendLeadEmailInput
{
    [JsonPropertyName("agentId")]
    public required string AgentId { get; init; }

    [JsonPropertyName("leadId")]
    public required string LeadId { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    [JsonPropertyName("instanceId")]
    public required string InstanceId { get; init; }

    [JsonPropertyName("emailDraft")]
    public required DraftLeadEmailOutput EmailDraft { get; init; }

    [JsonPropertyName("agentNotificationConfig")]
    public required AgentNotificationConfig AgentNotificationConfig { get; init; }

    [JsonPropertyName("score")]
    public required LeadScore Score { get; init; }

    [JsonPropertyName("cmaResult")]
    public CmaWorkerResult? CmaResult { get; init; }

    [JsonPropertyName("hsResult")]
    public HomeSearchWorkerResult? HsResult { get; init; }
}

/// <summary>
/// Input to the NotifyAgent activity function.
/// </summary>
public sealed record NotifyAgentInput
{
    [JsonPropertyName("agentId")]
    public required string AgentId { get; init; }

    [JsonPropertyName("leadId")]
    public required string LeadId { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    [JsonPropertyName("instanceId")]
    public required string InstanceId { get; init; }

    [JsonPropertyName("agentNotificationConfig")]
    public required AgentNotificationConfig AgentNotificationConfig { get; init; }

    [JsonPropertyName("score")]
    public required LeadScore Score { get; init; }

    [JsonPropertyName("cmaResult")]
    public CmaWorkerResult? CmaResult { get; init; }

    [JsonPropertyName("hsResult")]
    public HomeSearchWorkerResult? HsResult { get; init; }

    /// <summary>BCP 47 locale from the lead form submission (e.g., "en", "es"). Null when not provided.</summary>
    [JsonPropertyName("locale")]
    public string? Locale { get; init; }
}

/// <summary>
/// Input to the PersistLeadResults activity function.
/// </summary>
public sealed record PersistLeadResultsInput
{
    [JsonPropertyName("agentId")]
    public required string AgentId { get; init; }

    [JsonPropertyName("leadId")]
    public required string LeadId { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    [JsonPropertyName("score")]
    public required LeadScore Score { get; init; }

    [JsonPropertyName("cmaResult")]
    public CmaWorkerResult? CmaResult { get; init; }

    [JsonPropertyName("hsResult")]
    public HomeSearchWorkerResult? HsResult { get; init; }

    [JsonPropertyName("pdfStoragePath")]
    public string? PdfStoragePath { get; init; }

    [JsonPropertyName("emailDraft")]
    public DraftLeadEmailOutput? EmailDraft { get; init; }

    [JsonPropertyName("emailSent")]
    public bool EmailSent { get; init; }

    [JsonPropertyName("agentNotified")]
    public bool AgentNotified { get; init; }

    [JsonPropertyName("cmaInputHash")]
    public required string CmaInputHash { get; init; }

    [JsonPropertyName("hsInputHash")]
    public required string HsInputHash { get; init; }

    /// <summary>BCP 47 locale from the lead form submission (e.g., "en", "es"). Null when not provided.</summary>
    [JsonPropertyName("locale")]
    public string? Locale { get; init; }
}

/// <summary>
/// Input to the UpdateContentCache activity function.
/// </summary>
public sealed record UpdateContentCacheInput
{
    [JsonPropertyName("cmaInputHash")]
    public required string CmaInputHash { get; init; }

    [JsonPropertyName("hsInputHash")]
    public required string HsInputHash { get; init; }

    [JsonPropertyName("cmaResult")]
    public CmaWorkerResult? CmaResult { get; init; }

    [JsonPropertyName("hsResult")]
    public HomeSearchWorkerResult? HsResult { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }
}

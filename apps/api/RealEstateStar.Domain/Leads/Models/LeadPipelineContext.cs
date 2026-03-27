namespace RealEstateStar.Domain.Leads.Models;

/// <summary>
/// Mutable context passed through all pipeline activities for a single lead.
/// Each activity reads what it needs and writes its output back.
/// This is the single source of truth — no passing large objects between activities.
/// </summary>
public class LeadPipelineContext
{
    // Input (set at creation)
    public required Lead Lead { get; init; }
    public required AgentNotificationConfig AgentConfig { get; init; }
    public required string CorrelationId { get; init; }
    public LeadRetryState RetryState { get; set; } = new();

    // Activity outputs (set by each activity)
    public LeadScore? Score { get; set; }
    public CmaWorkerResult? CmaResult { get; set; }
    public HomeSearchWorkerResult? HsResult { get; set; }
    public string? PdfStoragePath { get; set; }
    public CommunicationRecord? LeadEmail { get; set; }
    public CommunicationRecord? AgentNotification { get; set; }

    /// <summary>Build the final pipeline result from accumulated context.</summary>
    public LeadPipelineResult ToResult() => new(
        LeadId: Lead.Id.ToString(),
        Success: true,
        Score: Score,
        CmaResult: CmaResult,
        HsResult: HsResult,
        PdfStoragePath: PdfStoragePath,
        LeadEmailSent: LeadEmail?.Sent ?? false,
        AgentNotified: AgentNotification?.Sent ?? false);
}

/// <summary>Final result returned by the orchestrator.</summary>
public record LeadPipelineResult(
    string LeadId,
    bool Success,
    LeadScore? Score,
    CmaWorkerResult? CmaResult,
    HomeSearchWorkerResult? HsResult,
    string? PdfStoragePath,
    bool LeadEmailSent,
    bool AgentNotified);

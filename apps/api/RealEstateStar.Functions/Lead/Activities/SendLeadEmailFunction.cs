using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Functions.Lead.Models;

namespace RealEstateStar.Functions.Lead.Activities;

/// <summary>
/// Sends the drafted lead email via <see cref="ILeadCommunicatorService"/>.
/// Idempotency is already guarded inside the service (IIdempotencyStore with key
/// <c>lead:{agentId}-{leadId}:email-send</c>).
/// This activity is safe to retry — duplicate sends are prevented at the service layer.
/// </summary>
public sealed class SendLeadEmailFunction(
    ILeadStore leadStore,
    ILeadCommunicatorService communicatorService,
    ILogger<SendLeadEmailFunction> logger)
{
    [Function("SendLeadEmail")]
    public async Task RunAsync(
        [ActivityTrigger] SendLeadEmailInput input,
        CancellationToken ct)
    {
        var leadId = Guid.Parse(input.LeadId);
        var lead = await leadStore.GetAsync(input.AgentId, leadId, ct)
            ?? throw new InvalidOperationException(
                $"[SLE-001] Lead {input.LeadId} not found. CorrelationId={input.CorrelationId}");

        // Reconstruct the pipeline context required by ILeadCommunicatorService
        var ctx = new LeadPipelineContext
        {
            Lead = lead,
            AgentConfig = input.AgentNotificationConfig,
            CorrelationId = input.CorrelationId,
            Score = input.Score,
            CmaResult = input.CmaResult,
            HsResult = input.HsResult
        };

        var draft = new CommunicationRecord
        {
            Subject = input.EmailDraft.Subject,
            HtmlBody = input.EmailDraft.HtmlBody,
            Channel = "email",
            DraftedAt = DateTimeOffset.UtcNow,
            ContentHash = Domain.Shared.ContentHash.Compute(input.EmailDraft.Subject, input.EmailDraft.HtmlBody)
        };

        logger.LogInformation("[SLE-010] Sending lead email for lead {LeadId}. CorrelationId={CorrelationId}",
            input.LeadId, input.CorrelationId);

        var result = await communicatorService.SendAsync(draft, ctx, ct);

        if (result.Sent)
            logger.LogInformation("[SLE-020] Lead email sent for lead {LeadId}. CorrelationId={CorrelationId}",
                input.LeadId, input.CorrelationId);
        else
            logger.LogWarning("[SLE-021] Lead email not sent for lead {LeadId}. Error={Error}. CorrelationId={CorrelationId}",
                input.LeadId, result.Error, input.CorrelationId);
    }
}

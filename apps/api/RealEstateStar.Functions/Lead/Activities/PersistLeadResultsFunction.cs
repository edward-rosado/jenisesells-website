using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Activities.Lead.Persist;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Functions.Lead.Models;

namespace RealEstateStar.Functions.Lead.Activities;

/// <summary>
/// Persists all pipeline artifacts for a lead via <see cref="PersistActivity"/>.
/// Sets final status to Complete. Safe to retry — PersistActivity uses content-hash dedup.
/// </summary>
public sealed class PersistLeadResultsFunction(
    ILeadStore leadStore,
    PersistActivity persistActivity,
    ILogger<PersistLeadResultsFunction> logger)
{
    [Function("PersistLeadResults")]
    public async Task RunAsync(
        [ActivityTrigger] PersistLeadResultsInput input,
        CancellationToken ct)
    {
        var leadId = Guid.Parse(input.LeadId);
        var lead = await leadStore.GetAsync(input.AgentId, leadId, ct)
            ?? throw new InvalidOperationException(
                $"[PLR-001] Lead {input.LeadId} not found. CorrelationId={input.CorrelationId}");

        // Build a minimal AgentNotificationConfig for the context
        // (PersistActivity only needs it to build the pipeline context structure)
        var agentConfig = new AgentNotificationConfig
        {
            AgentId = input.AgentId,
            Handle = input.AgentId,
            Name = string.Empty,
            FirstName = string.Empty,
            Email = string.Empty,
            Phone = string.Empty,
            LicenseNumber = string.Empty,
            BrokerageName = string.Empty,
            PrimaryColor = string.Empty,
            AccentColor = string.Empty,
            State = string.Empty
        };

        // Build retry state from input hashes so PersistActivity can write it
        var retryState = new LeadRetryState();
        if (input.CmaResult?.Success == true)
        {
            retryState.CompletedActivityKeys["cma"] = input.CmaInputHash;
            retryState.CompletedResultPaths["cma"] = $"cma:{input.LeadId}:{input.CmaInputHash}";
        }

        if (input.HsResult?.Success == true)
        {
            retryState.CompletedActivityKeys["homeSearch"] = input.HsInputHash;
            retryState.CompletedResultPaths["homeSearch"] = $"hs:{input.LeadId}:{input.HsInputHash}";
        }

        // Build a CommunicationRecord for the email draft if one was produced
        CommunicationRecord? emailRecord = null;
        if (input.EmailDraft is not null)
        {
            emailRecord = new CommunicationRecord
            {
                Subject = input.EmailDraft.Subject,
                HtmlBody = input.EmailDraft.HtmlBody,
                Channel = "email",
                DraftedAt = DateTimeOffset.UtcNow,
                ContentHash = ContentHash.Compute(input.EmailDraft.Subject, input.EmailDraft.HtmlBody),
                Sent = input.EmailSent
            };
        }

        var ctx = new LeadPipelineContext
        {
            Lead = lead,
            AgentConfig = agentConfig,
            CorrelationId = input.CorrelationId,
            Score = input.Score,
            CmaResult = input.CmaResult,
            HsResult = input.HsResult,
            PdfStoragePath = input.PdfStoragePath,
            LeadEmail = emailRecord,
            RetryState = retryState
        };

        logger.LogInformation("[PLR-010] Persisting results for lead {LeadId}. CorrelationId={CorrelationId}",
            input.LeadId, input.CorrelationId);

        await persistActivity.ExecuteAsync(ctx, ct);

        logger.LogInformation("[PLR-020] Results persisted for lead {LeadId}. CorrelationId={CorrelationId}",
            input.LeadId, input.CorrelationId);
    }
}

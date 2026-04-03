using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Services.LeadCommunicator;

/// <summary>
/// Higher-level service that wraps draft + send as two separate activities.
/// Draft and send are separated so the pipeline can checkpoint between them.
/// </summary>
public class LeadCommunicatorService(
    ILeadEmailDrafter drafter,
    IGmailSender gmailSender,
    ILogger<LeadCommunicatorService> logger) : ILeadCommunicatorService
{
    /// <summary>
    /// Draft personalized email content via Claude.
    /// Returns a <see cref="CommunicationRecord"/> with <c>Sent = false</c>.
    /// </summary>
    public async Task<CommunicationRecord> DraftAsync(LeadPipelineContext ctx, CancellationToken ct)
    {
        var lead = ctx.Lead;
        var agentConfig = ctx.AgentConfig;

        using var span = LeadCommunicatorDiagnostics.ActivitySource.StartActivity("activity.draft_lead_email");
        span?.SetTag("lead.id", lead.Id.ToString());
        span?.SetTag("agent.id", agentConfig.AgentId);
        span?.SetTag("correlation.id", ctx.CorrelationId);

        logger.LogInformation(
            "[DRAFT-001] Drafting lead email for lead {LeadId} agent {AgentId}",
            lead.Id, agentConfig.AgentId);

        var draftStarted = Stopwatch.GetTimestamp();

        var email = await drafter.DraftAsync(
            lead,
            ctx.Score ?? new LeadScore { OverallScore = 0, Factors = [], Explanation = string.Empty },
            ctx.CmaResult,
            ctx.HsResult,
            agentConfig,
            ct,
            ctx.AgentContext);

        LeadCommunicatorDiagnostics.DraftDurationMs.Record(
            Stopwatch.GetElapsedTime(draftStarted).TotalMilliseconds);

        var contentHash = ContentHash.Compute(email.Subject, email.HtmlBody);

        return new CommunicationRecord
        {
            Subject = email.Subject,
            HtmlBody = email.HtmlBody,
            Channel = "email",
            Locale = lead.Locale,
            DraftedAt = DateTimeOffset.UtcNow,
            ContentHash = contentHash
        };
    }

    /// <summary>
    /// Send the drafted email via Gmail.
    /// Updates and returns the <see cref="CommunicationRecord"/> with <c>Sent = true</c> and <c>SentAt</c>.
    /// </summary>
    public async Task<CommunicationRecord> SendAsync(
        CommunicationRecord draft, LeadPipelineContext ctx, CancellationToken ct)
    {
        var lead = ctx.Lead;
        var agentConfig = ctx.AgentConfig;

        using var span = LeadCommunicatorDiagnostics.ActivitySource.StartActivity("activity.send_lead_email");
        span?.SetTag("lead.id", lead.Id.ToString());
        span?.SetTag("agent.id", agentConfig.AgentId);
        span?.SetTag("correlation.id", ctx.CorrelationId);

        var emailHash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(lead.Email ?? "")))[..12];
        logger.LogInformation(
            "[SEND-001] Sending lead email for lead {LeadId} to {LeadEmailHash}",
            lead.Id, emailHash);

        var sendStarted = Stopwatch.GetTimestamp();

        try
        {
            await gmailSender.SendAsync(
                agentConfig.AgentId,
                agentConfig.AgentId,
                lead.Email,
                draft.Subject,
                draft.HtmlBody,
                ct);

            draft = draft with { Sent = true, SentAt = DateTimeOffset.UtcNow };

            LeadCommunicatorDiagnostics.SendSuccess.Add(1);

            logger.LogInformation(
                "[SEND-002] Lead email sent for lead {LeadId}",
                lead.Id);
        }
        catch (Exception ex)
        {
            LeadCommunicatorDiagnostics.SendFailed.Add(1);
            span?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);

            logger.LogError(ex,
                "[SEND-010] Failed to send lead email for lead {LeadId}",
                lead.Id);

            var sanitizedError = ex is HttpRequestException or TimeoutException
                ? $"Delivery failure: {ex.GetType().Name}"
                : "Internal delivery error";
            draft = draft with { Error = sanitizedError };
        }
        finally
        {
            LeadCommunicatorDiagnostics.SendDurationMs.Record(
                Stopwatch.GetElapsedTime(sendStarted).TotalMilliseconds);
        }

        return draft;
    }
}

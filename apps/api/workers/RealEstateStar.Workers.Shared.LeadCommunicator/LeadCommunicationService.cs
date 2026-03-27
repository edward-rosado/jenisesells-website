using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Workers.Shared.LeadCommunicator;

/// <summary>
/// Higher-level service that wraps draft + send as two separate activities.
/// Draft and send are separated so the pipeline can checkpoint between them.
/// </summary>
public class LeadCommunicationService(
    ILeadEmailDrafter drafter,
    IGmailSender gmailSender,
    ILogger<LeadCommunicationService> logger)
{
    /// <summary>
    /// Draft personalized email content via Claude.
    /// Returns a <see cref="CommunicationRecord"/> with <c>Sent = false</c>.
    /// </summary>
    public async Task<CommunicationRecord> DraftAsync(LeadPipelineContext ctx, CancellationToken ct)
    {
        var lead = ctx.Lead;
        var agentConfig = ctx.AgentConfig;

        logger.LogInformation(
            "[LEAD-COMM-001] Drafting lead email for lead {LeadId} agent {AgentId}",
            lead.Id, agentConfig.AgentId);

        var email = await drafter.DraftAsync(
            lead,
            ctx.Score ?? new LeadScore { OverallScore = 0, Factors = [], Explanation = string.Empty },
            ctx.CmaResult,
            ctx.HsResult,
            agentConfig,
            ct);

        var contentHash = ComputeContentHash(email.Subject, email.HtmlBody);

        return new CommunicationRecord
        {
            Subject = email.Subject,
            HtmlBody = email.HtmlBody,
            Channel = "email",
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

        logger.LogInformation(
            "[LEAD-COMM-002] Sending lead email for lead {LeadId} to {LeadEmail}",
            lead.Id, lead.Email);

        try
        {
            await gmailSender.SendAsync(
                agentConfig.AgentId,
                agentConfig.AgentId,
                lead.Email,
                draft.Subject,
                draft.HtmlBody,
                ct);

            draft.Sent = true;
            draft.SentAt = DateTimeOffset.UtcNow;

            logger.LogInformation(
                "[LEAD-COMM-003] Lead email sent for lead {LeadId}",
                lead.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[LEAD-COMM-004] Failed to send lead email for lead {LeadId}",
                lead.Id);

            draft.Error = ex.Message;
        }

        return draft;
    }

    internal static string ComputeContentHash(string subject, string htmlBody)
    {
        var input = $"{subject}|{htmlBody}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

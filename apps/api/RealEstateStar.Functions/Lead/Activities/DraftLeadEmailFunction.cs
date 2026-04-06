using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Functions.Lead.Models;

namespace RealEstateStar.Functions.Lead.Activities;

/// <summary>
/// Drafts the lead email via <see cref="ILeadEmailDrafter"/>.
/// Decoupled from sending so the pipeline can checkpoint between draft and send.
/// </summary>
public sealed class DraftLeadEmailFunction(
    ILeadStore leadStore,
    ILeadEmailDrafter emailDrafter,
    ILogger<DraftLeadEmailFunction> logger,
    IAgentContextLoader? agentContextLoader = null)
{
    [Function("DraftLeadEmail")]
    public async Task<string> RunAsync(
        [ActivityTrigger] DraftLeadEmailInput input,
        CancellationToken ct)
    {
        var leadId = Guid.Parse(input.LeadId);
        var lead = await leadStore.GetAsync(input.AgentId, leadId, ct)
            ?? throw new InvalidOperationException(
                $"[DLE-001] Lead {input.LeadId} not found. CorrelationId={input.CorrelationId}");

        logger.LogInformation("[DLE-010] Drafting email for lead {LeadId}. Locale={Locale}. CorrelationId={CorrelationId}",
            input.LeadId, input.Locale ?? "en", input.CorrelationId);

        // Load agent context so locale-specific voice/personality skills are applied when drafting.
        Domain.Activation.Models.AgentContext? agentContext = null;
        if (agentContextLoader is not null)
        {
            try
            {
                agentContext = await agentContextLoader.LoadAsync(input.AgentId, input.AgentId, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "[DLE-011] Agent context load failed for {AgentId}; drafting without context. CorrelationId={CorrelationId}",
                    input.AgentId, input.CorrelationId);
            }
        }

        var email = await emailDrafter.DraftAsync(
            lead,
            input.Score,
            input.CmaResult,
            input.HsResult,
            input.AgentNotificationConfig,
            ct,
            agentContext);

        logger.LogInformation("[DLE-020] Email drafted for lead {LeadId}. Subject={Subject}. CorrelationId={CorrelationId}",
            input.LeadId, email.Subject, input.CorrelationId);

        return JsonSerializer.Serialize(new DraftLeadEmailOutput
        {
            Subject = email.Subject,
            HtmlBody = email.HtmlBody,
            PdfAttachmentPath = email.PdfAttachmentPath
        });
    }
}

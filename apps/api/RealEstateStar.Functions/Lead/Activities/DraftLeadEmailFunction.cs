using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
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
    ILogger<DraftLeadEmailFunction> logger)
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

        logger.LogInformation("[DLE-010] Drafting email for lead {LeadId}. CorrelationId={CorrelationId}",
            input.LeadId, input.CorrelationId);

        var email = await emailDrafter.DraftAsync(
            lead,
            input.Score,
            input.CmaResult,
            input.HsResult,
            input.AgentNotificationConfig,
            ct);

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

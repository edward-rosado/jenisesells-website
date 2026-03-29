namespace RealEstateStar.Domain.Leads.Interfaces;

using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Leads.Models;

public interface ILeadEmailDrafter
{
    Task<LeadEmail> DraftAsync(
        Lead lead, LeadScore score,
        CmaWorkerResult? cmaResult, HomeSearchWorkerResult? homeSearchResult,
        AgentNotificationConfig agentConfig, CancellationToken ct,
        AgentContext? agentContext = null);
}

public record LeadEmail(string Subject, string HtmlBody, string? PdfAttachmentPath);

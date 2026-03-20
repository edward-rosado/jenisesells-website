using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Services;
using RealEstateStar.Api.Services.Gws;

namespace RealEstateStar.Api.Features.Leads.Services;

public class MultiChannelLeadNotifier(
    IHttpClientFactory httpClientFactory,
    IGwsService gwsService,
    IAgentConfigService agentConfigService,
    ILogger<MultiChannelLeadNotifier> logger) : ILeadNotifier
{
    public async Task NotifyAgentAsync(string agentId, Lead lead, LeadEnrichment enrichment, LeadScore score, CancellationToken ct)
    {
        var config = await agentConfigService.GetAgentAsync(agentId, ct);
        var agentEmail = config?.Identity?.Email ?? "";
        var webhookUrl = config?.Integrations?.ChatWebhookUrl;

        var chatTask = SendChatAsync(webhookUrl, lead, enrichment, score, ct);
        var emailTask = SendEmailAsync(agentEmail, lead, enrichment, score, ct);

        await chatTask;
        await emailTask;
    }

    private async Task SendChatAsync(string? webhookUrl, Lead lead, LeadEnrichment enrichment, LeadScore score, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
            return;

        try
        {
            var card = LeadChatCardRenderer.RenderNewLeadCard(lead, enrichment, score);
            var client = httpClientFactory.CreateClient("GoogleChat");
            var response = await client.PostAsJsonAsync(webhookUrl, card, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[LEAD-033] Google Chat webhook failed for agent {AgentId}, lead {LeadId}. Continuing to email.", lead.AgentId, lead.Id);
        }
    }

    private async Task SendEmailAsync(string agentEmail, Lead lead, LeadEnrichment enrichment, LeadScore score, CancellationToken ct)
    {
        try
        {
            var subject = BuildSubject(lead, enrichment, score);
            var body = BuildEmailBody(lead, enrichment, score);
            await gwsService.SendEmailAsync(agentEmail, agentEmail, subject, body, null, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[LEAD-034] Gmail notification failed for agent {AgentId}, lead {LeadId}.", lead.AgentId, lead.Id);
        }
    }

    internal static string BuildSubject(Lead lead, LeadEnrichment enrichment, LeadScore score)
        => $"New Lead: {lead.FullName} — {enrichment.MotivationCategory} (Score: {score.OverallScore})";

    // CAN-SPAM classification: transactional (agent receiving notification about their own incoming lead).
    // Not commercial marketing — no unsubscribe footer or physical address required.
    internal static string BuildEmailBody(Lead lead, LeadEnrichment enrichment, LeadScore score)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# New Lead: {lead.FullName}");
        sb.AppendLine();
        sb.AppendLine($"**Score:** {score.OverallScore} / 100");
        sb.AppendLine($"**Motivation:** {enrichment.MotivationCategory}");
        sb.AppendLine($"**Lead Types:** {string.Join(", ", lead.LeadTypes)}");
        sb.AppendLine($"**Received:** {lead.ReceivedAt:MMMM d, yyyy h:mm tt} UTC");
        sb.AppendLine();

        // Contact
        sb.AppendLine("## Contact");
        sb.AppendLine($"- **Email:** {lead.Email}");
        sb.AppendLine($"- **Phone:** {lead.Phone}");
        sb.AppendLine($"- **Timeline:** {lead.Timeline}");
        sb.AppendLine();

        // Seller section — only when seller details are present
        if (lead.SellerDetails is { } s)
        {
            sb.AppendLine("## Selling");
            sb.AppendLine($"- **Address:** {s.Address}, {s.City}, {s.State} {s.Zip}");
            if (s.PropertyType is not null) sb.AppendLine($"- **Property Type:** {s.PropertyType}");
            if (s.Condition is not null)    sb.AppendLine($"- **Condition:** {s.Condition}");
            if (s.AskingPrice is not null)  sb.AppendLine($"- **Asking Price:** ${s.AskingPrice.Value:N0}");
            sb.AppendLine();
        }

        // Buyer section — only when buyer details are present
        if (lead.BuyerDetails is { } b)
        {
            sb.AppendLine("## Buying");
            sb.AppendLine($"- **Desired Area:** {b.City}, {b.State}");
            if (b.MaxBudget is not null)              sb.AppendLine($"- **Max Budget:** ${b.MaxBudget.Value:N0}");
            if (b.Bedrooms is not null)               sb.AppendLine($"- **Bedrooms:** {b.Bedrooms}");
            if (b.Bathrooms is not null)              sb.AppendLine($"- **Bathrooms:** {b.Bathrooms}");
            if (b.PropertyTypes is { Count: > 0 })   sb.AppendLine($"- **Property Types:** {string.Join(", ", b.PropertyTypes)}");
            if (b.MustHaves is { Count: > 0 })       sb.AppendLine($"- **Must-Haves:** {string.Join(", ", b.MustHaves)}");
            sb.AppendLine();
        }

        // Enrichment summary
        sb.AppendLine("## Enrichment Summary");
        sb.AppendLine($"**Motivation Analysis:** {enrichment.MotivationAnalysis}");
        sb.AppendLine();
        sb.AppendLine($"**Professional Background:** {enrichment.ProfessionalBackground}");
        sb.AppendLine();
        sb.AppendLine($"**Financial Indicators:** {enrichment.FinancialIndicators}");
        sb.AppendLine();
        sb.AppendLine($"**Timeline Pressure:** {enrichment.TimelinePressure}");
        sb.AppendLine();

        // Cold call openers
        if (enrichment.ColdCallOpeners.Count > 0)
        {
            sb.AppendLine("## Cold Call Openers");
            foreach (var opener in enrichment.ColdCallOpeners)
                sb.AppendLine($"- {opener}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Notifications.Leads;

public class MultiChannelLeadNotifier(
    IHttpClientFactory httpClientFactory,
    IGmailSender gmailSender,
    IFileStorageProvider fanOutStorage,
    IAccountConfigService accountConfigService,
    ILogger<MultiChannelLeadNotifier> logger) : ILeadNotifier
{
    public async Task NotifyAgentAsync(string agentId, Lead lead, LeadEnrichment enrichment, LeadScore score, CancellationToken ct)
    {
        logger.LogInformation("[NOTIFY-001] Starting agent notification for lead {LeadId}, agent {AgentId}", lead.Id, agentId);

        var config = await accountConfigService.GetAccountAsync(agentId, ct);
        var accountId = NotificationHelpers.ResolveAccountId(config, agentId);
        var agentEmail = config?.Agent?.Email ?? "";
        var webhookUrl = config?.Integrations?.ChatWebhookUrl;

        logger.LogInformation("[NOTIFY-002] Agent config loaded. Email: {AgentEmail}, WebhookConfigured: {HasWebhook}",
            string.IsNullOrWhiteSpace(agentEmail) ? "(empty)" : agentEmail,
            !string.IsNullOrWhiteSpace(webhookUrl));

        var chatSent = false;
        var emailSent = false;

        try { await SendChatAsync(webhookUrl, lead, enrichment, score, ct); chatSent = !string.IsNullOrWhiteSpace(webhookUrl); }
        catch (Exception) { /* logged inside SendChatAsync */ }

        try { await SendEmailAsync(accountId, agentId, agentEmail, lead, enrichment, score, ct); emailSent = true; }
        catch (Exception) { /* logged inside SendEmailAsync */ }

        logger.LogInformation("[NOTIFY-003] Notification result for lead {LeadId}: ChatSent={ChatSent}, EmailSent={EmailSent}",
            lead.Id, chatSent, emailSent);

        if (!chatSent && !emailSent)
        {
            logger.LogError("[NOTIFY-004] ALL notification channels failed for lead {LeadId}, agent {AgentId}. Lead was saved but agent was NOT notified.", lead.Id, agentId);
            throw new InvalidOperationException($"All notification channels failed for lead {lead.Id}");
        }
    }

    private async Task SendChatAsync(string? webhookUrl, Lead lead, LeadEnrichment enrichment, LeadScore score, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            logger.LogInformation("[NOTIFY-010] Google Chat webhook not configured for lead {LeadId} — skipping.", lead.Id);
            return;
        }

        try
        {
            var card = LeadChatCardRenderer.RenderNewLeadCard(lead, enrichment, score);
            var client = httpClientFactory.CreateClient("GoogleChat");
            var response = await client.PostAsJsonAsync(webhookUrl, card, ct);
            response.EnsureSuccessStatusCode();
            logger.LogInformation("[NOTIFY-011] Google Chat notification sent for lead {LeadId}.", lead.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[NOTIFY-012] Google Chat webhook failed for agent {AgentId}, lead {LeadId}.", lead.AgentId, lead.Id);
            throw; // re-throw so chatSuccess stays false
        }
    }

    private async Task SendEmailAsync(string accountId, string agentId, string agentEmail, Lead lead, LeadEnrichment enrichment, LeadScore score, CancellationToken ct)
    {
        var subject = BuildSubject(lead, enrichment, score);
        var body = BuildBody(lead, enrichment, score);

        string? sendError = null;

        try
        {
            logger.LogInformation("[NOTIFY-021] Sending email notification to {AgentEmailHash} for lead {LeadId}...", NotificationHelpers.HashEmail(agentEmail), lead.Id);
            await gmailSender.SendAsync(accountId, agentId, agentEmail, subject, body, ct);
            logger.LogInformation("[NOTIFY-022] Email notification sent for lead {LeadId}.", lead.Id);
        }
        catch (Exception ex)
        {
            sendError = ex.Message;
            logger.LogError(ex, "[NOTIFY-023] Gmail notification failed for agent {AgentId}, lead {LeadId}.", agentId, lead.Id);
            throw; // re-throw so emailSuccess stays false
        }
        finally
        {
            // Write email record to Drive — non-fatal
            try
            {
                var sent = sendError is null;
                var record = BuildEmailRecord(lead, subject, body, sent, sendError);
                var leadFolder = LeadPaths.LeadFolder(lead.FullName);
                var fileName = $"{DateTime.UtcNow:yyyy-MM-dd-HHmmss}-Lead Notification.md";
                await fanOutStorage.WriteDocumentAsync($"{leadFolder}/Communications", fileName, record, ct);
            }
            catch (Exception storageEx)
            {
                logger.LogError(storageEx, "[NOTIFY-024] Failed to write email record for lead {LeadId}, agent {AgentId}.", lead.Id, agentId);
            }
        }
    }

    internal static string BuildEmailRecord(Lead lead, string subject, string body, bool sent, string? error)
    {
        var sb = new StringBuilder();

        sb.AppendLine("---");
        sb.AppendLine($"leadId: {lead.Id}");
        sb.AppendLine($"sentAt: {DateTime.UtcNow:o}");
        sb.AppendLine($"subject: \"{NotificationHelpers.EscapeYaml(subject)}\"");
        sb.AppendLine($"recipientEmailHash: {NotificationHelpers.HashEmail(lead.AgentId)}");
        sb.AppendLine($"sent: {sent.ToString().ToLowerInvariant()}");
        if (error is not null)
            sb.AppendLine($"error: \"{NotificationHelpers.EscapeYaml(error)}\"");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(body);

        return sb.ToString();
    }

    public string BuildSubject(Lead lead, LeadEnrichment enrichment, LeadScore score)
        => $"New Lead: {lead.FullName} — {enrichment.MotivationCategory} (Score: {score.OverallScore})";

    // CAN-SPAM classification: transactional (agent receiving notification about their own incoming lead).
    // Not commercial marketing — no unsubscribe footer or physical address required.
    public string BuildBody(Lead lead, LeadEnrichment enrichment, LeadScore score)
    {
        // HtmlEncode all user-supplied fields — this body is sent as htmlBody to Gmail.
        static string H(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

        var sb = new StringBuilder();

        sb.AppendLine($"# New Lead: {H(lead.FullName)}");
        sb.AppendLine();
        sb.AppendLine($"**Score:** {score.OverallScore} / 100");
        sb.AppendLine($"**Motivation:** {H(enrichment.MotivationCategory)}");
        sb.AppendLine($"**Lead Type:** {H(lead.LeadType.ToString())}");
        sb.AppendLine($"**Received:** {lead.ReceivedAt:MMMM d, yyyy h:mm tt} UTC");
        sb.AppendLine();

        // Contact
        sb.AppendLine("## Contact");
        sb.AppendLine($"- **Email:** {H(lead.Email)}");
        sb.AppendLine($"- **Phone:** {H(lead.Phone)}");
        sb.AppendLine($"- **Timeline:** {H(lead.Timeline)}");
        sb.AppendLine();

        // Seller section — only when seller details are present
        if (lead.SellerDetails is { } s)
        {
            sb.AppendLine("## Selling");
            sb.AppendLine($"- **Address:** {H(s.Address)}, {H(s.City)}, {H(s.State)} {H(s.Zip)}");
            if (s.PropertyType is not null) sb.AppendLine($"- **Property Type:** {H(s.PropertyType)}");
            if (s.Condition is not null)    sb.AppendLine($"- **Condition:** {H(s.Condition)}");
            if (s.AskingPrice is not null)  sb.AppendLine($"- **Asking Price:** ${s.AskingPrice.Value:N0}");
            sb.AppendLine();
        }

        // Buyer section — only when buyer details are present
        if (lead.BuyerDetails is { } b)
        {
            sb.AppendLine("## Buying");
            sb.AppendLine($"- **Desired Area:** {H(b.City)}, {H(b.State)}");
            if (b.MaxBudget is not null)              sb.AppendLine($"- **Max Budget:** ${b.MaxBudget.Value:N0}");
            if (b.Bedrooms is not null)               sb.AppendLine($"- **Bedrooms:** {b.Bedrooms}");
            if (b.Bathrooms is not null)              sb.AppendLine($"- **Bathrooms:** {b.Bathrooms}");
            if (b.PropertyTypes is { Count: > 0 })   sb.AppendLine($"- **Property Types:** {string.Join(", ", b.PropertyTypes.Select(H))}");
            if (b.MustHaves is { Count: > 0 })       sb.AppendLine($"- **Must-Haves:** {string.Join(", ", b.MustHaves.Select(H))}");
            sb.AppendLine();
        }

        // Enrichment summary
        sb.AppendLine("## Enrichment Summary");
        sb.AppendLine($"**Motivation Analysis:** {H(enrichment.MotivationAnalysis)}");
        sb.AppendLine();
        sb.AppendLine($"**Professional Background:** {H(enrichment.ProfessionalBackground)}");
        sb.AppendLine();
        sb.AppendLine($"**Financial Indicators:** {H(enrichment.FinancialIndicators)}");
        sb.AppendLine();
        sb.AppendLine($"**Timeline Pressure:** {H(enrichment.TimelinePressure)}");
        sb.AppendLine();

        // Cold call openers
        if (enrichment.ColdCallOpeners.Count > 0)
        {
            sb.AppendLine("## Cold Call Openers");
            foreach (var opener in enrichment.ColdCallOpeners)
                sb.AppendLine($"- {H(opener)}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

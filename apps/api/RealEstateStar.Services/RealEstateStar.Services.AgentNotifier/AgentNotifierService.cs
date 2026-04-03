using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Senders;

namespace RealEstateStar.Services.AgentNotifier;

public class AgentNotifierService(
    IWhatsAppSender whatsAppSender,
    IGmailSender gmailSender,
    IIdempotencyStore idempotencyStore,
    ILogger<AgentNotifierService> logger,
    IAgentContextLoader? agentContextLoader = null) : IAgentNotifier
{
    public async Task NotifyAsync(Lead lead, LeadScore score,
        CmaWorkerResult? cmaResult, HomeSearchWorkerResult? homeSearchResult,
        AgentNotificationConfig agentConfig, CancellationToken ct)
    {
        using var span = AgentNotifierDiagnostics.ActivitySource.StartActivity("activity.send_agent_notification");
        span?.SetTag("lead.id", lead.Id.ToString());
        span?.SetTag("agent.id", agentConfig.AgentId);

        var idempotencyKey = $"lead:{agentConfig.AgentId}-{lead.Id}:agent-notify";
        if (await idempotencyStore.HasCompletedAsync(idempotencyKey, ct))
        {
            logger.LogWarning(
                "[SEND-025] Skipping duplicate agent notification for lead {LeadId} agent {AgentId} (idempotency guard)",
                lead.Id, agentConfig.AgentId);
            return;
        }

        // Load agent activation context for voice + brand voice application
        AgentContext? agentContext = null;
        if (agentContextLoader is not null)
        {
            try
            {
                agentContext = await agentContextLoader.LoadAsync(agentConfig.AgentId, agentConfig.AgentId, ct);
                if (agentContext is not null)
                {
                    logger.LogInformation(
                        "[CTX-030] Agent context loaded for notification. AgentId: {AgentId}, Lead: {LeadId}",
                        agentConfig.AgentId, lead.Id);
                }
                else
                {
                    logger.LogInformation(
                        "[CTX-031] Agent context unavailable for notification — using generic format. AgentId: {AgentId}",
                        agentConfig.AgentId);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "[CTX-032] Agent context load failed for notification; continuing without. AgentId: {AgentId}",
                    agentConfig.AgentId);
            }
        }

        if (!string.IsNullOrEmpty(agentConfig.WhatsAppPhoneNumberId))
        {
            try
            {
                await whatsAppSender.SendTemplateAsync(
                    agentConfig.WhatsAppPhoneNumberId,
                    "new_lead_notification",
                    BuildTemplateParameters(lead, score, cmaResult, homeSearchResult),
                    ct);

                AgentNotifierDiagnostics.WhatsAppSuccess.Add(1);
                await idempotencyStore.MarkCompletedAsync(idempotencyKey, ct);
                logger.LogInformation(
                    "[SEND-020] Agent notified via WhatsApp for {AgentId}, lead {LeadId}",
                    agentConfig.AgentId, lead.Id);
                return;
            }
            catch (Exception ex)
            {
                AgentNotifierDiagnostics.WhatsAppFailed.Add(1);
                AgentNotifierDiagnostics.EmailFallback.Add(1);
                logger.LogWarning(ex,
                    "[SEND-021] WhatsApp failed for {AgentId}, falling back to email",
                    agentConfig.AgentId);
            }
        }

        try
        {
            var html = BuildAgentNotificationEmail(lead, score, cmaResult, homeSearchResult, agentConfig, agentContext);
            await gmailSender.SendAsync(
                agentConfig.AgentId,
                agentConfig.AgentId,
                agentConfig.Email,
                "New Lead Notification",
                html,
                ct);
            await idempotencyStore.MarkCompletedAsync(idempotencyKey, ct);
        }
        catch (Exception ex)
        {
            span?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex,
                "[SEND-022] Both WhatsApp and email failed for {AgentId}",
                agentConfig.AgentId);
        }
    }


    internal static List<(string type, string value)> BuildTemplateParameters(
        Lead lead, LeadScore score,
        CmaWorkerResult? cmaResult, HomeSearchWorkerResult? homeSearchResult)
    {
        var parameters = new List<(string type, string value)>
        {
            ("text", lead.FullName),
            ("text", lead.Phone),
            ("text", lead.Email),
            ("text", lead.Timeline),
            ("text", score.OverallScore.ToString()),
            ("text", score.Bucket)
        };

        if (lead.Locale is not null && lead.Locale != "en")
            parameters.Add(("text", $"Language: {GetLanguageDisplayName(lead.Locale)}"));

        if (lead.SellerDetails is { } seller)
        {
            var address = $"{seller.Address}, {seller.City}, {seller.State} {seller.Zip}";
            parameters.Add(("text", address));

            if (cmaResult?.EstimatedValue is { } estimated)
                parameters.Add(("text", $"Est. Value: ${estimated:N0}"));

            if (cmaResult?.Comps is { } comps)
                parameters.Add(("text", $"{comps.Count} comps analyzed"));
        }

        if (lead.BuyerDetails is { } buyer)
        {
            parameters.Add(("text", $"{buyer.City}, {buyer.State}"));

            if (buyer.MinBudget is not null || buyer.MaxBudget is not null)
            {
                var range = $"${buyer.MinBudget:N0} - ${buyer.MaxBudget:N0}";
                parameters.Add(("text", range));
            }

            if (homeSearchResult?.Listings is { } listings)
                parameters.Add(("text", $"{listings.Count} listings found"));
        }

        return parameters;
    }

    internal static string BuildAgentNotificationEmail(
        Lead lead, LeadScore score,
        CmaWorkerResult? cmaResult, HomeSearchWorkerResult? homeSearchResult,
        AgentNotificationConfig agentConfig, AgentContext? agentContext = null)
    {
        var scoreColor = score.Bucket switch
        {
            "Hot" => "#dc2626",
            "Warm" => "#f97316",
            _ => "#3b82f6"
        };

        var sellerSection = lead.SellerDetails is { } seller
            ? BuildSellerSection(seller, cmaResult)
            : string.Empty;

        var buyerSection = lead.BuyerDetails is { } buyer
            ? BuildBuyerSection(buyer, homeSearchResult)
            : string.Empty;

        var activationBadge = agentContext?.IsActivated == true
            ? " \u00b7 <span style=\"color:#16a34a;\">Voice-Personalized</span>"
            : string.Empty;

        return "<html>" +
               "<head>" +
               "<meta charset=\"utf-8\" />" +
               "<style>" +
               "body { font-family: Arial, sans-serif; margin: 0; padding: 0; background: #f9fafb; }" +
               ".container { max-width: 600px; margin: 24px auto; background: #ffffff; border-radius: 8px; overflow: hidden; }" +
               $".header {{ background: {SafeCssColor(agentConfig.PrimaryColor)}; padding: 24px; color: #ffffff; }}" +
               ".header h1 { margin: 0; font-size: 20px; }" +
               ".header p { margin: 4px 0 0; font-size: 14px; opacity: 0.85; }" +
               ".body { padding: 24px; }" +
               $".score-badge {{ display: inline-block; padding: 4px 12px; border-radius: 9999px; color: #ffffff; font-weight: bold; background: {scoreColor}; }}" +
               ".section { margin-top: 20px; border-top: 1px solid #e5e7eb; padding-top: 16px; }" +
               $".section h2 {{ margin: 0 0 12px; font-size: 16px; color: {SafeCssColor(agentConfig.PrimaryColor)}; }}" +
               ".field { margin-bottom: 8px; font-size: 14px; color: #374151; }" +
               ".field strong { color: #111827; }" +
               ".footer { padding: 16px 24px; background: #f3f4f6; font-size: 12px; color: #6b7280; }" +
               "</style>" +
               "</head>" +
               "<body>" +
               "<div class=\"container\">" +
               "<div class=\"header\">" +
               $"<h1>New Lead \u2014 {agentConfig.Name}</h1>" +
               $"<p>{agentConfig.BrokerageName}</p>" +
               "</div>" +
               "<div class=\"body\">" +
               "<div class=\"section\">" +
               "<h2>Lead Overview</h2>" +
               $"<div class=\"field\"><strong>Name:</strong> {H(lead.FullName)}</div>" +
               $"<div class=\"field\"><strong>Phone:</strong> {H(lead.Phone)}</div>" +
               $"<div class=\"field\"><strong>Email:</strong> {H(lead.Email)}</div>" +
               $"<div class=\"field\"><strong>Timeline:</strong> {H(lead.Timeline)}</div>" +
               $"<div class=\"field\"><strong>Score:</strong> <span class=\"score-badge\">{score.OverallScore} \u00b7 {H(score.Bucket)}</span></div>" +
               $"<div class=\"field\"><strong>Notes:</strong> {(lead.Notes is not null ? H(lead.Notes) : "\u2014")}</div>" +
               (lead.Locale is not null && lead.Locale != "en"
                   ? $"<div class=\"field\"><strong>Language Preference:</strong> {H(GetLanguageDisplayName(lead.Locale))}</div>"
                   : string.Empty) +
               "</div>" +
               sellerSection +
               buyerSection +
               "</div>" +
               $"<div class=\"footer\">Sent by Real Estate Star \u00b7 {agentConfig.Name} \u00b7 {agentConfig.Email}{activationBadge}</div>" +
               "</div>" +
               "</body>" +
               "</html>";
    }

    private static string BuildSellerSection(SellerDetails seller, CmaWorkerResult? cmaResult)
    {
        var address = $"{H(seller.Address)}, {H(seller.City)}, {H(seller.State)} {H(seller.Zip)}";
        var estimatedValue = cmaResult?.EstimatedValue is { } v ? $"${v:N0}" : "\u2014";
        var compCount = cmaResult?.Comps?.Count.ToString() ?? "\u2014";
        var marketAnalysis = cmaResult?.MarketAnalysis is not null ? H(cmaResult.MarketAnalysis) : "\u2014";

        var bedsRow = seller.Beds.HasValue
            ? $"<div class=\"field\"><strong>Beds / Baths:</strong> {seller.Beds} bd / {seller.Baths} ba</div>"
            : string.Empty;
        var sqftRow = seller.Sqft.HasValue
            ? $"<div class=\"field\"><strong>Size:</strong> {seller.Sqft:N0} sqft</div>"
            : string.Empty;
        var askingRow = seller.AskingPrice.HasValue
            ? $"<div class=\"field\"><strong>Asking Price:</strong> ${seller.AskingPrice:N0}</div>"
            : string.Empty;

        return "<div class=\"section\">" +
               "<h2>Seller Details</h2>" +
               $"<div class=\"field\"><strong>Property:</strong> {address}</div>" +
               bedsRow +
               sqftRow +
               askingRow +
               $"<div class=\"field\"><strong>CMA Estimated Value:</strong> {estimatedValue}</div>" +
               $"<div class=\"field\"><strong>Comps Analyzed:</strong> {compCount}</div>" +
               $"<div class=\"field\"><strong>Market Analysis:</strong> {marketAnalysis}</div>" +
               "</div>";
    }

    private static string BuildBuyerSection(BuyerDetails buyer, HomeSearchWorkerResult? homeSearchResult)
    {
        var priceRange = (buyer.MinBudget, buyer.MaxBudget) switch
        {
            ({ } min, { } max) => $"${min:N0} - ${max:N0}",
            ({ } min, null) => $"${min:N0}+",
            (null, { } max) => $"Up to ${max:N0}",
            _ => "\u2014"
        };

        var listingCount = homeSearchResult?.Listings?.Count.ToString() ?? "\u2014";
        var areaSummary = homeSearchResult?.AreaSummary is not null ? H(homeSearchResult.AreaSummary) : "\u2014";

        var bedsRow = buyer.Bedrooms.HasValue
            ? $"<div class=\"field\"><strong>Bedrooms:</strong> {buyer.Bedrooms}+</div>"
            : string.Empty;
        var preApprovalRow = buyer.PreApproved is not null
            ? $"<div class=\"field\"><strong>Pre-Approved:</strong> {buyer.PreApproved}</div>"
            : string.Empty;

        return "<div class=\"section\">" +
               "<h2>Buyer Details</h2>" +
               $"<div class=\"field\"><strong>Search Area:</strong> {H(buyer.City)}, {H(buyer.State)}</div>" +
               $"<div class=\"field\"><strong>Price Range:</strong> {priceRange}</div>" +
               bedsRow +
               preApprovalRow +
               $"<div class=\"field\"><strong>Listings Found:</strong> {listingCount}</div>" +
               $"<div class=\"field\"><strong>Area Summary:</strong> {areaSummary}</div>" +
               "</div>";
    }

    internal static string GetLanguageDisplayName(string? locale) => locale?.ToLowerInvariant() switch
    {
        "es" => "Spanish",
        "fr" => "French",
        "zh" => "Chinese",
        "ko" => "Korean",
        "vi" => "Vietnamese",
        "ht" => "Haitian Creole",
        _ => locale ?? "English"
    };

    private static string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

    internal static string SafeCssColor(string? color, string fallback = "#1E3A5F")
    {
        if (string.IsNullOrWhiteSpace(color)) return fallback;
        var trimmed = color.TrimStart('#');
        return trimmed.Length is 3 or 6 && trimmed.All(c => char.IsAsciiHexDigit(c))
            ? $"#{trimmed}" : fallback;
    }
}

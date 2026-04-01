using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Services.WelcomeNotification;

/// <summary>
/// Sends a personalized welcome message to the agent via WhatsApp first,
/// falling back to email via IGmailSender.
/// Content is drafted using Claude with Voice Skill + Personality + Brand Voice + Coaching.
/// Under 150 words, opens with the agent's catchphrase, closes with their sign-off.
/// Idempotent — tracks sent flag via IFileStorageProvider; does not re-send on re-activation.
/// </summary>
public sealed class WelcomeNotificationService(
    IWhatsAppSender whatsAppSender,
    IGmailSender gmailSender,
    IAnthropicClient anthropic,
    IFileStorageProvider storage,
    ILogger<WelcomeNotificationService> logger) : IWelcomeNotificationService
{
    internal const string FolderPrefix = "real-estate-star";
    internal const string WelcomeSentFile = "Welcome Sent.md";
    internal const string Model = "claude-opus-4-6";
    internal const string Pipeline = "welcome-notification";
    internal const int MaxTokens = 600;

    public async Task SendAsync(
        string accountId,
        string agentId,
        string handle,
        ActivationOutputs outputs,
        CancellationToken ct)
    {
        var agentFolder = $"{FolderPrefix}/{agentId}";

        // Idempotency check — skip if already sent
        var existing = await storage.ReadDocumentAsync(agentFolder, WelcomeSentFile, ct);
        if (existing is not null)
        {
            logger.LogInformation(
                "[WELCOME-020] Welcome already sent for agentId={AgentId}, skipping", agentId);
            return;
        }

        logger.LogInformation("[WELCOME-010] Drafting welcome message for agentId={AgentId}", agentId);

        var message = await DraftMessageAsync(accountId, agentId, handle, outputs, ct);

        var agentPhone = outputs.AgentPhone;
        var whatsAppEnabled = outputs.Discovery?.WhatsAppEnabled == true;

        var sent = false;

        // Try WhatsApp first
        if (whatsAppEnabled && !string.IsNullOrWhiteSpace(agentPhone))
        {
            try
            {
                await whatsAppSender.SendFreeformAsync(agentPhone, message, ct);
                sent = true;
                logger.LogInformation(
                    "[WELCOME-030] Welcome sent via WhatsApp for agentId={AgentId}", agentId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "[WELCOME-031] WhatsApp failed for agentId={AgentId}, falling back to email", agentId);
            }
        }

        // Fallback to email
        if (!sent)
        {
            var agentEmail = outputs.AgentEmail;
            if (!string.IsNullOrWhiteSpace(agentEmail))
            {
                try
                {
                    var htmlBody = WrapHtml(message, outputs.AgentName);
                    await gmailSender.SendAsync(
                        accountId, agentId, agentEmail,
                        "Welcome to Real Estate Star! The premier AI automation platform for real estate agents.",
                        htmlBody, ct);
                    sent = true;
                    logger.LogInformation(
                        "[WELCOME-032] Welcome sent via email for agentId={AgentId}", agentId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "[WELCOME-033] Both WhatsApp and email failed for agentId={AgentId}", agentId);
                }
            }
        }

        // Record sent flag (even if both channels failed — avoids retry spam)
        if (sent)
        {
            var record = $"---\nsent: true\nsent_at: {DateTime.UtcNow:O}\n---\n\n{message}";
            await storage.WriteDocumentAsync(agentFolder, WelcomeSentFile, record, ct);
            logger.LogInformation("[WELCOME-090] Welcome sent flag recorded for agentId={AgentId}", agentId);
        }
    }

    // ── Message drafting ──────────────────────────────────────────────────────

    private async Task<string> DraftMessageAsync(
        string accountId,
        string agentId,
        string handle,
        ActivationOutputs outputs,
        CancellationToken ct)
    {
        var isSingleAgent = accountId == agentId;
        var agentSiteUrl = isSingleAgent
            ? $"https://{handle}.real-estate-star.com"
            : $"https://{accountId}.real-estate-star.com/agents/{agentId}";

        const string systemPrompt =
            "You are writing a personalized welcome email from Real Estate Star to a real estate " +
            "agent who just connected their Google account and activated the platform for the first time.\n\n" +
            "CONTEXT: Real Estate Star is an AI-powered platform that automates real estate agent " +
            "workflows — from instant lead response and CMA generation to personalized agent websites " +
            "and smart follow-up. The agent's site is already live and ready to capture leads.\n\n" +
            "CRITICAL RULES:\n" +
            "- ONLY mention Real Estate Star by name. Do NOT reference any other company, " +
            "bank, brand, or organization found in the agent's data — those are from their " +
            "personal email and must NEVER appear in this welcome message.\n" +
            "- Under 200 words total\n" +
            "- MUST include a catchphrase or signature phrase from the Voice Skill data. " +
            "If the Voice Skill contains a catchphrase, tagline, or sign-off, weave it naturally " +
            "into the message. This makes the email feel personal and shows we understand the agent.\n" +
            "- Open with a warm welcome using the agent's first name\n" +
            "- Convey excitement — their automation is live, their site is ready\n" +
            "- Briefly highlight what's now working for them (auto lead response, CMA generation, " +
            "personalized website) — make them feel the value immediately\n" +
            "- Include one actionable coaching tip relevant to their pipeline\n" +
            "- Include their agent site URL with (beta) appended to the end\n" +
            "- Close with an encouraging, forward-looking sign-off from Real Estate Star\n" +
            "- TONE: Confident, professional, and polished. Real Estate Star is the best partner " +
            "an agent can have to expand their business. Convey that this platform is built to " +
            "help them win more clients, close more deals, and grow. Not salesy — authoritative.\n" +
            "- Plain text only, no markdown, no HTML";

        var voiceContext = outputs.VoiceSkill ?? "professional and approachable tone";
        var personalityContext = outputs.PersonalitySkill ?? "dedicated REALTOR";
        var pipelineContext = outputs.SalesPipeline ?? "strong pipeline management";
        var coachingContext = outputs.CoachingReport ?? "focus on follow-up";
        var agentName = outputs.AgentName ?? agentId;

        var userMessage =
            $"Write a welcome message for {agentName}.\n\n" +
            $"Voice Skill:\n{voiceContext}\n\n" +
            $"Personality:\n{personalityContext}\n\n" +
            $"Sales Pipeline insight:\n{pipelineContext}\n\n" +
            $"Coaching tip:\n{coachingContext}\n\n" +
            $"Agent site URL: {agentSiteUrl}\n\n" +
            "Write the welcome message now. Plain text, under 150 words.";

        var response = await anthropic.SendAsync(
            Model, systemPrompt, userMessage, MaxTokens, Pipeline, ct);

        return response.Content.Trim();
    }

    // ── HTML wrapper for email ────────────────────────────────────────────────

    internal static string WrapHtml(string message, string? agentName)
    {
        // Strip any Claude-generated header/greeting line that duplicates our hardcoded h2
        var cleaned = StripFirstLineIfHeader(message);
        var encoded = System.Net.WebUtility.HtmlEncode(cleaned).Replace("\n", "<br />");

        var firstName = agentName?.Split(' ').FirstOrDefault() ?? "";
        var greeting = string.IsNullOrEmpty(firstName)
            ? "Welcome to Real Estate Star!"
            : $"Welcome to Real Estate Star, {System.Net.WebUtility.HtmlEncode(firstName)}!";

        return
            "<html><body style=\"font-family:Arial,sans-serif;max-width:600px;margin:24px auto;\">" +
            $"<h2>{greeting}</h2>" +
            "<p style=\"font-size:14px;color:#555;\">The premier AI automation platform for real estate agents.</p>" +
            $"<p>{encoded}</p>" +
            "<p style=\"color:#6b7280;font-size:12px;\">Sent by Real Estate Star</p>" +
            "</body></html>";
    }

    /// <summary>
    /// Strips the first line if it looks like a greeting/header that Claude generated
    /// (e.g., "Welcome to Real Estate Star, Thank you for choosing...").
    /// Prevents duplicate headers in the email.
    /// </summary>
    internal static string StripFirstLineIfHeader(string message)
    {
        var lines = message.Split('\n', 2);
        if (lines.Length < 2) return message;

        var firstLine = lines[0].Trim();
        if (firstLine.StartsWith("Welcome", StringComparison.OrdinalIgnoreCase) ||
            firstLine.StartsWith("Thank you", StringComparison.OrdinalIgnoreCase) ||
            firstLine.StartsWith("Congratulations", StringComparison.OrdinalIgnoreCase))
        {
            return lines[1].TrimStart();
        }

        return message;
    }
}

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
    internal const string Model = "claude-haiku-4-5";
    internal const string Pipeline = "welcome-notification";
    internal const int MaxTokens = 400;

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
                        "Welcome to Real Estate Star!", htmlBody, ct);
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
            "You are writing a personalized welcome message for a real estate agent " +
            "who just activated their Real Estate Star account. " +
            "Rules:\n" +
            "- Under 150 words total\n" +
            "- Open with the agent's catchphrase or their signature sign-on style\n" +
            "- Include 1-2 sentence brand synthesis\n" +
            "- Include one insight about their sales pipeline (from the pipeline skill)\n" +
            "- Include 1 coaching tip (from the coaching report)\n" +
            "- Include their agent site URL\n" +
            "- Close with their sign-off style\n" +
            "- Warm, personal, NOT corporate. Use their own voice.\n" +
            "- Plain text only, no markdown";

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
        var encoded = System.Net.WebUtility.HtmlEncode(message).Replace("\n", "<br />");
        return
            "<html><body style=\"font-family:Arial,sans-serif;max-width:600px;margin:24px auto;\">" +
            $"<h2>Welcome to Real Estate Star{(agentName is not null ? $", {System.Net.WebUtility.HtmlEncode(agentName)}" : "")}!</h2>" +
            $"<p>{encoded}</p>" +
            "<p style=\"color:#6b7280;font-size:12px;\">Sent by Real Estate Star</p>" +
            "</body></html>";
    }
}

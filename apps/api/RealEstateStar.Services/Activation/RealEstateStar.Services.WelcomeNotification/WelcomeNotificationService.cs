using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Services.WelcomeNotification;

/// <summary>
/// Sends a personalized welcome message to the agent via WhatsApp first,
/// falling back to email via IGmailSender.
/// Content is drafted using Claude with Voice Skill + Personality + Brand Voice + Coaching.
/// Under 150 words, opens with the agent's catchphrase, closes with their sign-off.
/// Idempotent — tracks sent flag via IFileStorageProvider and IIdempotencyStore; does not re-send on re-activation.
/// </summary>
public sealed class WelcomeNotificationService(
    IWhatsAppSender whatsAppSender,
    IGmailSender gmailSender,
    IAnthropicClient anthropic,
    IFileStorageProvider storage,
    IIdempotencyStore idempotencyStore,
    ILogger<WelcomeNotificationService> logger) : IWelcomeNotificationService
{
    internal const string FolderPrefix = "real-estate-star";
    internal const string WelcomeSentFile = "Welcome Sent.md";
    internal const string Model = "claude-sonnet-4-6";
    internal const string Pipeline = "welcome-notification";
    internal const int MaxTokens = 1200;

    public async Task SendAsync(
        string accountId,
        string agentId,
        string handle,
        ActivationOutputs outputs,
        CancellationToken ct)
    {
        var agentFolder = $"{FolderPrefix}/{agentId}";

        // Belt-and-suspenders idempotency guard (fast path — no storage read on replay)
        var idempotencyKey = $"activation:{agentId}:welcome-notification";
        if (await idempotencyStore.HasCompletedAsync(idempotencyKey, ct))
        {
            logger.LogInformation(
                "[WELCOME-021] Welcome already sent for agentId={AgentId}, skipping (idempotency store)", agentId);
            return;
        }

        // File-based idempotency check — authoritative record
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
            await idempotencyStore.MarkCompletedAsync(idempotencyKey, ct);
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

        var personalityContext = outputs.PersonalitySkill;
        var voiceContext = outputs.VoiceSkill;
        var pipelineContext = BuildPipelineContext(outputs.PipelineJson);
        var coachingContext = outputs.CoachingReport;
        var agentName = outputs.AgentName ?? agentId;

        var systemPrompt =
            "You are writing a personalized welcome email from Real Estate Star to a real estate " +
            "agent who just activated the platform.\n\n" +
            "CONTEXT: Real Estate Star is an AI-powered platform that automates real estate agent " +
            "workflows — instant lead response, CMA generation, personalized websites, smart follow-up.\n\n" +
            (personalityContext != null
                ? $"AGENT PERSONALITY:\n{personalityContext}\n" +
                  "INSTRUCTION: Match this agent's personality traits in your writing. " +
                  "If their warmth is high, be genuinely warm. If confidence is high, be assertive. " +
                  "If humor usage is none, do not use humor.\n\n"
                : "") +
            (voiceContext != null
                ? $"AGENT VOICE & CORE DIRECTIVE:\n{voiceContext}\n" +
                  "INSTRUCTION: Use their catchphrase or sign-off naturally if one exists. " +
                  "Reference their core directive traits (e.g., 'client-first', 'deal-making').\n\n"
                : "") +
            "CRITICAL RULES:\n" +
            "- ONLY mention Real Estate Star by name\n" +
            "- DO NOT reference any other company or brand from the agent's data\n" +
            "- Include ALL 4 sections below\n" +
            "- Under 300 words total\n" +
            "- Plain text only, no markdown, no HTML\n\n" +
            "SECTIONS (include all 4):\n" +
            "1. Personalized greeting using agent's name and reflecting their personality\n" +
            "2. 'We found your leads' — reference specific lead(s) by name and property from pipeline data\n" +
            "3. One concrete coaching insight with real numbers and an industry benchmark\n" +
            "4. What Real Estate Star will do for them specifically, include their site URL";

        var userMessage =
            $"Write a welcome email for {agentName}.\n\n" +
            $"Agent site URL: {agentSiteUrl}\n\n" +
            (pipelineContext != null ? $"Pipeline Data (their current leads):\n{pipelineContext}\n\n" : "") +
            (coachingContext != null ? $"Coaching Analysis:\n{coachingContext}\n\n" : "") +
            "Write the welcome email now. Plain text, under 300 words, all 4 sections.";

        var response = await anthropic.SendAsync(
            Model, systemPrompt, userMessage, MaxTokens, Pipeline, ct);

        return response.Content.Trim();
    }

    // ── Pipeline context builder ─────────────────────────────────────────────

    internal static string? BuildPipelineContext(string? pipelineJson)
    {
        if (string.IsNullOrWhiteSpace(pipelineJson)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(pipelineJson);
            if (!doc.RootElement.TryGetProperty("leads", out var leads)) return null;

            var sb = new System.Text.StringBuilder();
            var count = 0;
            foreach (var lead in leads.EnumerateArray())
            {
                if (count >= 3) break;
                var name = lead.TryGetProperty("name", out var n) ? n.GetString() : null;
                var stage = lead.TryGetProperty("stage", out var s) ? s.GetString() : null;
                var property = lead.TryGetProperty("property", out var p) ? p.GetString() : null;
                var type = lead.TryGetProperty("type", out var t) ? t.GetString() : null;
                sb.AppendLine($"- {name ?? "Unknown"}: {type ?? "lead"} at {property ?? "unknown"} — stage: {stage ?? "unknown"}");
                count++;
            }
            return count > 0 ? sb.ToString() : null;
        }
        catch { return null; }
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

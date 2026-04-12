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

        // Send via email (WhatsApp disabled — not yet operational)
        var agentEmail = outputs.AgentEmail;
        if (string.IsNullOrWhiteSpace(agentEmail))
        {
            throw new InvalidOperationException(
                $"[WELCOME-034] No email address available for agentId={agentId}. " +
                "Cannot send welcome email — aborting so Durable Functions retries.");
        }

        // Let exceptions propagate — Durable Functions will retry the activity on failure.
        // This ensures transient issues (token refresh, network, DPAPI key rotation) are retried
        // instead of silently dropping the welcome email.
        var htmlBody = WrapHtml(message, outputs.AgentName);
        await gmailSender.SendAsync(
            accountId, agentId, agentEmail,
            "Welcome to Real Estate Star! The premier AI automation platform for real estate agents.",
            htmlBody, ct);

        logger.LogInformation(
            "[WELCOME-032] Welcome sent via email for agentId={AgentId}, to={Email}",
            agentId, agentEmail);

        // Record sent flag — prevents re-sending on future activations
        var record = $"---\nsent: true\nsent_at: {DateTime.UtcNow:O}\nchannel: email\nrecipient: {agentEmail}\n---\n\n{message}";
        await storage.WriteDocumentAsync(agentFolder, WelcomeSentFile, record, ct);
        await idempotencyStore.MarkCompletedAsync(idempotencyKey, ct);
        logger.LogInformation("[WELCOME-090] Welcome sent flag recorded for agentId={AgentId}", agentId);
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

        // Extract catchphrases from Voice Skill for direct injection into the prompt.
        // The full Voice Skill is 10-15KB — catchphrases get buried. Extracting them
        // as a short list forces Claude to actually use them.
        var catchphrases = ExtractCatchphrases(voiceContext);

        // Pull localized personality + voice for bilingual agents
        var localizedSkills = outputs.LocalizedSkills;
        string? spanishPersonality = null;
        string? spanishVoice = null;
        if (localizedSkills is not null)
        {
            localizedSkills.TryGetValue("PersonalitySkill.es", out spanishPersonality);
            localizedSkills.TryGetValue("VoiceSkill.es", out spanishVoice);
        }

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
            (catchphrases.Count > 0
                ? $"AGENT'S ACTUAL CATCHPHRASES (use at least one):\n" +
                  string.Join("\n", catchphrases.Select(p => $"- \"{p}\"")) + "\n" +
                  "INSTRUCTION: You MUST weave at least one of these exact phrases into the email. " +
                  "These are the agent's real words — using them makes the email feel personal.\n\n"
                : "") +
            (voiceContext != null
                ? $"AGENT VOICE & CORE DIRECTIVE:\n{voiceContext}\n" +
                  "INSTRUCTION: Match the agent's tone, formality, and communication style.\n\n"
                : "") +
            (spanishPersonality != null || spanishVoice != null
                ? "BILINGUAL AGENT CONTEXT:\n" +
                  "This agent serves clients in both English and Spanish. " +
                  "They have a distinct identity in each language.\n" +
                  (spanishPersonality != null
                      ? $"SPANISH PERSONALITY (cultural heritage, signature expressions, connection style):\n{spanishPersonality}\n"
                      : "") +
                  (spanishVoice != null
                      ? $"SPANISH VOICE (catchphrases, greetings, sign-offs in Spanish):\n{spanishVoice}\n"
                      : "") +
                  "INSTRUCTION: Weave in ONE cultural reference or Spanish phrase that shows " +
                  "you understand who they are — their heritage, their community, their bilingual " +
                  "superpower. This should feel personal, not generic. Example: if they're Dominican, " +
                  "a quick '¡Pa'lante!' hits differently than a generic '¡Bienvenido!'.\n\n"
                : "") +
            "CRITICAL RULES:\n" +
            "- ONLY mention Real Estate Star by name\n" +
            "- DO NOT reference any other company or brand from the agent's data\n" +
            "- Include ALL sections below\n" +
            "- Under 300 words total\n" +
            "- Plain text only, no markdown, no HTML\n" +
            "- You MUST include at least one of the agent's actual catchphrases or signature phrases " +
            "from the Voice Skill data. Do NOT use generic phrases — use THEIR words.\n" +
            "- If the agent's data mentions 'Se Habla Español', Spanish, bilingual service, or any " +
            "non-English language capability, you MUST acknowledge this as a strength. Mention that " +
            "Real Estate Star will serve their clients in their preferred language.\n\n" +
            "SECTIONS (include all):\n" +
            "1. Personalized greeting using agent's name and one of their catchphrases or sign-off phrases\n" +
            "2. 'We found your leads' — reference specific lead(s) by name and property from pipeline data\n" +
            "3. One concrete coaching insight with real numbers and an industry benchmark\n" +
            "4. What Real Estate Star will do for them specifically, include their site URL\n" +
            "5. If agent data shows bilingual/multilingual service: mention that their platform will " +
            "serve leads in the right language automatically";

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

    // ── Catchphrase extraction ─────────────────────────────────────────────

    /// <summary>
    /// Extracts catchphrases from the Voice Skill markdown.
    /// Looks for lines in the "Signature Phrases" section that start with a number and contain quoted text.
    /// Returns up to 5 of the most distinctive phrases.
    /// </summary>
    internal static IReadOnlyList<string> ExtractCatchphrases(string? voiceSkill)
    {
        if (string.IsNullOrWhiteSpace(voiceSkill))
            return [];

        var phrases = new List<string>();
        var inSection = false;

        foreach (var line in voiceSkill.Split('\n'))
        {
            var trimmed = line.Trim();

            // Detect the "Signature Phrases" section header
            if (trimmed.StartsWith("## Signature Phrases", StringComparison.OrdinalIgnoreCase))
            {
                inSection = true;
                continue;
            }

            // Stop at the next section header
            if (inSection && trimmed.StartsWith("## "))
                break;

            if (!inSection)
                continue;

            // Extract quoted text from numbered list items: 1. "phrase here"
            var quoteMatch = System.Text.RegularExpressions.Regex.Match(trimmed,
                @"^\d+\.\s+""([^""]+)""");
            if (quoteMatch.Success)
                phrases.Add(quoteMatch.Groups[1].Value);
        }

        // Return up to 5, skip generic ones
        return phrases
            .Where(p => p.Length > 5 && p.Length < 100)
            .Take(5)
            .ToList();
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

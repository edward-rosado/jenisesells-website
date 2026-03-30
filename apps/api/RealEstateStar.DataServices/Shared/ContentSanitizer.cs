using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces;

namespace RealEstateStar.DataServices.Shared;

/// <summary>
/// Sanitizes untrusted external content before it is injected into LLM prompts.
/// Strips HTML, removes invisible Unicode, redacts sensitive data, and removes
/// prompt-injection patterns.
/// </summary>
public sealed partial class ContentSanitizer(ILogger<ContentSanitizer> logger) : IContentSanitizer
{
    public const int DefaultMaxLength = 50_000;

    // ── Compiled regexes ──────────────────────────────────────────────────────

    [GeneratedRegex(@"<[^>]*>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    // Invisible Unicode: zero-width space, ZWNJ, ZWJ, RTL/LTR override/embed/mark, BOM
    [GeneratedRegex(@"[\u200B\u200C\u200D\u200E\u200F\u202A\u202B\u202C\u202D\u202E\uFEFF]",
        RegexOptions.Compiled)]
    private static partial Regex InvisibleUnicodeRegex();

    // Base64 blocks: long runs of base64 chars (>100) often used to smuggle payloads
    [GeneratedRegex(@"[A-Za-z0-9+/]{100,}={0,2}", RegexOptions.Compiled)]
    private static partial Regex Base64BlockRegex();

    // Common prompt-injection phrases (case-insensitive)
    [GeneratedRegex(
        @"(?i)\b(ignore\s+(all\s+)?previous\s+instructions?|SYSTEM\s*:|ASSISTANT\s*:|Human\s*:|" +
        @"You\s+are\s+now\b|Act\s+as\b|Pretend\s+to\s+be\b)",
        RegexOptions.Compiled)]
    private static partial Regex InjectionPatternRegex();

    // Passwords: "password: secret" or "pwd = secret"
    [GeneratedRegex(@"(?i)(password|passwd|pwd)\s*[:=]\s*\S+", RegexOptions.Compiled)]
    private static partial Regex PasswordPatternRegex();

    // BIP-39 seed phrases: 12 or 24 word sequences of common seed words
    [GeneratedRegex(
        @"(?i)\b(abandon|ability|able|about|above|absent|absorb|abstract|absurd|abuse|access|" +
        @"accident|account|accuse|achieve|acid|acoustic|acquire|across|act|action|actor|actual|" +
        @"adapt|add|addict|address|adjust|admit|adult|advance|advice|aerobic|afford|afraid|" +
        @"again|age|agent|agree|ahead|aim|air|airport|aisle|alarm|album|alcohol|alert|alien|all|" +
        @"alley|allow|almost|alone|alpha|already|also|alter|always|amateur|amazing|among)\b" +
        @"(\s+\b\w+\b){11,23}",
        RegexOptions.Compiled)]
    private static partial Regex SeedPhrasePatternRegex();

    // Private keys: -----BEGIN ... KEY----- or hex strings 64+ chars
    [GeneratedRegex(@"-----BEGIN\s+[A-Z\s]+KEY-----|[0-9a-fA-F]{64,}", RegexOptions.Compiled)]
    private static partial Regex PrivateKeyRegex();

    // SSNs: 9 digit US SSNs in common formats
    [GeneratedRegex(@"\b\d{3}[-\s]?\d{2}[-\s]?\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex SsnRegex();

    // Credit card numbers: 13–19 digits, common separators
    [GeneratedRegex(@"\b(?:\d{4}[-\s]?){3}\d{4}\b", RegexOptions.Compiled)]
    private static partial Regex CreditCardRegex();

    // API keys: generic patterns like sk-xxx, pk_live_xxx, AKIA (AWS)
    [GeneratedRegex(@"\b(sk-[A-Za-z0-9]{20,}|pk_live_[A-Za-z0-9]{20,}|pk_test_[A-Za-z0-9]{20,}|AKIA[0-9A-Z]{16})\b",
        RegexOptions.Compiled)]
    private static partial Regex ApiKeyRegex();

    // ── Public surface ────────────────────────────────────────────────────────

    public string Sanitize(string untrustedContent) =>
        Sanitize(untrustedContent, DefaultMaxLength);

    public string Sanitize(string untrustedContent, int maxLength)
    {
        if (string.IsNullOrEmpty(untrustedContent))
            return string.Empty;

        var result = untrustedContent;

        // 1. Strip HTML — keep text content only
        result = HtmlTagRegex().Replace(result, " ");

        // 2. Remove invisible Unicode
        result = InvisibleUnicodeRegex().Replace(result, string.Empty);

        // 3. Remove base64 blocks (injection smuggling)
        result = Base64BlockRegex().Replace(result, "[base64-removed]");

        // 4. Strip prompt injection phrases
        result = InjectionPatternRegex().Replace(result, "[removed]");

        // 5. Redact sensitive data — log [ACTV-083] when found
        result = RedactSensitive(result);

        // 6. Truncate
        if (result.Length > maxLength)
            result = result[..maxLength];

        return result.Trim();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private string RedactSensitive(string input)
    {
        var result = input;
        var redacted = false;

        result = Redact(result, PasswordPatternRegex(), "[password-redacted]", ref redacted);
        result = Redact(result, SeedPhrasePatternRegex(), "[seed-phrase-redacted]", ref redacted);
        result = Redact(result, PrivateKeyRegex(), "[private-key-redacted]", ref redacted);
        result = Redact(result, SsnRegex(), "[ssn-redacted]", ref redacted);
        result = Redact(result, CreditCardRegex(), "[cc-redacted]", ref redacted);
        result = Redact(result, ApiKeyRegex(), "[api-key-redacted]", ref redacted);

        if (redacted)
            logger.LogWarning("[ACTV-083] Sensitive data detected and redacted from untrusted content");

        return result;
    }

    private static string Redact(string input, Regex pattern, string replacement, ref bool redacted)
    {
        if (!pattern.IsMatch(input))
            return input;

        redacted = true;
        return pattern.Replace(input, replacement);
    }
}

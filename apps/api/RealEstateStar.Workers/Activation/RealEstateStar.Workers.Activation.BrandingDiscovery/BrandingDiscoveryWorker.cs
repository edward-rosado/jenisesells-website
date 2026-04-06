using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Workers.Activation.BrandingDiscovery;

/// <summary>
/// Pure-compute worker that extracts a BrandingKit from website HTML and email signatures.
/// All extraction and template recommendation is deterministic — no Claude calls.
/// NO storage, NO DataServices.
/// </summary>
public sealed partial class BrandingDiscoveryWorker(
    ILogger<BrandingDiscoveryWorker> logger)
{
    // ── Compiled regexes ──────────────────────────────────────────────────────

    [GeneratedRegex(@"(?:color|background-color|background)\s*:\s*(#[0-9a-fA-F]{3,8}|rgb\([^)]+\))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ColorPropertyRegex();

    [GeneratedRegex(@"font-family\s*:\s*([^;}{]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex FontFamilyRegex();

    [GeneratedRegex(@"fonts\.googleapis\.com/css[^""']*family=([^""'&;]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex GoogleFontRegex();

    [GeneratedRegex(@"@font-face\s*\{[^}]*font-family\s*:\s*[""']?([^""';]+)[""']?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex FontFaceRegex();

    [GeneratedRegex(@"<meta[^>]+name\s*=\s*[""']theme-color[""'][^>]*content\s*=\s*[""']([^""']+)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ThemeColorMetaRegex();

    public Task<BrandingDiscoveryResult> DiscoverAsync(
        string agentName,
        AgentDiscovery agentDiscovery,
        EmailCorpus emailCorpus,
        DriveIndex driveIndex,
        CancellationToken ct)
    {
        logger.LogDebug(
            "[ACTV-030] Starting branding discovery for agent {AgentName}: {WebsiteCount} websites",
            agentName, agentDiscovery.Websites.Count);

        // Step 1: Deterministic extraction
        var colors = ExtractColors(agentDiscovery.Websites);
        var fonts = ExtractFonts(agentDiscovery.Websites);
        var logos = ExtractLogos(agentDiscovery, emailCorpus);

        // Step 2: Deterministic template recommendation — no Claude call needed
        var specialties = string.Join(", ", agentDiscovery.Profiles
            .SelectMany(p => p.Specialties)
            .Distinct());
        var (recommendedTemplate, templateReason) = ScoreTemplate(
            colors, fonts, agentDiscovery.Profiles, specialties);

        logger.LogDebug(
            "[ACTV-031] Branding discovery complete for {AgentName}: {ColorCount} colors, {FontCount} fonts, {LogoCount} logos, template={Template}",
            agentName, colors.Count, fonts.Count, logos.Count, recommendedTemplate);

        var brandingKit = new BrandingKit(colors, fonts, logos, recommendedTemplate, templateReason);

        var markdown = BuildBrandingKitMarkdown(agentName, brandingKit);

        return Task.FromResult(new BrandingDiscoveryResult(brandingKit, markdown));
    }

    // ── Deterministic extraction ──────────────────────────────────────────────

    internal IReadOnlyList<ColorEntry> ExtractColors(IReadOnlyList<DiscoveredWebsite> websites)
    {
        var colorMap = new Dictionary<string, ColorEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var site in websites)
        {
            if (string.IsNullOrEmpty(site.Html))
                continue;

            // theme-color meta tag — highest priority
            var themeMatch = ThemeColorMetaRegex().Match(site.Html);
            if (themeMatch.Success)
            {
                var hex = NormalizeColor(themeMatch.Groups[1].Value.Trim());
                if (!string.IsNullOrEmpty(hex))
                    colorMap.TryAdd(hex, new ColorEntry("Primary", hex, site.Source, "theme-color meta"));
            }

            // CSS color declarations
            var colorMatches = ColorPropertyRegex().Matches(site.Html);
            foreach (Match match in colorMatches)
            {
                var raw = match.Groups[1].Value.Trim();
                var hex = NormalizeColor(raw);
                if (!string.IsNullOrEmpty(hex) && !IsCommonSystemColor(hex))
                    colorMap.TryAdd(hex, new ColorEntry("Brand", hex, site.Source, "css"));
            }
        }

        return colorMap.Values.Take(10).ToList();
    }

    internal IReadOnlyList<FontEntry> ExtractFonts(IReadOnlyList<DiscoveredWebsite> websites)
    {
        var fontMap = new Dictionary<string, FontEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var site in websites)
        {
            if (string.IsNullOrEmpty(site.Html))
                continue;

            // Google Fonts links
            var googleMatches = GoogleFontRegex().Matches(site.Html);
            foreach (Match match in googleMatches)
            {
                var family = Uri.UnescapeDataString(match.Groups[1].Value.Trim())
                    .Replace("+", " ")
                    .Split(':')[0]
                    .Trim();
                if (!string.IsNullOrEmpty(family))
                    fontMap.TryAdd(family, new FontEntry("Body", family, "400", "google-fonts"));
            }

            // @font-face declarations
            var fontFaceMatches = FontFaceRegex().Matches(site.Html);
            foreach (Match match in fontFaceMatches)
            {
                var family = match.Groups[1].Value.Trim().Trim('"', '\'');
                if (!string.IsNullOrEmpty(family))
                    fontMap.TryAdd(family, new FontEntry("Display", family, "400", "font-face"));
            }

            // Inline font-family declarations
            var fontMatches = FontFamilyRegex().Matches(site.Html);
            foreach (Match match in fontMatches)
            {
                var raw = match.Groups[1].Value.Trim();
                var family = raw.Split(',')[0].Trim().Trim('"', '\'');
                if (!string.IsNullOrEmpty(family) && !IsSystemFont(family))
                    fontMap.TryAdd(family, new FontEntry("Body", family, "400", "css"));
            }
        }

        return fontMap.Values.Take(5).ToList();
    }

    internal IReadOnlyList<LogoVariant> ExtractLogos(AgentDiscovery agentDiscovery, EmailCorpus emailCorpus)
    {
        var logos = new List<LogoVariant>();

        if (agentDiscovery.LogoBytes is { Length: > 0 })
            logos.Add(new LogoVariant("full", "logo.png", agentDiscovery.LogoBytes, "agent-discovery"));

        if (agentDiscovery.HeadshotBytes is { Length: > 0 })
            logos.Add(new LogoVariant("headshot", "headshot.png", agentDiscovery.HeadshotBytes, "agent-discovery"));

        if (emailCorpus.Signature?.LogoUrl is not null)
            logos.Add(new LogoVariant("email-sig", "email-sig-logo.png", [], "email-signature-url"));

        return logos;
    }

    // ── Deterministic template recommendation ─────────────────────────────────

    /// <summary>
    /// Deterministic template recommendation based on brand signals — no Claude call needed.
    /// Scores each template (luxury, modern, warm, professional) based on color palette,
    /// font choices, market positioning, and specialties.
    /// </summary>
    internal static (string Template, string Reason) ScoreTemplate(
        IReadOnlyList<ColorEntry> colors,
        IReadOnlyList<FontEntry> fonts,
        IReadOnlyList<ThirdPartyProfile> profiles,
        string? specialties)
    {
        var scores = new Dictionary<string, int>
        {
            ["luxury"] = 0, ["modern"] = 0, ["warm"] = 0, ["professional"] = 0
        };

        // Color signals
        foreach (var color in colors)
        {
            var hex = color.Hex.TrimStart('#').ToLowerInvariant();
            if (hex.Length < 6) continue;

            var r = Convert.ToInt32(hex[..2], 16);
            var g = Convert.ToInt32(hex[2..4], 16);
            var b = Convert.ToInt32(hex[4..6], 16);
            var brightness = (r * 299 + g * 587 + b * 114) / 1000;

            // Dark palette → luxury
            if (brightness < 80) scores["luxury"] += 2;
            // Very bright/white → modern
            else if (brightness > 230) scores["modern"] += 1;
            // Warm tones (high red, medium green, low blue)
            else if (r > 150 && g > 80 && g < 180 && b < 120) scores["warm"] += 2;
            // Muted/neutral
            else if (Math.Abs(r - g) < 30 && Math.Abs(g - b) < 30) scores["professional"] += 1;

            // Gold/navy signals luxury
            if (r > 180 && g > 150 && b < 80) scores["luxury"] += 1; // gold
            if (r < 50 && g < 50 && b > 100) scores["luxury"] += 1; // navy
        }

        // Font signals
        foreach (var font in fonts)
        {
            var family = font.Family.ToLowerInvariant();
            // Serif headlines → luxury
            if (font.Role.Equals("Display", StringComparison.OrdinalIgnoreCase) ||
                font.Role.Equals("Headline", StringComparison.OrdinalIgnoreCase))
            {
                if (family.Contains("serif") && !family.Contains("sans")) scores["luxury"] += 2;
                if (family.Contains("ivar") || family.Contains("playfair") || family.Contains("cormorant")) scores["luxury"] += 2;
            }
            // Geometric sans → modern
            if (family.Contains("inter") || family.Contains("roboto") || family.Contains("montserrat")) scores["modern"] += 1;
            // Rounded/friendly → warm
            if (family.Contains("nunito") || family.Contains("quicksand") || family.Contains("poppins")) scores["warm"] += 1;
            // Traditional → professional
            if (family.Contains("times") || family.Contains("georgia") || family.Contains("garamond")) scores["professional"] += 1;
        }

        // Specialty/keyword signals
        var spec = (specialties ?? "").ToLowerInvariant();
        if (spec.Contains("luxury") || spec.Contains("estate") || spec.Contains("investment")) scores["luxury"] += 3;
        if (spec.Contains("first-time") || spec.Contains("family") || spec.Contains("community")) scores["warm"] += 3;
        if (spec.Contains("commercial") || spec.Contains("corporate")) scores["professional"] += 2;
        if (spec.Contains("new construction") || spec.Contains("development")) scores["modern"] += 2;

        // Profile signals
        foreach (var profile in profiles)
        {
            var bio = (profile.Bio ?? "").ToLowerInvariant();
            if (bio.Contains("luxury") || bio.Contains("high-end") || bio.Contains("million")) scores["luxury"] += 2;
            if (bio.Contains("family") || bio.Contains("neighborhood") || bio.Contains("community")) scores["warm"] += 2;
            if (bio.Contains("years experience") || bio.Contains("veteran") || bio.Contains("trusted")) scores["professional"] += 1;
        }

        var winner = scores.MaxBy(kv => kv.Value);

        // If all scores are 0, default to modern
        if (winner.Value == 0)
            return ("modern", "Default template — insufficient brand signals for recommendation");

        var reason = winner.Key switch
        {
            "luxury" => "Brand signals indicate upscale positioning with premium color palette and editorial typography",
            "modern" => "Clean visual identity with contemporary fonts suggests a tech-forward, minimalist approach",
            "warm" => "Community-focused positioning with warm color tones and approachable typography",
            "professional" => "Traditional brand signals with neutral palette and established market presence",
            _ => "Default template selection"
        };

        return (winner.Key, reason);
    }

    // ── Markdown output ───────────────────────────────────────────────────────

    private static string BuildBrandingKitMarkdown(string agentName, BrandingKit kit)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Branding Kit: {agentName}");
        sb.AppendLine();
        sb.AppendLine("## Colors");
        foreach (var color in kit.Colors)
            sb.AppendLine($"- **{color.Role}**: `{color.Hex}` (from {color.Source}, usage: {color.Usage})");

        sb.AppendLine();
        sb.AppendLine("## Fonts");
        foreach (var font in kit.Fonts)
            sb.AppendLine($"- **{font.Role}**: {font.Family} {font.Weight} (from {font.Source})");

        sb.AppendLine();
        sb.AppendLine("## Logos");
        foreach (var logo in kit.Logos)
            sb.AppendLine($"- **{logo.Variant}**: {logo.FileName} (from {logo.Source})");

        sb.AppendLine();
        sb.AppendLine("## Template Recommendation");
        sb.AppendLine($"- **Template**: {kit.RecommendedTemplate ?? "modern"}");
        sb.AppendLine($"- **Reason**: {kit.TemplateReason ?? "Default"}");

        return sb.ToString();
    }

    // ── Utility helpers ───────────────────────────────────────────────────────

    private static string NormalizeColor(string raw)
    {
        if (raw.StartsWith('#'))
            return raw.ToUpperInvariant();
        return string.Empty; // skip rgb() for simplicity — hex only
    }

    private static bool IsCommonSystemColor(string hex) =>
        hex is "#FFFFFF" or "#000000" or "#FFF" or "#000";

    private static bool IsSystemFont(string family) =>
        family is "sans-serif" or "serif" or "monospace" or "cursive" or "fantasy" or
        "Arial" or "Helvetica" or "Times" or "Verdana" or "Georgia" or "system-ui";
}

public sealed record BrandingDiscoveryResult(BrandingKit Kit, string BrandingKitMarkdown);

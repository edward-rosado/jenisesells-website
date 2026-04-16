namespace RealEstateStar.DataServices.Legal;

/// <summary>
/// Renders legal page Markdown from file-based templates.
/// ZERO Claude calls — pure string substitution on Markdown templates.
/// </summary>
public static class LegalPageRenderer
{
    private const string DisclaimerFooter =
        "\n---\n*This page was generated from professional information on file. " +
        "It is provided for informational purposes only and does not constitute legal advice. " +
        "Please review with legal counsel before relying on this content.*";

    private const string DisclaimerMarker =
        "*This page was generated from professional information on file.";

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Renders a legal page template by substituting all {{variable}} placeholders
    /// and appending the disclaimer footer if not already present.
    /// Missing variables are left as-is ({{variable}}).
    /// </summary>
    /// <param name="templatePath">Absolute path to the .md template file.</param>
    /// <param name="variables">Variable substitutions keyed without braces (e.g. "agent.name").</param>
    /// <returns>Rendered Markdown string.</returns>
    public static string Render(string templatePath, Dictionary<string, string> variables)
    {
        var template = File.ReadAllText(templatePath);
        return RenderTemplate(template, variables);
    }

    /// <summary>
    /// Resolves the template file path using state-override-then-locale-then-English fallback.
    /// Priority:
    ///   1. {templatesRoot}/by-state/{state}/{locale}/{page}.md
    ///   2. {templatesRoot}/_defaults/{locale}/{page}.md
    ///   3. {templatesRoot}/_defaults/en/{page}.md
    /// </summary>
    /// <param name="page">Page name without extension (e.g. "privacy", "fair-housing").</param>
    /// <param name="locale">BCP 47 locale code (e.g. "en", "es").</param>
    /// <param name="state">Two-letter state code (e.g. "NJ"), or null to skip state override.</param>
    /// <param name="templatesRoot">Absolute path to the templates root directory.</param>
    /// <returns>Resolved file path, or null if no template is found.</returns>
    public static string? ResolvePath(string page, string locale, string? state, string templatesRoot)
    {
        // 1. State + locale override
        if (!string.IsNullOrWhiteSpace(state))
        {
            var statePath = Path.Combine(templatesRoot, "by-state", state, locale, $"{page}.md");
            if (File.Exists(statePath))
                return statePath;
        }

        // 2. Default locale
        var localePath = Path.Combine(templatesRoot, "_defaults", locale, $"{page}.md");
        if (File.Exists(localePath))
            return localePath;

        // 3. English fallback
        if (locale != "en")
        {
            var englishPath = Path.Combine(templatesRoot, "_defaults", "en", $"{page}.md");
            if (File.Exists(englishPath))
                return englishPath;
        }

        return null;
    }

    // ─── Internal ─────────────────────────────────────────────────────────────

    internal static string RenderTemplate(string template, Dictionary<string, string> variables)
    {
        var result = template;

        foreach (var (key, value) in variables)
        {
            result = result.Replace($"{{{{{key}}}}}", value, StringComparison.Ordinal);
        }

        if (!result.Contains(DisclaimerMarker, StringComparison.Ordinal))
        {
            result = result.TrimEnd() + DisclaimerFooter;
        }

        return result;
    }
}

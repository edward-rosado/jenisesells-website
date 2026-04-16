using System.Text.RegularExpressions;

namespace RealEstateStar.Domain.Shared.Services;

/// <summary>
/// Pure static language detection using character-set heuristics.
/// Classifies text as English ("en"), Spanish ("es"), or Portuguese ("pt").
/// No external dependencies, no DI, no async — suitable for Domain layer.
/// </summary>
public static partial class LanguageDetector
{
    private const int MinTextLength = 20;
    private const int SpanishCharWeight = 3;          // á/é/í/ó/ú/ñ/¿/¡ — original Spanish char weight
    private const int PortugueseExclusiveWeight = 5;  // ã/õ/â/ê/ô/à — exclusive to Portuguese, bonus weight
    private const int StopWordWeight = 2;
    private const int MinTopScore = 10;
    private const int RunnerUpMultiplier = 2;

    /// <summary>
    /// All locales supported by the platform. Workers iterate this list instead of hardcoding "en"/"es".
    /// </summary>
    public static IReadOnlyList<string> SupportedLocales => ["en", "es", "pt"];

    // Spanish char indicators (original set, fully preserved for backward compatibility).
    private static readonly HashSet<char> SpanishIndicators = ['ñ', '¿', '¡', 'á', 'é', 'í', 'ó', 'ú'];

    // Portuguese-exclusive chars: ã/õ (nasal vowels) and â/ê/ô/à never appear in standard Spanish.
    // These are weighted higher than Spanish shared chars to overcome the shared-accent disadvantage.
    private static readonly HashSet<char> PortugueseExclusiveIndicators = ['ã', 'õ', 'â', 'ê', 'ô', 'à'];

    // Shared Romance accent chars present in both Spanish and Portuguese.
    // Counted separately so Portuguese can receive equal credit for á/é/í/ó/ú
    // that also appear in SpanishIndicators — prevents unfair penalisation of PT text.
    private static readonly HashSet<char> SharedRomanceIndicators = ['á', 'é', 'í', 'ó', 'ú'];

    private static readonly HashSet<string> SpanishStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "el", "la", "los", "las", "de", "en", "que", "por", "con", "para", "es", "un", "una"
    };

    private static readonly HashSet<string> EnglishStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "is", "are", "was", "were", "have", "has", "and", "but", "for", "with"
    };

    // Portuguese stop words chosen to maximise discrimination against Spanish.
    // Shared words ("de", "por", "para", "com", "que", "uma", "um") are intentionally excluded
    // so they don't inflate Spanish score on Portuguese text.
    private static readonly HashSet<string> PortugueseStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "do", "da", "dos", "das", "em", "no", "na", "nos", "nas",
        "seu", "sua", "são", "está", "isso", "como", "mais",
        "mas", "muito", "também", "quando", "sobre", "este", "esta", "aqui", "agora",
        "ao", "aos", "às", "foi", "ele", "ela", "eles", "elas", "você", "vocês",
        "nós", "ter", "ser", "tem", "têm", "temos", "sendo", "pode",
        "podem", "havia", "tudo", "nada", "aquilo", "ainda"
    };

    /// <summary>
    /// Detects the locale of the given text using character-set and stop-word heuristics.
    /// Returns a BCP 47 code: "en", "es", or "pt". Defaults to "en" when undetermined.
    /// </summary>
    public static string DetectLocale(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "en";

        var cleaned = StripHtml(text);
        cleaned = NormalizeWhitespace(cleaned);

        if (cleaned.Length < MinTextLength)
            return "en";

        var spanishCharCount = 0;
        var portugueseExclusiveCount = 0;
        var sharedRomanceCount = 0;

        foreach (var ch in cleaned)
        {
            var lower = char.ToLowerInvariant(ch);
            if (SpanishIndicators.Contains(lower))
                spanishCharCount++;
            if (PortugueseExclusiveIndicators.Contains(lower))
                portugueseExclusiveCount++;
            // á/é/í/ó/ú appear in both Spanish and Portuguese — track separately.
            // They are already counted by SpanishIndicators above; track for PT base credit.
            if (SharedRomanceIndicators.Contains(lower))
                sharedRomanceCount++;
        }

        var tokens = TokenizeRegex().Split(cleaned)
            .Where(t => t.Length > 0)
            .ToArray();

        var spanishStopCount = tokens.Count(t => SpanishStopWords.Contains(t));
        var englishStopCount = tokens.Count(t => EnglishStopWords.Contains(t));
        var portugueseStopCount = tokens.Count(t => PortugueseStopWords.Contains(t));

        // Spanish score: all SpanishIndicators (including shared á/é/í/ó/ú) + stop words.
        // Portuguese score: exclusive high-weight chars + shared accent chars at same weight (level playing field)
        //                   + stop words. This neutralises the á/é/ó penalty on genuine PT text.
        var spanishScore = (spanishCharCount * SpanishCharWeight) + (spanishStopCount * StopWordWeight);
        var englishScore = (englishStopCount * StopWordWeight);
        var portugueseScore = (portugueseExclusiveCount * PortugueseExclusiveWeight)
                              + (sharedRomanceCount * SpanishCharWeight)
                              + (portugueseStopCount * StopWordWeight);

        var scores = new (string Locale, int Score)[]
        {
            ("en", englishScore),
            ("es", spanishScore),
            ("pt", portugueseScore)
        };

        var sorted = scores.OrderByDescending(s => s.Score).ToArray();
        var top = sorted[0];
        var runnerUp = sorted[1];

        if (top.Score > MinTopScore && top.Score > RunnerUpMultiplier * runnerUp.Score)
            return top.Locale;

        return "en";
    }

    /// <summary>
    /// Maps a locale code to its display name.
    /// </summary>
    public static string GetLanguageName(string locale) => locale switch
    {
        "es" => "Spanish",
        "pt" => "Portuguese",
        _ => "English"
    };

    private static string StripHtml(string text) => HtmlTagRegex().Replace(text, string.Empty);

    private static string NormalizeWhitespace(string text) => WhitespaceRegex().Replace(text.Trim(), " ");

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[\s\p{P}]+")]
    private static partial Regex TokenizeRegex();
}

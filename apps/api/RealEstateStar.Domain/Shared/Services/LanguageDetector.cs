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
    private const int IndicatorWeight = 3;
    private const int StopWordWeight = 2;
    private const int MinTopScore = 10;
    private const int RunnerUpMultiplier = 2;

    private static readonly HashSet<char> SpanishIndicators = ['ñ', '¿', '¡', 'á', 'é', 'í', 'ó', 'ú'];
    private static readonly HashSet<char> PortugueseIndicators = ['ã', 'õ', 'ç', 'ê', 'â'];

    private static readonly HashSet<string> SpanishStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "el", "la", "los", "las", "de", "en", "que", "por", "con", "para", "es", "un", "una"
    };

    private static readonly HashSet<string> PortugueseStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "o", "a", "os", "as", "do", "da", "em", "que", "por", "com", "para", "um", "uma"
    };

    private static readonly HashSet<string> EnglishStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "is", "are", "was", "were", "have", "has", "and", "but", "for", "with"
    };

    /// <summary>
    /// Detects the locale of the given text using character-set and stop-word heuristics.
    /// Returns "en", "es", or "pt".
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
        var portugueseCharCount = 0;

        foreach (var ch in cleaned)
        {
            var lower = char.ToLowerInvariant(ch);
            if (SpanishIndicators.Contains(lower))
                spanishCharCount++;
            if (PortugueseIndicators.Contains(lower))
                portugueseCharCount++;
        }

        var tokens = TokenizeRegex().Split(cleaned)
            .Where(t => t.Length > 0)
            .ToArray();

        var spanishStopCount = tokens.Count(t => SpanishStopWords.Contains(t));
        var portugueseStopCount = tokens.Count(t => PortugueseStopWords.Contains(t));
        var englishStopCount = tokens.Count(t => EnglishStopWords.Contains(t));

        var spanishScore = (spanishCharCount * IndicatorWeight) + (spanishStopCount * StopWordWeight);
        var portugueseScore = (portugueseCharCount * IndicatorWeight) + (portugueseStopCount * StopWordWeight);
        var englishScore = (englishStopCount * StopWordWeight);

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

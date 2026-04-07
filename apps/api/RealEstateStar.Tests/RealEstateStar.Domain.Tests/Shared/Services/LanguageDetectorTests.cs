using FluentAssertions;
using RealEstateStar.Domain.Shared.Services;

namespace RealEstateStar.Domain.Tests.Shared.Services;

public class LanguageDetectorTests
{
    [Theory]
    [InlineData(null, "en")]
    [InlineData("", "en")]
    [InlineData("   ", "en")]
    [InlineData("Hi", "en")]
    [InlineData("José", "en")]
    public void DetectLocale_returns_en_for_null_empty_whitespace_and_short_text(string? text, string expected)
    {
        LanguageDetector.DetectLocale(text).Should().Be(expected);
    }

    [Fact]
    public void DetectLocale_returns_en_for_english_sentence()
    {
        var text = "Hi, I'd love to schedule a showing for 123 Main St this week";
        LanguageDetector.DetectLocale(text).Should().Be("en");
    }

    [Fact]
    public void DetectLocale_returns_es_for_spanish_sentence()
    {
        var text = "Hola, me gustaría programar una visita para la propiedad en 123 Calle Principal";
        LanguageDetector.DetectLocale(text).Should().Be("es");
    }

    [Fact]
    public void DetectLocale_returns_es_for_inverted_punctuation()
    {
        var text = "¿Le gustaría ver esta hermosa casa? Tiene tres habitaciones y un jardín grande";
        LanguageDetector.DetectLocale(text).Should().Be("es");
    }

    [Fact]
    public void DetectLocale_returns_en_when_spanish_score_equals_threshold()
    {
        // Spanish stop words score exactly MinTopScore (10) but threshold requires > 10, so defaults to "en"
        var text = "El precio de la propiedad es muy bueno para esta zona del mercado";
        LanguageDetector.DetectLocale(text).Should().Be("en");
    }

    [Fact]
    public void DetectLocale_returns_es_for_spanish_stop_words_above_threshold()
    {
        // Longer text with more Spanish stop words + accented chars to exceed threshold
        var text = "El precio de la propiedad es muy bueno para esta zona del mercado y además la ubicación es excelente";
        LanguageDetector.DetectLocale(text).Should().Be("es");
    }

    [Fact]
    public void DetectLocale_returns_en_for_english_dominant_with_accented_name()
    {
        var text = "The property at Río Grande has 3 beds and 2 baths";
        LanguageDetector.DetectLocale(text).Should().Be("en");
    }

    [Fact]
    public void DetectLocale_strips_html_and_detects_spanish()
    {
        // After HTML stripping, needs enough Spanish indicators + stop words to exceed threshold
        var text = "<p>Hola, me gustaría programar una visita para la <strong>propiedad hermosa</strong> en la zona</p>";
        LanguageDetector.DetectLocale(text).Should().Be("es");
    }

    [Theory]
    [InlineData("es", "Spanish")]
    [InlineData("en", "English")]
    [InlineData("xx", "English")]
    public void GetLanguageName_maps_locale_to_display_name(string locale, string expected)
    {
        LanguageDetector.GetLanguageName(locale).Should().Be(expected);
    }
}

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
        // ¿ and ñ are Spanish-exclusive indicators; la/es/un are ES stop words
        var text = "¿Le gustaría ver esta hermosa casa? Tiene tres habitaciones y un jardín grande con piscina la cual es muy buena";
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
    [InlineData("pt", "Portuguese")]
    [InlineData("en", "English")]
    [InlineData("xx", "English")]
    public void GetLanguageName_maps_locale_to_display_name(string locale, string expected)
    {
        LanguageDetector.GetLanguageName(locale).Should().Be(expected);
    }

    // --- Portuguese detection ---

    [Fact]
    public void DetectLocale_returns_pt_for_portuguese_paragraph()
    {
        // Contains ã (informações, localização, também), õ (não), â (também) — exclusive PT chars
        // and PT-exclusive stop words: são, está, também, quando, mais, nos, da, do, das, este
        var text = "As informações sobre o imóvel são muito importantes para os compradores. " +
                   "A localização está em uma área tranquila. Também é possível agendar " +
                   "uma visita quando quiser. Não perca esta oportunidade incrível.";
        LanguageDetector.DetectLocale(text).Should().Be("pt");
    }

    [Fact]
    public void DetectLocale_returns_pt_for_portuguese_with_exclusive_chars()
    {
        // ã and õ are exclusive Portuguese indicators that strongly differentiate from Spanish
        var text = "As condições do apartamento são excelentes. A localização também está " +
                   "muito boa nos arredores da cidade. Você pode agendar uma visita quando " +
                   "quiser. Nós temos mais opções disponíveis para você escolher agora.";
        LanguageDetector.DetectLocale(text).Should().Be("pt");
    }

    [Fact]
    public void DetectLocale_returns_pt_when_portuguese_dominates_spanish()
    {
        // Portuguese text with multiple exclusive chars (ã, ê, õ) and PT-exclusive stop words.
        // "você", "também", "são", "está", "nos", "do" all score for PT; ã/ê/õ give strong char signal.
        var text = "Você está interessado nas condições do financiamento? As informações são " +
                   "muito importantes. Também verificamos nos documentos que temos disponíveis " +
                   "agora. Nossa equipe está aqui para ajudá-lo quando quiser agendar uma visita.";
        LanguageDetector.DetectLocale(text).Should().Be("pt");
    }

    [Fact]
    public void DetectLocale_returns_es_when_spanish_dominates_similar_portuguese()
    {
        // Spanish text with ñ and inverted punctuation clearly wins over Portuguese
        var text = "¡Hola! Estoy buscando una propiedad con jardín y piscina en esta zona. " +
                   "El precio de la casa es muy bueno para la ubicación. " +
                   "¿Podría programar una visita para ver el apartamento con tres habitaciones?";
        LanguageDetector.DetectLocale(text).Should().Be("es");
    }

    // --- Short text still defaults to "en" ---

    [Theory]
    [InlineData("Olá mundo")]
    [InlineData("Bom dia")]
    [InlineData("Muito obrigado")]
    public void DetectLocale_returns_en_for_short_portuguese_text(string text)
    {
        // Under MinTextLength (20 chars) always returns "en"
        LanguageDetector.DetectLocale(text).Should().Be("en");
    }

    // --- SupportedLocales registry ---

    [Fact]
    public void SupportedLocales_contains_exactly_en_es_pt()
    {
        LanguageDetector.SupportedLocales.Should().BeEquivalentTo(["en", "es", "pt"]);
    }

    [Fact]
    public void SupportedLocales_contains_three_locales()
    {
        LanguageDetector.SupportedLocales.Should().HaveCount(3);
    }

    [Fact]
    public void SupportedLocales_is_in_expected_order()
    {
        LanguageDetector.SupportedLocales.Should().ContainInOrder("en", "es", "pt");
    }
}

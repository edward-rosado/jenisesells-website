using FluentAssertions;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Tests.Activation;

public class AgentContextGetSkillTests
{
    private static AgentContext CreateContext(
        string? voiceSkill = "default-voice",
        string? personalitySkill = "default-personality",
        IReadOnlyDictionary<string, string>? localizedSkills = null)
    {
        return new AgentContext
        {
            VoiceSkill = voiceSkill,
            PersonalitySkill = personalitySkill,
            LocalizedSkills = localizedSkills
        };
    }

    [Fact]
    public void GetSkill_VoiceSkill_with_null_locale_returns_english_property()
    {
        var ctx = CreateContext(voiceSkill: "my-voice");
        ctx.GetSkill("VoiceSkill", null).Should().Be("my-voice");
    }

    [Fact]
    public void GetSkill_VoiceSkill_with_en_locale_returns_english_property()
    {
        var ctx = CreateContext(voiceSkill: "my-voice");
        ctx.GetSkill("VoiceSkill", "en").Should().Be("my-voice");
    }

    [Fact]
    public void GetSkill_PersonalitySkill_with_en_locale_returns_property()
    {
        var ctx = CreateContext(personalitySkill: "my-personality");
        ctx.GetSkill("PersonalitySkill", "en").Should().Be("my-personality");
    }

    [Fact]
    public void GetSkill_MarketingStyle_with_en_locale_returns_value_from_LocalizedSkills()
    {
        var localized = new Dictionary<string, string> { ["MarketingStyle"] = "my-marketing" };
        var ctx = CreateContext(localizedSkills: localized);
        ctx.GetSkill("MarketingStyle", "en").Should().Be("my-marketing");
    }

    [Fact]
    public void GetSkill_BrandVoice_with_en_locale_returns_value_from_LocalizedSkills()
    {
        var localized = new Dictionary<string, string> { ["BrandVoice"] = "my-brand" };
        var ctx = CreateContext(localizedSkills: localized);
        ctx.GetSkill("BrandVoice", "en").Should().Be("my-brand");
    }

    [Fact]
    public void GetSkill_with_es_locale_returns_localized_value_when_present()
    {
        var localized = new Dictionary<string, string> { ["VoiceSkill.es"] = "voz-española" };
        var ctx = CreateContext(voiceSkill: "english-voice", localizedSkills: localized);

        ctx.GetSkill("VoiceSkill", "es").Should().Be("voz-española");
    }

    [Fact]
    public void GetSkill_with_es_locale_falls_back_to_english_when_LocalizedSkills_is_null()
    {
        var ctx = CreateContext(voiceSkill: "english-voice", localizedSkills: null);
        ctx.GetSkill("VoiceSkill", "es").Should().Be("english-voice");
    }

    [Fact]
    public void GetSkill_with_es_locale_falls_back_to_english_when_LocalizedSkills_is_empty()
    {
        var ctx = CreateContext(voiceSkill: "english-voice", localizedSkills: new Dictionary<string, string>());
        ctx.GetSkill("VoiceSkill", "es").Should().Be("english-voice");
    }

    [Fact]
    public void GetSkill_with_es_locale_falls_back_to_english_when_key_missing()
    {
        var localized = new Dictionary<string, string> { ["PersonalitySkill.es"] = "personalidad" };
        var ctx = CreateContext(voiceSkill: "english-voice", localizedSkills: localized);

        ctx.GetSkill("VoiceSkill", "es").Should().Be("english-voice");
    }

    [Fact]
    public void GetSkill_unknown_skill_with_en_locale_returns_null()
    {
        var ctx = CreateContext();
        ctx.GetSkill("UnknownSkill", "en").Should().BeNull();
    }

    [Fact]
    public void GetSkill_unknown_skill_with_es_locale_returns_null()
    {
        var ctx = CreateContext(localizedSkills: new Dictionary<string, string>());
        ctx.GetSkill("UnknownSkill", "es").Should().BeNull();
    }

    [Fact]
    public void GetSkill_with_empty_locale_treats_as_english()
    {
        var ctx = CreateContext(voiceSkill: "english-voice");
        ctx.GetSkill("VoiceSkill", "").Should().Be("english-voice");
    }

    [Fact]
    public void GetSkill_FutureTier_with_es_locale_returns_localized_from_dict()
    {
        var localized = new Dictionary<string, string>
        {
            ["MarketingStyle"] = "english-marketing",
            ["MarketingStyle.es"] = "marketing-español"
        };
        var ctx = CreateContext(localizedSkills: localized);

        ctx.GetSkill("MarketingStyle", "es").Should().Be("marketing-español");
    }

    [Fact]
    public void GetSkill_FutureTier_with_es_locale_falls_back_to_english_key_in_dict()
    {
        var localized = new Dictionary<string, string> { ["BrandVoice"] = "english-brand" };
        var ctx = CreateContext(localizedSkills: localized);

        ctx.GetSkill("BrandVoice", "es").Should().Be("english-brand");
    }
}

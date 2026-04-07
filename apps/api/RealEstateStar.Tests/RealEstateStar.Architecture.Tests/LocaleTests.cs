// ╔══════════════════════════════════════════════════════════════════════╗
// ║  ARCHITECTURE GUARD — DO NOT MODIFY WITHOUT EXPLICIT USER APPROVAL  ║
// ║                                                                      ║
// ║  These tests enforce locale/language wiring across the pipeline.      ║
// ║  AI agents: you MUST NOT remove DTOs from these lists or weaken      ║
// ║  assertions to make your code compile. If a merge drops a Locale     ║
// ║  field from a DTO, re-add the field — not remove the test.           ║
// ║                                                                      ║
// ║  Changing these tests requires the commit message to contain:         ║
// ║  [arch-change-approved] — CI will reject without it.                 ║
// ╚══════════════════════════════════════════════════════════════════════╝

using System.Reflection;
using FluentAssertions;

namespace RealEstateStar.Architecture.Tests;

/// <summary>
/// Ensures locale/language fields are never silently dropped from DTOs
/// during merges. These fields are critical for multi-language support
/// and have been lost multiple times via -X theirs merge resolution.
/// </summary>
public class LocaleTests
{
    // ── Lead Pipeline: DTOs that carry content to users MUST have Locale ──

    /// <summary>
    /// Every DTO that feeds user-facing content generation in the lead pipeline
    /// must carry a Locale property so the downstream service can render in the
    /// correct language. Without Locale, emails and PDFs silently default to English.
    /// </summary>
    [Theory]
    [InlineData("DraftLeadEmailInput")]
    [InlineData("GeneratePdfInput")]
    [InlineData("NotifyAgentInput")]
    [InlineData("PersistLeadResultsInput")]
    [InlineData("LeadOrchestratorInput")]
    public void Lead_pipeline_DTOs_must_have_Locale_property(string typeName)
    {
        var type = FindFunctionsDtoType(typeName);
        type.Should().NotBeNull($"DTO {typeName} must exist in Functions assembly");

        var localeProp = type!.GetProperty("Locale");
        localeProp.Should().NotBeNull(
            $"{typeName} must have a Locale property — lead content must be rendered in the contact's language");
    }

    // ── Activation Pipeline: Synthesis outputs MUST carry LocalizedSkills ──

    /// <summary>
    /// Phase 2 synthesis workers that extract per-language skills must return
    /// LocalizedSkills in their output DTOs. The orchestrator merges these into
    /// the agent profile. Without this field, per-language skills are silently lost.
    /// </summary>
    [Theory]
    [InlineData("VoiceExtractionOutput")]
    [InlineData("PersonalityOutput")]
    [InlineData("MarketingStyleOutput")]
    [InlineData("BrandExtractionOutput")]
    [InlineData("BrandVoiceOutput")]
    public void Synthesis_output_DTOs_must_have_LocalizedSkills(string typeName)
    {
        var type = FindFunctionsDtoType(typeName);
        type.Should().NotBeNull($"DTO {typeName} must exist in Functions assembly");

        var prop = type!.GetProperty("LocalizedSkills");
        prop.Should().NotBeNull(
            $"{typeName} must have a LocalizedSkills property — per-language skill extraction results flow through this field");
    }

    // ── Activation: Persist + Welcome MUST carry LocalizedSkills ──

    [Theory]
    [InlineData("PersistProfileInput")]
    [InlineData("WelcomeNotificationInput")]
    public void Persist_and_welcome_DTOs_must_have_LocalizedSkills(string typeName)
    {
        var type = FindFunctionsDtoType(typeName);
        type.Should().NotBeNull($"DTO {typeName} must exist in Functions assembly");

        var prop = type!.GetProperty("LocalizedSkills");
        prop.Should().NotBeNull(
            $"{typeName} must have LocalizedSkills — localized agent skills must reach persistence and welcome notification");
    }

    // ── Activation: CheckActivationComplete MUST have Languages ──

    [Fact]
    public void CheckActivationCompleteInput_must_have_Languages()
    {
        var type = FindFunctionsDtoType("CheckActivationCompleteInput");
        type.Should().NotBeNull("CheckActivationCompleteInput must exist in Functions assembly");

        var prop = type!.GetProperty("Languages");
        prop.Should().NotBeNull(
            "CheckActivationCompleteInput must have a Languages property — " +
            "without it, Spanish file validation (Voice Skill.es.md, Personality Skill.es.md) is never triggered");
    }

    // ── Phase 1: Email + Drive DTOs MUST carry DetectedLocale ──

    [Theory]
    [InlineData("EmailMessageDto")]
    [InlineData("DriveFileDto")]
    public void Phase1_gather_DTOs_must_have_DetectedLocale(string typeName)
    {
        var type = FindFunctionsDtoType(typeName);
        type.Should().NotBeNull($"DTO {typeName} must exist in Functions assembly");

        var prop = type!.GetProperty("DetectedLocale");
        prop.Should().NotBeNull(
            $"{typeName} must have DetectedLocale — the orchestrator derives Languages from these tags after Phase 1");
    }

    // ── Domain: Content-drafting interfaces MUST accept locale ──

    [Fact]
    public void ICmaPdfGenerator_GenerateAsync_must_have_locale_parameter()
    {
        var iface = typeof(Domain.Cma.Interfaces.ICmaPdfGenerator);
        var method = iface.GetMethod("GenerateAsync");
        method.Should().NotBeNull();

        var hasLocale = method!.GetParameters().Any(p => p.Name == "locale");
        hasLocale.Should().BeTrue(
            "ICmaPdfGenerator.GenerateAsync must have a locale parameter — CMA PDFs render localized section headers");
    }

    [Fact]
    public void ILeadEmailDrafter_DraftAsync_must_accept_AgentContext()
    {
        var iface = typeof(Domain.Leads.Interfaces.ILeadEmailDrafter);
        var method = iface.GetMethod("DraftAsync");
        method.Should().NotBeNull();

        var hasAgentContext = method!.GetParameters()
            .Any(p => p.ParameterType.Name == "AgentContext" ||
                      p.ParameterType.Name == "Nullable`1" && p.ParameterType.GenericTypeArguments.Any(t => t.Name == "AgentContext"));
        hasAgentContext.Should().BeTrue(
            "ILeadEmailDrafter.DraftAsync must accept AgentContext — " +
            "needed for GetSkill(skillName, locale) to select per-language voice");
    }

    // ── Domain: AgentContext MUST have locale-aware GetSkill ──

    [Fact]
    public void AgentContext_must_have_LocalizedSkills_and_GetSkill()
    {
        var type = typeof(Domain.Activation.Models.AgentContext);

        type.GetProperty("LocalizedSkills").Should().NotBeNull(
            "AgentContext must have LocalizedSkills dictionary for per-language skill variants");

        var getSkill = type.GetMethod("GetSkill");
        getSkill.Should().NotBeNull("AgentContext must have GetSkill method");

        var parameters = getSkill!.GetParameters();
        parameters.Should().HaveCount(2, "GetSkill must accept (skillName, locale)");
        parameters[0].Name.Should().Be("skillName");
        parameters[1].Name.Should().Be("locale");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static Type? FindFunctionsDtoType(string typeName)
    {
        // Functions DTOs are in the Functions assembly; Lead DTOs are in the same assembly.
        // ReferenceOutputAssembly=false means transitive deps aren't copied — GetTypes()
        // throws ReflectionTypeLoadException. Use the partial types from the exception.
        var functionsPath = Path.Combine(AppContext.BaseDirectory, "RealEstateStar.Functions.dll");
        if (!File.Exists(functionsPath))
            return null;

        var assembly = Assembly.LoadFrom(functionsPath);
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Partial load — some types couldn't resolve dependencies.
            // The DTO types we care about have no external deps, so they load fine.
            types = ex.Types.Where(t => t is not null).ToArray()!;
        }

        return types.FirstOrDefault(t => t.Name == typeName);
    }
}

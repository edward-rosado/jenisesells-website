using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Activation.Interfaces;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Functions.Activation;
using RealEstateStar.Functions.Activation.Activities;
using RealEstateStar.Functions.Activation.Dtos;

namespace RealEstateStar.Functions.Tests.Activation;

/// <summary>
/// Tests for the activity function wrappers.
/// AgentProfilePersistActivity and BrandMergeActivity are sealed, so we construct real
/// instances with mocked underlying services (IFileStorageProviderFactory, IAgentConfigService,
/// IBrandMergeService, IFileStorageProvider) and verify delegation via those interfaces.
/// For the mapper layer, see ActivationDtoMapperTests.
/// </summary>
public sealed class ActivityFunctionTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    // ── PersistProfileFunction ────────────────────────────────────────────────

    [Fact]
    public async Task PersistProfileFunction_DelegatesToActivity_WithCorrectArgs()
    {
        // Arrange — real AgentProfilePersistActivity with mocked services
        var storageFactory = new Mock<IFileStorageProviderFactory>(MockBehavior.Loose);
        var storage = new Mock<IFileStorageProvider>(MockBehavior.Loose);
        var agentConfigService = new Mock<IAgentConfigService>(MockBehavior.Loose);

        storageFactory.Setup(f => f.CreateForAgent(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(storage.Object);
        storage.Setup(s => s.EnsureFolderExistsAsync(It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        // Track that GenerateAsync is called with the correct IDs
        agentConfigService
            .Setup(s => s.GenerateAsync("acc1", "agent1", "agent1",
                It.IsAny<ActivationOutputs>(), Ct))
            .Returns(Task.CompletedTask);

        var realActivity = new RealEstateStar.Activities.Activation.PersistAgentProfile.AgentProfilePersistActivity(
            storageFactory.Object,
            agentConfigService.Object,
            NullLogger<RealEstateStar.Activities.Activation.PersistAgentProfile.AgentProfilePersistActivity>.Instance);

        var discoveryOutput = new AgentDiscoveryOutput();
        var input = new PersistProfileInput
        {
            AccountId = "acc1",
            AgentId = "agent1",
            Handle = "agent1",
            DriveIndexMarkdown = "# Drive Index",
            DiscoveryMarkdown = "# Discovery",
            AgentEmail = "jane@x.com",
            Discovery = discoveryOutput,
        };

        var fn = new PersistProfileFunction(realActivity, NullLogger<PersistProfileFunction>.Instance);

        // Act
        await fn.RunAsync(input, Ct);

        // Assert — IAgentConfigService.GenerateAsync was called with correct IDs
        agentConfigService.Verify(s => s.GenerateAsync("acc1", "agent1", "agent1",
            It.IsAny<ActivationOutputs>(), Ct), Times.Once);
    }

    [Fact]
    public async Task PersistProfileFunction_MapsVoiceAndPersonalityToOutputs()
    {
        // Arrange — capture the ActivationOutputs passed to IAgentConfigService.GenerateAsync
        ActivationOutputs? capturedOutputs = null;

        var storageFactory = new Mock<IFileStorageProviderFactory>(MockBehavior.Loose);
        var storage = new Mock<IFileStorageProvider>(MockBehavior.Loose);
        var agentConfigService = new Mock<IAgentConfigService>(MockBehavior.Loose);

        storageFactory.Setup(f => f.CreateForAgent(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(storage.Object);
        storage.Setup(s => s.EnsureFolderExistsAsync(It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        agentConfigService
            .Setup(s => s.GenerateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<ActivationOutputs>(), Ct))
            .Callback<string, string, string, ActivationOutputs, CancellationToken>(
                (_, _, _, o, _) => capturedOutputs = o)
            .Returns(Task.CompletedTask);

        var realActivity = new RealEstateStar.Activities.Activation.PersistAgentProfile.AgentProfilePersistActivity(
            storageFactory.Object,
            agentConfigService.Object,
            NullLogger<RealEstateStar.Activities.Activation.PersistAgentProfile.AgentProfilePersistActivity>.Instance);

        var discoveryOutput = new AgentDiscoveryOutput();
        var input = new PersistProfileInput
        {
            AccountId = "acc1",
            AgentId = "agent1",
            Handle = "agent1",
            Voice = new VoiceExtractionOutput { VoiceSkillMarkdown = "# Voice content" },
            Personality = new PersonalityOutput { PersonalitySkillMarkdown = "# Personality content" },
            CmaStyle = "CMA guide",
            DriveIndexMarkdown = "# Drive",
            DiscoveryMarkdown = "# Discovery",
            AgentName = "Jane Smith",
            AgentEmail = "jane@x.com",
            Discovery = discoveryOutput,
        };

        var fn = new PersistProfileFunction(realActivity, NullLogger<PersistProfileFunction>.Instance);

        // Act
        await fn.RunAsync(input, Ct);

        // Assert — mapper correctly transferred voice/personality/cma to ActivationOutputs
        capturedOutputs.Should().NotBeNull();
        capturedOutputs!.VoiceSkill.Should().Be("# Voice content");
        capturedOutputs.PersonalitySkill.Should().Be("# Personality content");
        capturedOutputs.CmaStyleGuide.Should().Be("CMA guide");
        capturedOutputs.AgentName.Should().Be("Jane Smith");
    }

    // ── BrandMergeFunction ────────────────────────────────────────────────────

    [Fact]
    public async Task BrandMergeFunction_DelegatesToActivity_WithCorrectArgs()
    {
        // Arrange — real BrandMergeActivity with mocked services
        var brandMergeService = new Mock<IBrandMergeService>(MockBehavior.Strict);
        var storage = new Mock<IFileStorageProvider>(MockBehavior.Loose);

        var mergeResult = new BrandMergeResult("# Brand Profile", "# Brand Voice");

        brandMergeService
            .Setup(s => s.MergeAsync("acc1", "agent1", "branding kit content", "voice skill content", Ct, null))
            .ReturnsAsync(mergeResult);

        storage.Setup(s => s.EnsureFolderExistsAsync(It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        var realActivity = new RealEstateStar.Activities.Activation.BrandMerge.BrandMergeActivity(
            brandMergeService.Object,
            storage.Object,
            NullLogger<RealEstateStar.Activities.Activation.BrandMerge.BrandMergeActivity>.Instance);

        var fn = new BrandMergeFunction(realActivity, NullLogger<BrandMergeFunction>.Instance);

        // Act
        await fn.RunAsync(
            new BrandMergeInput { AccountId = "acc1", AgentId = "agent1", BrandingKit = "branding kit content", VoiceSkill = "voice skill content" }, Ct);

        // Assert — IBrandMergeService was called with the exact arguments
        brandMergeService.VerifyAll();
    }

    // ── WelcomeNotificationFunction ───────────────────────────────────────────

    [Fact]
    public async Task WelcomeNotificationFunction_DelegatesToService_WithCorrectArgs()
    {
        var service = new Mock<IWelcomeNotificationService>(MockBehavior.Strict);

        service.Setup(s => s.SendAsync(
            "acc1", "agent1", "agent1",
            It.Is<ActivationOutputs>(o => o.AgentName == "Jane Smith"),
            Ct))
            .Returns(Task.CompletedTask);

        var fn = new WelcomeNotificationFunction(service.Object, NullLogger<WelcomeNotificationFunction>.Instance);
        await fn.RunAsync(
            new WelcomeNotificationInput { AccountId = "acc1", AgentId = "agent1", Handle = "agent1", AgentName = "Jane Smith", AgentPhone = "555-1234", WhatsAppEnabled = true }, Ct);

        service.VerifyAll();
    }

    [Fact]
    public async Task WelcomeNotificationFunction_NullAgentName_StillCallsService()
    {
        var service = new Mock<IWelcomeNotificationService>(MockBehavior.Strict);

        service.Setup(s => s.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.Is<ActivationOutputs>(o => o.AgentName == null),
            Ct))
            .Returns(Task.CompletedTask);

        var fn = new WelcomeNotificationFunction(service.Object, NullLogger<WelcomeNotificationFunction>.Instance);
        await fn.RunAsync(
            new WelcomeNotificationInput { AccountId = "acc1", AgentId = "agent1", Handle = "agent1" }, Ct);

        service.VerifyAll();
    }

    // ── CheckActivationCompleteFunction ──────────────────────────────────────

    [Fact]
    public async Task CheckActivationCompleteFunction_AllFilesPresent_ReturnsTrue()
    {
        var storage = new Mock<IDocumentStorageProvider>(MockBehavior.Strict);

        // All required files exist
        storage.Setup(s => s.ReadDocumentAsync(
                It.IsAny<string>(), It.IsAny<string>(), Ct))
            .ReturnsAsync("content");

        var fn = new CheckActivationCompleteFunction(
            storage.Object, NullLogger<CheckActivationCompleteFunction>.Instance);

        var json = await fn.RunAsync(
            new CheckActivationCompleteInput { AccountId = "acc1", AgentId = "agent1" }, Ct);
        var result = JsonSerializer.Deserialize<CheckActivationCompleteOutput>(json)!;

        result.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task CheckActivationCompleteFunction_SomeFilesMissing_ReturnsFalse()
    {
        var storage = new Mock<IDocumentStorageProvider>(MockBehavior.Loose);

        // Most files exist
        storage.Setup(s => s.ReadDocumentAsync(
                It.IsAny<string>(), It.IsAny<string>(), Ct))
            .ReturnsAsync("content");

        // "headshot.jpg" is missing
        storage.Setup(s => s.ReadDocumentAsync(
                It.IsAny<string>(), "headshot.jpg", Ct))
            .ReturnsAsync((string?)null);

        var fn = new CheckActivationCompleteFunction(
            storage.Object, NullLogger<CheckActivationCompleteFunction>.Instance);

        var json = await fn.RunAsync(
            new CheckActivationCompleteInput { AccountId = "acc1", AgentId = "agent1" }, Ct);
        var result = JsonSerializer.Deserialize<CheckActivationCompleteOutput>(json)!;

        result.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task CheckActivationCompleteFunction_NoFilesPresent_ReturnsFalse()
    {
        var storage = new Mock<IDocumentStorageProvider>(MockBehavior.Strict);

        storage.Setup(s => s.ReadDocumentAsync(
                It.IsAny<string>(), It.IsAny<string>(), Ct))
            .ReturnsAsync((string?)null);

        var fn = new CheckActivationCompleteFunction(
            storage.Object, NullLogger<CheckActivationCompleteFunction>.Instance);

        var json = await fn.RunAsync(
            new CheckActivationCompleteInput { AccountId = "acc1", AgentId = "agent1" }, Ct);
        var result = JsonSerializer.Deserialize<CheckActivationCompleteOutput>(json)!;

        result.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void CheckActivationCompleteFunction_RequiredFilesList_ContainsExpectedFiles()
    {
        // All key agent files must be in the check list
        CheckActivationCompleteFunction.RequiredAgentFiles
            .Should().Contain("Voice Skill.md")
            .And.Contain("headshot.jpg")
            .And.Contain("Drive Index.md")
            .And.Contain("Agent Discovery.md");

        // Account files must include brand files
        CheckActivationCompleteFunction.RequiredAccountFiles
            .Should().Contain("Brand Profile.md")
            .And.Contain("Brand Voice.md");
    }

    // ── ActivityNames constant test ───────────────────────────────────────────

    [Fact]
    public void ActivityNames_AllAreDefined_And_Unique()
    {
        var names = new[]
        {
            ActivityNames.CheckActivationComplete,
            ActivityNames.EmailFetch,
            ActivityNames.DriveIndex,
            ActivityNames.AgentDiscovery,
            ActivityNames.VoiceExtraction,
            ActivityNames.Personality,
            ActivityNames.BrandingDiscovery,
            ActivityNames.CmaStyle,
            ActivityNames.MarketingStyle,
            ActivityNames.WebsiteStyle,
            ActivityNames.PipelineAnalysis,
            ActivityNames.Coaching,
            ActivityNames.BrandExtraction,
            ActivityNames.BrandVoice,
            ActivityNames.ComplianceAnalysis,
            ActivityNames.FeeStructure,
            ActivityNames.ContactDetection,
            ActivityNames.PersistProfile,
            ActivityNames.BrandMerge,
            ActivityNames.ContactImport,
            ActivityNames.WelcomeNotification,
        };

        // All names must be unique
        names.Should().OnlyHaveUniqueItems();

        // No name should be null or empty
        names.Should().AllSatisfy(n => n.Should().NotBeNullOrWhiteSpace());

        // All names should start with "Activation" prefix
        names.Should().AllSatisfy(n => n.Should().StartWith("Activation"));
    }
}

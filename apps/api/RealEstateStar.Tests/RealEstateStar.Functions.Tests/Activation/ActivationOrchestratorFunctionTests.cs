using System.Text.Json;
using FluentAssertions;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Functions.Activation;
using RealEstateStar.Functions.Activation.Dtos;

namespace RealEstateStar.Functions.Tests.Activation;

/// <summary>
/// Tests for <see cref="ActivationOrchestratorFunction"/>:
/// - Correct activity call order per phase
/// - Phase 2 workers called in parallel (all 12)
/// - Phase 2.5 contact detection failure is non-fatal
/// - Phase 3 contact import failure is non-fatal
/// - Phase 0 skip-if-complete path skips to welcome notification
/// - Deterministic instance ID generation
///
/// Activities return pre-serialized JSON strings (workaround for DF SDK record.ToString() bug).
/// Mock setups use <c>CallActivityAsync&lt;string&gt;</c> with JSON-serialized DTOs.
/// </summary>
public sealed class ActivationOrchestratorFunctionTests
{
    // ── Instance ID ─────────────���─────────────────────────────────────────────

    [Fact]
    public void InstanceId_Format_IsAccountAgentBased()
    {
        var id = ActivationOrchestratorFunction.InstanceId("acc1", "agent1");
        id.Should().Be("activation-acc1-agent1");
    }

    [Fact]
    public void InstanceId_DifferentAgents_ProduceDifferentIds()
    {
        var id1 = ActivationOrchestratorFunction.InstanceId("acc1", "agent1");
        var id2 = ActivationOrchestratorFunction.InstanceId("acc1", "agent2");
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void InstanceId_DifferentAccounts_ProduceDifferentIds()
    {
        var id1 = ActivationOrchestratorFunction.InstanceId("acc1", "agent1");
        var id2 = ActivationOrchestratorFunction.InstanceId("acc2", "agent1");
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void InstanceId_SameInputs_ProducesSameId_Deterministic()
    {
        var id1 = ActivationOrchestratorFunction.InstanceId("acc1", "agent1");
        var id2 = ActivationOrchestratorFunction.InstanceId("acc1", "agent1");
        id1.Should().Be(id2);
    }

    // ── Orchestrator: happy path ──────────────────��────────────────────────────

    [Fact]
    public async Task Orchestrator_HappyPath_CallsAllPhasesInOrder()
    {
        var callOrder = new List<string>();
        var ctx = new Mock<TaskOrchestrationContext>(MockBehavior.Strict);
        var request = new ActivationRequest("acc1", "agent1", "jane@example.com", DateTime.UtcNow);

        ctx.Setup(c => c.GetInput<ActivationRequest>()).Returns(request);
        ctx.Setup(c => c.IsReplaying).Returns(false);
        ctx.Setup(c => c.CurrentUtcDateTime).Returns(DateTime.UtcNow);
        ctx.Setup(c => c.CreateReplaySafeLogger<ActivationOrchestratorFunction>())
            .Returns(NullLogger<ActivationOrchestratorFunction>.Instance);

        // Phase 0: not complete
        SetupJsonActivity(ctx, ActivityNames.CheckActivationComplete,
            new CheckActivationCompleteOutput(), callOrder);

        // Phase 1
        SetupJsonActivity(ctx, ActivityNames.EmailFetch,
            new EmailFetchOutput(), callOrder);
        SetupJsonActivity(ctx, ActivityNames.DriveIndex,
            new DriveIndexOutput { FolderId = "folder1" }, callOrder);
        SetupJsonActivity(ctx, ActivityNames.AgentDiscovery,
            new AgentDiscoveryOutput(), callOrder);

        // Phase 2: all 12 synthesis workers (each with correct return type)
        SetupAllPhase2Activities(ctx, callOrder);

        // Phase 2.5: contact detection
        SetupJsonActivity(ctx, ActivityNames.ContactDetection,
            new ContactDetectionOutput(), callOrder);

        // Phase 3
        SetupVoidActivity(ctx, ActivityNames.PersistProfile, callOrder);
        SetupVoidActivity(ctx, ActivityNames.BrandMerge, callOrder);

        // Staged content cleanup
        SetupVoidActivity(ctx, ActivityNames.CleanupStagedContent, callOrder);

        // Phase 4
        SetupVoidActivity(ctx, ActivityNames.WelcomeNotification, callOrder);

        // Act
        await ActivationOrchestratorFunction.RunAsync(ctx.Object);

        // Assert: phases must be called in order
        callOrder.Should().ContainInOrder(
            ActivityNames.CheckActivationComplete,
            ActivityNames.EmailFetch,
            // DriveIndex runs in parallel with EmailFetch but both complete before AgentDiscovery
            ActivityNames.AgentDiscovery,
            ActivityNames.ContactDetection,
            ActivityNames.PersistProfile,
            ActivityNames.BrandMerge,
            ActivityNames.WelcomeNotification);
    }

    [Fact]
    public async Task Orchestrator_Phase2_MvpTier_OnlyEightWorkersAreCalled()
    {
        var ctx = BuildMockOrchestratorContext(isComplete: false);

        await ActivationOrchestratorFunction.RunAsync(ctx.Object);

        // MVP tier: 8 core workers called
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.VoiceExtraction, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.Personality, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.BrandingDiscovery, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.CmaStyle, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.WebsiteStyle, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.PipelineAnalysis, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.Coaching, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.ComplianceAnalysis, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Once);

        // FUTURE-tier workers NOT called for MVP
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.MarketingStyle, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Never);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.BrandExtraction, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Never);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.BrandVoice, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Never);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.FeeStructure, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Never);
    }

    [Fact]
    public async Task Orchestrator_Phase2_FutureTier_AllTwelveWorkersAreCalled()
    {
        var ctx = BuildMockOrchestratorContext(isComplete: false, tier: ActivationTier.Future);

        await ActivationOrchestratorFunction.RunAsync(ctx.Object);

        // Future tier: all 12 workers called
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.VoiceExtraction, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.Personality, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.BrandingDiscovery, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.CmaStyle, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.MarketingStyle, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.WebsiteStyle, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.PipelineAnalysis, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.Coaching, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.BrandExtraction, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.BrandVoice, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.ComplianceAnalysis, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.FeeStructure, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()), Times.Once);
    }

    // ── Phase 0: skip-if-complete ───��─────────────────────────────────────────

    [Fact]
    public async Task Orchestrator_AlreadyComplete_SkipsToWelcomeOnly()
    {
        var ctx = new Mock<TaskOrchestrationContext>(MockBehavior.Strict);
        var request = new ActivationRequest("acc1", "agent1", "jane@example.com", DateTime.UtcNow);

        ctx.Setup(c => c.GetInput<ActivationRequest>()).Returns(request);
        ctx.Setup(c => c.IsReplaying).Returns(false);
        ctx.Setup(c => c.CreateReplaySafeLogger<ActivationOrchestratorFunction>())
            .Returns(NullLogger<ActivationOrchestratorFunction>.Instance);

        // Phase 0: already complete
        SetupJsonActivity(ctx, ActivityNames.CheckActivationComplete,
            new CheckActivationCompleteOutput { IsComplete = true }, new List<string>());

        SetupVoidActivity(ctx, ActivityNames.WelcomeNotification, new List<string>());

        await ActivationOrchestratorFunction.RunAsync(ctx.Object);

        // Phase 1, 2, 3 should NOT be called
        ctx.Verify(c => c.CallActivityAsync<string>(
            ActivityNames.EmailFetch, It.IsAny<object>(), It.IsAny<TaskOptions>()),
            Times.Never);

        ctx.Verify(c => c.CallActivityAsync(
            ActivityNames.PersistProfile, It.IsAny<object>(), It.IsAny<TaskOptions>()),
            Times.Never);

        // WelcomeNotification should be called once (for idempotent re-send)
        ctx.Verify(c => c.CallActivityAsync(
            ActivityNames.WelcomeNotification, It.IsAny<WelcomeNotificationInput>(), It.IsAny<TaskOptions>()),
            Times.Once);
    }

    // ── Phase 2.5: contact detection failure is non-fatal ────────────────────

    [Fact]
    public async Task Orchestrator_ContactDetectionFails_PipelineContinues()
    {
        var ctx = BuildMockOrchestratorContext(isComplete: false);

        // Override contact detection to throw
        ctx.Setup(c => c.CallActivityAsync<string>(
                ActivityNames.ContactDetection, It.IsAny<ContactDetectionInput>(), It.IsAny<TaskOptions>()))
            .ThrowsAsync(new InvalidOperationException("Contact detection failed"));

        // Act — should not throw
        await ActivationOrchestratorFunction.RunAsync(ctx.Object);

        // Phase 3 and 4 should still be called
        ctx.Verify(c => c.CallActivityAsync(
            ActivityNames.PersistProfile, It.IsAny<PersistProfileInput>(), It.IsAny<TaskOptions>()),
            Times.Once);

        ctx.Verify(c => c.CallActivityAsync(
            ActivityNames.WelcomeNotification, It.IsAny<WelcomeNotificationInput>(), It.IsAny<TaskOptions>()),
            Times.Once);
    }

    // ── Phase 3: contact import failure is non-fatal ──────────────────────────

    [Fact]
    public async Task Orchestrator_ContactImportFails_PipelineContinuesToPhase4()
    {
        var ctx = BuildMockOrchestratorContextWithContacts(isComplete: false);

        // Override contact import to throw
        ctx.Setup(c => c.CallActivityAsync(
                ActivityNames.ContactImport, It.IsAny<ContactImportInput>(), It.IsAny<TaskOptions>()))
            .ThrowsAsync(new InvalidOperationException("Contact import failed"));

        // Act — should not throw
        await ActivationOrchestratorFunction.RunAsync(ctx.Object);

        // Phase 4 should still be called
        ctx.Verify(c => c.CallActivityAsync(
            ActivityNames.WelcomeNotification, It.IsAny<WelcomeNotificationInput>(), It.IsAny<TaskOptions>()),
            Times.Once);
    }

    // ── Phase 2: individual worker failure is non-fatal ───────────────────��───

    [Fact]
    public async Task Orchestrator_OnePhase2WorkerFails_OthersContinue()
    {
        var ctx = BuildMockOrchestratorContext(isComplete: false);

        // Make VoiceExtraction throw while all others succeed
        ctx.Setup(c => c.CallActivityAsync<string>(
                ActivityNames.VoiceExtraction, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()))
            .ThrowsAsync(new InvalidOperationException("Claude timeout"));

        // Act — should not throw
        await ActivationOrchestratorFunction.RunAsync(ctx.Object);

        // PersistProfile still called (with null voice)
        ctx.Verify(c => c.CallActivityAsync(
            ActivityNames.PersistProfile, It.IsAny<PersistProfileInput>(), It.IsAny<TaskOptions>()),
            Times.Once);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets up a mock activity that returns a JSON-serialized DTO string,
    /// matching the DF SDK workaround where activities return <c>Task&lt;string&gt;</c>.
    /// All three parameters must be specified explicitly to avoid Moq CS0854 errors
    /// with optional parameters in expression trees.
    /// </summary>
    private static void SetupJsonActivity<T>(
        Mock<TaskOrchestrationContext> ctx, string name, T result, List<string> callOrder)
    {
        var json = JsonSerializer.Serialize(result);
        ctx.Setup(c => c.CallActivityAsync<string>(name, It.IsAny<object>(), It.IsAny<TaskOptions>()))
            .Callback(() => callOrder.Add(name))
            .ReturnsAsync(json);
    }

    private static void SetupVoidActivity(
        Mock<TaskOrchestrationContext> ctx, string name, List<string> callOrder)
    {
        ctx.Setup(c => c.CallActivityAsync(name, It.IsAny<object>(), It.IsAny<TaskOptions>()))
            .Callback(() => callOrder.Add(name))
            .Returns(Task.CompletedTask);
    }

    /// <summary>
    /// Sets up all 12 Phase 2 workers on the mock context.
    /// Each worker returns a JSON-serialized string (matching the DF SDK workaround).
    /// Null outputs are serialized as "null" JSON strings.
    /// </summary>
    private static void SetupAllPhase2Activities(
        Mock<TaskOrchestrationContext> ctx, List<string> callOrder)
    {
        SetupPhase2Activity<VoiceExtractionOutput>(ctx, ActivityNames.VoiceExtraction, callOrder);
        SetupPhase2Activity<PersonalityOutput>(ctx, ActivityNames.Personality, callOrder);
        SetupPhase2Activity<BrandingDiscoveryOutput>(ctx, ActivityNames.BrandingDiscovery, callOrder);
        SetupPhase2Activity<StringOutput>(ctx, ActivityNames.CmaStyle, callOrder);
        SetupPhase2Activity<MarketingStyleOutput>(ctx, ActivityNames.MarketingStyle, callOrder);
        SetupPhase2Activity<StringOutput>(ctx, ActivityNames.WebsiteStyle, callOrder);
        SetupPhase2Activity<PipelineAnalysisOutput>(ctx, ActivityNames.PipelineAnalysis, callOrder);
        SetupPhase2Activity<CoachingOutput>(ctx, ActivityNames.Coaching, callOrder);
        SetupPhase2Activity<StringOutput>(ctx, ActivityNames.BrandExtraction, callOrder);
        SetupPhase2Activity<StringOutput>(ctx, ActivityNames.BrandVoice, callOrder);
        SetupPhase2Activity<StringOutput>(ctx, ActivityNames.ComplianceAnalysis, callOrder);
        SetupPhase2Activity<StringOutput>(ctx, ActivityNames.FeeStructure, callOrder);
    }

    /// <summary>
    /// Helper for Phase 2: sets up a string-returning activity that returns "null" JSON.
    /// The orchestrator's WrapAsync deserializes this to null for the typed DTO.
    /// </summary>
    private static void SetupPhase2Activity<T>(
        Mock<TaskOrchestrationContext> ctx, string activityName, List<string> callOrder)
    {
        ctx.Setup(c => c.CallActivityAsync<string>(
                activityName, It.IsAny<SynthesisInput>(), It.IsAny<TaskOptions>()))
            .Callback(() => callOrder.Add(activityName))
            .ReturnsAsync("null");
    }

    /// <summary>
    /// Builds a fully-mocked orchestration context with all activities returning empty results.
    /// </summary>
    private static Mock<TaskOrchestrationContext> BuildMockOrchestratorContext(
        bool isComplete, ActivationTier tier = ActivationTier.Mvp)
    {
        var ctx = new Mock<TaskOrchestrationContext>(MockBehavior.Loose);
        var request = new ActivationRequest("acc1", "agent1", "jane@example.com", DateTime.UtcNow, tier);
        var callOrder = new List<string>();

        ctx.Setup(c => c.GetInput<ActivationRequest>()).Returns(request);
        ctx.Setup(c => c.IsReplaying).Returns(false);
        ctx.Setup(c => c.CreateReplaySafeLogger<ActivationOrchestratorFunction>())
            .Returns(NullLogger<ActivationOrchestratorFunction>.Instance);

        SetupJsonActivity(ctx, ActivityNames.CheckActivationComplete,
            new CheckActivationCompleteOutput { IsComplete = isComplete }, callOrder);

        if (!isComplete)
        {
            SetupJsonActivity(ctx, ActivityNames.EmailFetch,
                new EmailFetchOutput(), callOrder);
            SetupJsonActivity(ctx, ActivityNames.DriveIndex,
                new DriveIndexOutput { FolderId = "folder1" }, callOrder);
            SetupJsonActivity(ctx, ActivityNames.AgentDiscovery,
                new AgentDiscoveryOutput(), callOrder);

            SetupAllPhase2Activities(ctx, callOrder);

            SetupJsonActivity(ctx, ActivityNames.ContactDetection,
                new ContactDetectionOutput(), callOrder);

            SetupVoidActivity(ctx, ActivityNames.PersistProfile, callOrder);
            SetupVoidActivity(ctx, ActivityNames.BrandMerge, callOrder);
        }

        SetupVoidActivity(ctx, ActivityNames.WelcomeNotification, callOrder);

        return ctx;
    }

    /// <summary>
    /// Like BuildMockOrchestratorContext but with contacts returned from contact detection,
    /// so ContactImport gets called.
    /// </summary>
    private static Mock<TaskOrchestrationContext> BuildMockOrchestratorContextWithContacts(bool isComplete)
    {
        var ctx = BuildMockOrchestratorContext(isComplete);

        // Override ContactDetection to return one contact (triggers ContactImport call)
        var contact = new ImportedContactDto
        {
            Name = "Alice",
            Email = "alice@x.com",
            Role = "Buyer",
            Stage = "Lead",
        };
        var contactOutput = new ContactDetectionOutput { Contacts = [contact] };
        ctx.Setup(c => c.CallActivityAsync<string>(
                ActivityNames.ContactDetection, It.IsAny<ContactDetectionInput>(), It.IsAny<TaskOptions>()))
            .ReturnsAsync(JsonSerializer.Serialize(contactOutput));

        // ContactImport void activity
        ctx.Setup(c => c.CallActivityAsync(
                ActivityNames.ContactImport, It.IsAny<ContactImportInput>(), It.IsAny<TaskOptions>()))
            .Returns(Task.CompletedTask);

        return ctx;
    }
}

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
/// </summary>
public sealed class ActivationOrchestratorFunctionTests
{
    // ── Instance ID ───────────────────────────────────────────────────────────

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

    // ── Orchestrator: happy path ───────────────────────────────────────────────

    [Fact]
    public async Task Orchestrator_HappyPath_CallsAllPhasesInOrder()
    {
        var callOrder = new List<string>();
        var ctx = new Mock<TaskOrchestrationContext>(MockBehavior.Strict);
        var request = new ActivationRequest("acc1", "agent1", "jane@example.com", DateTime.UtcNow);

        ctx.Setup(c => c.GetInput<ActivationRequest>()).Returns(request);
        ctx.Setup(c => c.IsReplaying).Returns(false);
        ctx.Setup(c => c.CreateReplaySafeLogger<ActivationOrchestratorFunction>())
            .Returns(NullLogger<ActivationOrchestratorFunction>.Instance);

        // Phase 0: not complete
        SetupActivity(ctx, ActivityNames.CheckActivationComplete,
            new CheckActivationCompleteOutput(IsComplete: false), callOrder);

        // Phase 1
        SetupActivity(ctx, ActivityNames.EmailFetch,
            new EmailFetchOutput([], [], null), callOrder);
        SetupActivity(ctx, ActivityNames.DriveIndex,
            new DriveIndexOutput("folder1", [], new Dictionary<string, string>(), [], []), callOrder);
        SetupActivity(ctx, ActivityNames.AgentDiscovery,
            new AgentDiscoveryOutput(null, null, null, [], [], [], null, false), callOrder);

        // Phase 2: all 12 synthesis workers (each with correct return type)
        SetupAllPhase2Activities(ctx, callOrder);

        // Phase 2.5: contact detection
        SetupActivity(ctx, ActivityNames.ContactDetection,
            new ContactDetectionOutput([]), callOrder);

        // Phase 3
        SetupVoidActivity(ctx, ActivityNames.PersistProfile, callOrder);
        SetupVoidActivity(ctx, ActivityNames.BrandMerge, callOrder);

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
    public async Task Orchestrator_Phase2_AllTwelveWorkersAreCalled()
    {
        var ctx = BuildMockOrchestratorContext(isComplete: false);

        await ActivationOrchestratorFunction.RunAsync(ctx.Object);

        // Verify each of the 12 Phase 2 workers was called exactly once,
        // using the correct generic return type that matches the production code.
        ctx.Verify(c => c.CallActivityAsync<VoiceExtractionOutput>(
            ActivityNames.VoiceExtraction, It.IsAny<SynthesisInput>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<PersonalityOutput>(
            ActivityNames.Personality, It.IsAny<SynthesisInput>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<BrandingDiscoveryOutput>(
            ActivityNames.BrandingDiscovery, It.IsAny<SynthesisInput>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<StringOutput>(
            ActivityNames.CmaStyle, It.IsAny<SynthesisInput>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<MarketingStyleOutput>(
            ActivityNames.MarketingStyle, It.IsAny<SynthesisInput>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<StringOutput>(
            ActivityNames.WebsiteStyle, It.IsAny<SynthesisInput>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<StringOutput>(
            ActivityNames.PipelineAnalysis, It.IsAny<SynthesisInput>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<CoachingOutput>(
            ActivityNames.Coaching, It.IsAny<SynthesisInput>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<StringOutput>(
            ActivityNames.BrandExtraction, It.IsAny<SynthesisInput>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<StringOutput>(
            ActivityNames.BrandVoice, It.IsAny<SynthesisInput>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<StringOutput>(
            ActivityNames.ComplianceAnalysis, It.IsAny<SynthesisInput>()), Times.Once);
        ctx.Verify(c => c.CallActivityAsync<StringOutput>(
            ActivityNames.FeeStructure, It.IsAny<SynthesisInput>()), Times.Once);
    }

    // ── Phase 0: skip-if-complete ─────────────────────────────────────────────

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
        SetupActivity(ctx, ActivityNames.CheckActivationComplete,
            new CheckActivationCompleteOutput(IsComplete: true), new List<string>());

        SetupVoidActivity(ctx, ActivityNames.WelcomeNotification, new List<string>());

        await ActivationOrchestratorFunction.RunAsync(ctx.Object);

        // Phase 1, 2, 3 should NOT be called
        ctx.Verify(c => c.CallActivityAsync<EmailFetchOutput>(
            ActivityNames.EmailFetch, It.IsAny<object>()),
            Times.Never);

        ctx.Verify(c => c.CallActivityAsync(
            ActivityNames.PersistProfile, It.IsAny<object>()),
            Times.Never);

        // WelcomeNotification should be called once (for idempotent re-send)
        ctx.Verify(c => c.CallActivityAsync(
            ActivityNames.WelcomeNotification, It.IsAny<WelcomeNotificationInput>()),
            Times.Once);
    }

    // ── Phase 2.5: contact detection failure is non-fatal ────────────────────

    [Fact]
    public async Task Orchestrator_ContactDetectionFails_PipelineContinues()
    {
        var ctx = BuildMockOrchestratorContext(isComplete: false);

        // Override contact detection to throw
        ctx.Setup(c => c.CallActivityAsync<ContactDetectionOutput>(
                ActivityNames.ContactDetection, It.IsAny<ContactDetectionInput>()))
            .ThrowsAsync(new InvalidOperationException("Contact detection failed"));

        // Act — should not throw
        await ActivationOrchestratorFunction.RunAsync(ctx.Object);

        // Phase 3 and 4 should still be called
        ctx.Verify(c => c.CallActivityAsync(
            ActivityNames.PersistProfile, It.IsAny<PersistProfileInput>()),
            Times.Once);

        ctx.Verify(c => c.CallActivityAsync(
            ActivityNames.WelcomeNotification, It.IsAny<WelcomeNotificationInput>()),
            Times.Once);
    }

    // ── Phase 3: contact import failure is non-fatal ──────────────────────────

    [Fact]
    public async Task Orchestrator_ContactImportFails_PipelineContinuesToPhase4()
    {
        var ctx = BuildMockOrchestratorContextWithContacts(isComplete: false);

        // Override contact import to throw
        ctx.Setup(c => c.CallActivityAsync(
                ActivityNames.ContactImport, It.IsAny<ContactImportInput>()))
            .ThrowsAsync(new InvalidOperationException("Contact import failed"));

        // Act — should not throw
        await ActivationOrchestratorFunction.RunAsync(ctx.Object);

        // Phase 4 should still be called
        ctx.Verify(c => c.CallActivityAsync(
            ActivityNames.WelcomeNotification, It.IsAny<WelcomeNotificationInput>()),
            Times.Once);
    }

    // ── Phase 2: individual worker failure is non-fatal ───────────────────────

    [Fact]
    public async Task Orchestrator_OnePhase2WorkerFails_OthersContinue()
    {
        var ctx = BuildMockOrchestratorContext(isComplete: false);

        // Make VoiceExtraction throw while all others succeed
        ctx.Setup(c => c.CallActivityAsync<VoiceExtractionOutput>(
                ActivityNames.VoiceExtraction, It.IsAny<SynthesisInput>()))
            .ThrowsAsync(new InvalidOperationException("Claude timeout"));

        // Act — should not throw
        await ActivationOrchestratorFunction.RunAsync(ctx.Object);

        // PersistProfile still called (with null voice)
        ctx.Verify(c => c.CallActivityAsync(
            ActivityNames.PersistProfile, It.IsAny<PersistProfileInput>()),
            Times.Once);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SetupActivity<T>(
        Mock<TaskOrchestrationContext> ctx, string name, T result, List<string> callOrder)
    {
        ctx.Setup(c => c.CallActivityAsync<T>(name, It.IsAny<object>()))
            .Callback(() => callOrder.Add(name))
            .ReturnsAsync(result);
    }

    private static void SetupVoidActivity(
        Mock<TaskOrchestrationContext> ctx, string name, List<string> callOrder)
    {
        ctx.Setup(c => c.CallActivityAsync(name, It.IsAny<object>()))
            .Callback(() => callOrder.Add(name))
            .Returns(Task.CompletedTask);
    }

    /// <summary>
    /// Sets up all 12 Phase 2 workers on the mock context.
    /// Each worker must be set up with its exact generic return type to match the
    /// production code's CallActivityAsync&lt;T&gt; calls.
    /// </summary>
    private static void SetupAllPhase2Activities(
        Mock<TaskOrchestrationContext> ctx, List<string> callOrder)
    {
        ctx.Setup(c => c.CallActivityAsync<VoiceExtractionOutput>(
                ActivityNames.VoiceExtraction, It.IsAny<SynthesisInput>()))
            .Callback(() => callOrder.Add(ActivityNames.VoiceExtraction))
            .ReturnsAsync((VoiceExtractionOutput?)null);

        ctx.Setup(c => c.CallActivityAsync<PersonalityOutput>(
                ActivityNames.Personality, It.IsAny<SynthesisInput>()))
            .Callback(() => callOrder.Add(ActivityNames.Personality))
            .ReturnsAsync((PersonalityOutput?)null);

        ctx.Setup(c => c.CallActivityAsync<BrandingDiscoveryOutput>(
                ActivityNames.BrandingDiscovery, It.IsAny<SynthesisInput>()))
            .Callback(() => callOrder.Add(ActivityNames.BrandingDiscovery))
            .ReturnsAsync((BrandingDiscoveryOutput?)null);

        ctx.Setup(c => c.CallActivityAsync<StringOutput>(
                ActivityNames.CmaStyle, It.IsAny<SynthesisInput>()))
            .Callback(() => callOrder.Add(ActivityNames.CmaStyle))
            .ReturnsAsync((StringOutput?)null);

        ctx.Setup(c => c.CallActivityAsync<MarketingStyleOutput>(
                ActivityNames.MarketingStyle, It.IsAny<SynthesisInput>()))
            .Callback(() => callOrder.Add(ActivityNames.MarketingStyle))
            .ReturnsAsync((MarketingStyleOutput?)null);

        ctx.Setup(c => c.CallActivityAsync<StringOutput>(
                ActivityNames.WebsiteStyle, It.IsAny<SynthesisInput>()))
            .Callback(() => callOrder.Add(ActivityNames.WebsiteStyle))
            .ReturnsAsync((StringOutput?)null);

        ctx.Setup(c => c.CallActivityAsync<StringOutput>(
                ActivityNames.PipelineAnalysis, It.IsAny<SynthesisInput>()))
            .Callback(() => callOrder.Add(ActivityNames.PipelineAnalysis))
            .ReturnsAsync((StringOutput?)null);

        ctx.Setup(c => c.CallActivityAsync<CoachingOutput>(
                ActivityNames.Coaching, It.IsAny<SynthesisInput>()))
            .Callback(() => callOrder.Add(ActivityNames.Coaching))
            .ReturnsAsync((CoachingOutput?)null);

        ctx.Setup(c => c.CallActivityAsync<StringOutput>(
                ActivityNames.BrandExtraction, It.IsAny<SynthesisInput>()))
            .Callback(() => callOrder.Add(ActivityNames.BrandExtraction))
            .ReturnsAsync((StringOutput?)null);

        ctx.Setup(c => c.CallActivityAsync<StringOutput>(
                ActivityNames.BrandVoice, It.IsAny<SynthesisInput>()))
            .Callback(() => callOrder.Add(ActivityNames.BrandVoice))
            .ReturnsAsync((StringOutput?)null);

        ctx.Setup(c => c.CallActivityAsync<StringOutput>(
                ActivityNames.ComplianceAnalysis, It.IsAny<SynthesisInput>()))
            .Callback(() => callOrder.Add(ActivityNames.ComplianceAnalysis))
            .ReturnsAsync((StringOutput?)null);

        ctx.Setup(c => c.CallActivityAsync<StringOutput>(
                ActivityNames.FeeStructure, It.IsAny<SynthesisInput>()))
            .Callback(() => callOrder.Add(ActivityNames.FeeStructure))
            .ReturnsAsync((StringOutput?)null);
    }

    /// <summary>
    /// Builds a fully-mocked orchestration context with all activities returning empty results.
    /// </summary>
    private static Mock<TaskOrchestrationContext> BuildMockOrchestratorContext(bool isComplete)
    {
        var ctx = new Mock<TaskOrchestrationContext>(MockBehavior.Loose);
        var request = new ActivationRequest("acc1", "agent1", "jane@example.com", DateTime.UtcNow);
        var callOrder = new List<string>();

        ctx.Setup(c => c.GetInput<ActivationRequest>()).Returns(request);
        ctx.Setup(c => c.IsReplaying).Returns(false);
        ctx.Setup(c => c.CreateReplaySafeLogger<ActivationOrchestratorFunction>())
            .Returns(NullLogger<ActivationOrchestratorFunction>.Instance);

        SetupActivity(ctx, ActivityNames.CheckActivationComplete,
            new CheckActivationCompleteOutput(IsComplete: isComplete), callOrder);

        if (!isComplete)
        {
            SetupActivity(ctx, ActivityNames.EmailFetch,
                new EmailFetchOutput([], [], null), callOrder);
            SetupActivity(ctx, ActivityNames.DriveIndex,
                new DriveIndexOutput("folder1", [], new Dictionary<string, string>(), [], []), callOrder);
            SetupActivity(ctx, ActivityNames.AgentDiscovery,
                new AgentDiscoveryOutput(null, null, null, [], [], [], null, false), callOrder);

            SetupAllPhase2Activities(ctx, callOrder);

            SetupActivity(ctx, ActivityNames.ContactDetection,
                new ContactDetectionOutput([]), callOrder);

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
        var contact = new ImportedContactDto(
            "Alice", "alice@x.com", null, "Buyer", "Lead", null, []);
        ctx.Setup(c => c.CallActivityAsync<ContactDetectionOutput>(
                ActivityNames.ContactDetection, It.IsAny<ContactDetectionInput>()))
            .ReturnsAsync(new ContactDetectionOutput([contact]));

        // ContactImport void activity
        ctx.Setup(c => c.CallActivityAsync(
                ActivityNames.ContactImport, It.IsAny<ContactImportInput>()))
            .Returns(Task.CompletedTask);

        return ctx;
    }
}

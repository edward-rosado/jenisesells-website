using FluentAssertions;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Functions.Activation;

namespace RealEstateStar.Functions.Tests.Activation;

/// <summary>
/// Tests for <see cref="StartActivationFunction"/>:
/// - Starts orchestration with correct instance ID
/// - Skips duplicate when already Running or Pending (pre-check, not exception-catch)
/// - Propagates unexpected exceptions
/// </summary>
public sealed class StartActivationFunctionTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task StartActivation_SchedulesOrchestration_WithCorrectInstanceId()
    {
        var client = new Mock<DurableTaskClient>(MockBehavior.Loose, "test");
        var request = new ActivationRequest("acc1", "agent1", "jane@example.com",
            new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

        var expectedInstanceId = ActivationOrchestratorFunction.InstanceId("acc1", "agent1");

        // No existing instance
        client.Setup(c => c.GetInstanceAsync(expectedInstanceId, Ct))
            .ReturnsAsync((OrchestrationMetadata?)null);

        client.Setup(c => c.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                request,
                It.Is<StartOrchestrationOptions>(o => o.InstanceId == expectedInstanceId),
                Ct))
            .ReturnsAsync(expectedInstanceId);

        var fn = new StartActivationFunction(NullLogger<StartActivationFunction>.Instance);
        await fn.RunAsync(request, client.Object, Ct);

        client.Verify(c => c.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(),
                request,
                It.Is<StartOrchestrationOptions>(o => o.InstanceId == expectedInstanceId),
                Ct),
            Times.Once);
    }

    [Theory]
    [InlineData(OrchestrationRuntimeStatus.Running)]
    [InlineData(OrchestrationRuntimeStatus.Pending)]
    public async Task StartActivation_AlreadyRunningOrPending_SkipsDuplicate_DoesNotSchedule(OrchestrationRuntimeStatus status)
    {
        var client = new Mock<DurableTaskClient>(MockBehavior.Loose, "test");
        var request = new ActivationRequest("acc1", "agent1", "jane@example.com",
            new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

        var expectedInstanceId = ActivationOrchestratorFunction.InstanceId("acc1", "agent1");

        // Return a metadata object with Running/Pending status — pre-check should skip
        var metadata = new OrchestrationMetadata("ActivationOrchestrator", expectedInstanceId)
        {
            RuntimeStatus = status
        };
        client.Setup(c => c.GetInstanceAsync(expectedInstanceId, Ct))
            .ReturnsAsync(metadata);

        var fn = new StartActivationFunction(NullLogger<StartActivationFunction>.Instance);
        await fn.RunAsync(request, client.Object, Ct);

        // ScheduleNewOrchestrationInstanceAsync should NOT be called when already Running/Pending
        client.Verify(c => c.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(), It.IsAny<object>(),
                It.IsAny<StartOrchestrationOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(OrchestrationRuntimeStatus.Completed)]
    [InlineData(OrchestrationRuntimeStatus.Failed)]
    [InlineData(OrchestrationRuntimeStatus.Terminated)]
    public async Task StartActivation_TerminalStatus_ReschedulesOrchestration(OrchestrationRuntimeStatus status)
    {
        var client = new Mock<DurableTaskClient>(MockBehavior.Loose, "test");
        var request = new ActivationRequest("acc1", "agent1", "jane@example.com",
            new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

        var expectedInstanceId = ActivationOrchestratorFunction.InstanceId("acc1", "agent1");

        // Existing instance in a terminal state — should be rescheduled
        var metadata = new OrchestrationMetadata("ActivationOrchestrator", expectedInstanceId)
        {
            RuntimeStatus = status
        };
        client.Setup(c => c.GetInstanceAsync(expectedInstanceId, Ct))
            .ReturnsAsync(metadata);
        client.Setup(c => c.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(), It.IsAny<object>(),
                It.IsAny<StartOrchestrationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedInstanceId);

        var fn = new StartActivationFunction(NullLogger<StartActivationFunction>.Instance);
        await fn.RunAsync(request, client.Object, Ct);

        client.Verify(c => c.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(), It.IsAny<object>(),
                It.IsAny<StartOrchestrationOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartActivation_ScheduleThrows_Propagates()
    {
        var client = new Mock<DurableTaskClient>(MockBehavior.Loose, "test");
        var request = new ActivationRequest("acc1", "agent1", "jane@example.com",
            new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

        client.Setup(c => c.GetInstanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrchestrationMetadata?)null);

        client.Setup(c => c.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(), It.IsAny<object>(),
                It.IsAny<StartOrchestrationOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Storage account not configured"));

        var fn = new StartActivationFunction(NullLogger<StartActivationFunction>.Instance);

        var act = async () => await fn.RunAsync(request, client.Object, Ct);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Storage account not configured");
    }

    [Fact]
    public async Task StartActivation_InstanceIdIsAccountAgentBased_Deterministic()
    {
        // Two separate requests for the same agent produce the same instanceId
        var request1 = new ActivationRequest("acc1", "agent1", "jane@example.com",
            new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        var request2 = new ActivationRequest("acc1", "agent1", "jane@example.com",
            new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc)); // different timestamp

        var id1 = ActivationOrchestratorFunction.InstanceId(request1.AccountId, request1.AgentId);
        var id2 = ActivationOrchestratorFunction.InstanceId(request2.AccountId, request2.AgentId);

        id1.Should().Be(id2, "same agent always maps to same orchestration instance for dedup");
    }

    [Fact]
    public async Task StartActivation_DifferentAgents_UseDifferentInstanceIds()
    {
        var id1 = ActivationOrchestratorFunction.InstanceId("acc1", "agent1");
        var id2 = ActivationOrchestratorFunction.InstanceId("acc1", "agent2");

        id1.Should().NotBe(id2);
    }
}

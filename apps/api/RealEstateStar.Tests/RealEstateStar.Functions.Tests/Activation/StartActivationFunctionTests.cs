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
/// - Skips duplicate when already running
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

    [Fact]
    public async Task StartActivation_AlreadyRunning_DoesNotThrow_SkipsDuplicate()
    {
        var client = new Mock<DurableTaskClient>(MockBehavior.Loose, "test");
        var request = new ActivationRequest("acc1", "agent1", "jane@example.com",
            new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

        var expectedInstanceId = ActivationOrchestratorFunction.InstanceId("acc1", "agent1");

        // Simulate instance already running by throwing InvalidOperationException with instanceId in message
        client.Setup(c => c.ScheduleNewOrchestrationInstanceAsync(
                It.IsAny<TaskName>(), It.IsAny<object>(),
                It.IsAny<StartOrchestrationOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                $"An orchestration with instanceId '{expectedInstanceId}' already exists."));

        var fn = new StartActivationFunction(NullLogger<StartActivationFunction>.Instance);

        // Should not propagate the exception
        var act = async () => await fn.RunAsync(request, client.Object, Ct);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartActivation_OtherException_Propagates()
    {
        var client = new Mock<DurableTaskClient>(MockBehavior.Loose, "test");
        var request = new ActivationRequest("acc1", "agent1", "jane@example.com",
            new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

        // Throw with message that does NOT contain the instanceId
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

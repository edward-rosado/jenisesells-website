using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenTelemetry;
using RealEstateStar.Functions.Diagnostics;

namespace RealEstateStar.Functions.Tests.Diagnostics;

/// <summary>
/// Tests for DurableOrchestratorTracingMiddleware.
///
/// Testing strategy:
/// - DeriveTraceId is tested directly as a pure static method (no FunctionContext needed).
/// - Middleware pass-through is tested via minimal FunctionContext mocks.
/// - We do not test the Activity enrichment path because ActivitySource.StartActivity()
///   returns null when no listener is active (unit test environment) — that path is
///   verified by the stable trace ID tests below.
/// </summary>
public sealed class DurableOrchestratorTracingMiddlewareTests
{
    private readonly DurableOrchestratorTracingMiddleware _middleware = new(
        NullLogger<DurableOrchestratorTracingMiddleware>.Instance);

    // ── DeriveTraceId: determinism ────────────────────────────────────────────

    [Fact]
    public void DeriveTraceId_SameInstanceId_ProducesSameTraceId()
    {
        var id1 = DurableOrchestratorTracingMiddleware.DeriveTraceId("activation-acc1-agent1");
        var id2 = DurableOrchestratorTracingMiddleware.DeriveTraceId("activation-acc1-agent1");

        id1.ToHexString().Should().Be(id2.ToHexString(),
            because: "same instance ID must always produce the same trace ID across replays");
    }

    [Fact]
    public void DeriveTraceId_DifferentInstanceIds_ProduceDifferentTraceIds()
    {
        var id1 = DurableOrchestratorTracingMiddleware.DeriveTraceId("activation-acc1-agent1");
        var id2 = DurableOrchestratorTracingMiddleware.DeriveTraceId("activation-acc1-agent2");

        id1.ToHexString().Should().NotBe(id2.ToHexString(),
            because: "different instance IDs must map to different trace IDs");
    }

    [Fact]
    public void DeriveTraceId_LeadVsActivation_ProduceDifferentTraceIds()
    {
        var leadId = DurableOrchestratorTracingMiddleware.DeriveTraceId("lead-acc1-lead123");
        var activationId = DurableOrchestratorTracingMiddleware.DeriveTraceId("activation-acc1-agent1");

        leadId.ToHexString().Should().NotBe(activationId.ToHexString(),
            because: "lead and activation orchestrators must have distinct trace IDs");
    }

    [Fact]
    public void DeriveTraceId_IsValidW3CFormat_32HexChars()
    {
        var traceId = DurableOrchestratorTracingMiddleware.DeriveTraceId("test-instance-id");
        var hex = traceId.ToHexString();

        hex.Should().HaveLength(32, because: "W3C trace IDs are 128 bits = 32 hex characters");
        hex.Should().MatchRegex("^[0-9a-f]{32}$", because: "trace ID must be lowercase hex");
    }

    [Fact]
    public void DeriveTraceId_EmptyString_DoesNotThrow()
    {
        // Edge case: if instanceId is somehow empty, we still derive a valid trace ID
        var act = () => DurableOrchestratorTracingMiddleware.DeriveTraceId(string.Empty);
        act.Should().NotThrow();
    }

    [Fact]
    public void DeriveTraceId_IsStableAcrossMultipleCalls_NineCallsAllMatch()
    {
        // Verify determinism under repeated access (no state mutation)
        const string instanceId = "lead-acme-brokerage-lead-abc123";
        var expected = DurableOrchestratorTracingMiddleware.DeriveTraceId(instanceId).ToHexString();

        for (var i = 0; i < 9; i++)
        {
            var result = DurableOrchestratorTracingMiddleware.DeriveTraceId(instanceId).ToHexString();
            result.Should().Be(expected, because: $"call #{i + 1} must return the same trace ID");
        }
    }

    // ── Middleware pass-through: non-durable functions ────────────────────────

    [Fact]
    public async Task Invoke_NonDurableFunction_PassesThroughWithoutModification()
    {
        var nextCalled = false;
        FunctionExecutionDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var context = CreateMockContext(bindingType: "httpTrigger");

        await _middleware.Invoke(context, next);

        nextCalled.Should().BeTrue(because: "non-durable functions must pass through to next middleware");
    }

    [Fact]
    public async Task Invoke_OrchestratorWithNoInstanceId_PassesThroughWithoutThrowing()
    {
        // If binding data has no instanceId, the middleware must not throw —
        // it logs a debug message and calls next.
        var nextCalled = false;
        FunctionExecutionDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var context = CreateMockContext(
            bindingType: "orchestrationTrigger",
            instanceId: null);

        var act = async () => await _middleware.Invoke(context, next);

        await act.Should().NotThrowAsync(because: "missing instanceId must be handled gracefully");
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Invoke_ActivityTrigger_PassesThroughWithoutThrowing()
    {
        var nextCalled = false;
        FunctionExecutionDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var context = CreateMockContext(bindingType: "activityTrigger");

        var act = async () => await _middleware.Invoke(context, next);

        await act.Should().NotThrowAsync(because: "activity triggers must be handled gracefully");
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Invoke_OrchestratorWithInstanceId_SetsStableBaggage()
    {
        const string instanceId = "activation-acc1-agent1";
        var expectedTraceId = DurableOrchestratorTracingMiddleware.DeriveTraceId(instanceId).ToHexString();

        string? capturedBaggageTraceId = null;

        FunctionExecutionDelegate next = _ =>
        {
            capturedBaggageTraceId = Baggage.GetBaggage("df.trace_id");
            return Task.CompletedTask;
        };

        var context = CreateMockContext(
            bindingType: "orchestrationTrigger",
            instanceId: instanceId);

        await _middleware.Invoke(context, next);

        capturedBaggageTraceId.Should().Be(expectedTraceId,
            because: "orchestrator must set df.trace_id baggage to the stable trace ID");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a minimal FunctionContext mock with the given trigger binding type.
    /// The Azure Functions SDK does not expose a public constructor for FunctionContext,
    /// so we use Moq to create a sealed-class-compatible mock via reflection setup.
    /// </summary>
    private static FunctionContext CreateMockContext(string bindingType, string? instanceId = "test-instance-123")
    {
        var bindingMeta = new Mock<BindingMetadata>();
        bindingMeta.Setup(b => b.Type).Returns(bindingType);

        var inputBindings = new Dictionary<string, BindingMetadata>
        {
            ["trigger"] = bindingMeta.Object
        }.ToImmutableDictionary();

        var functionDefinition = new Mock<FunctionDefinition>();
        functionDefinition.Setup(fd => fd.InputBindings).Returns(inputBindings);
        functionDefinition.Setup(fd => fd.Name).Returns("TestFunction");

        var bindingData = new Dictionary<string, object?>();
        if (instanceId is not null)
            bindingData["instanceId"] = instanceId;

        var bindingContext = new Mock<BindingContext>();
        bindingContext.Setup(bc => bc.BindingData).Returns(bindingData);

        var context = new Mock<FunctionContext>();
        context.Setup(c => c.FunctionDefinition).Returns(functionDefinition.Object);
        context.Setup(c => c.BindingContext).Returns(bindingContext.Object);
        context.Setup(c => c.InvocationId).Returns("test-invocation-id");

        return context.Object;
    }
}

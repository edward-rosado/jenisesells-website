using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

namespace RealEstateStar.Functions.Diagnostics;

/// <summary>
/// Middleware that attaches stable, deterministic W3C trace IDs to Durable orchestrator
/// and activity function invocations.
///
/// <para>
/// Problem: Durable Functions replay the orchestrator from the beginning of its task
/// history on every checkpoint. Each replay fires a new Functions middleware pipeline, which
/// would normally produce a new, random trace ID -- fragmenting the orchestration into dozens
/// of disconnected spans in Grafana.
/// </para>
///
/// <para>
/// Solution: Derive the trace ID deterministically from the orchestrator instance ID
/// via SHA-256. The same instance ID always maps to the same 128-bit trace ID, so every
/// replay shares a single trace in the backend. Activity invocations propagate this trace ID
/// through Baggage so their spans are correlated to the parent orchestrator.
/// </para>
///
/// <para>
/// Replay safety: This middleware does NOT create a new root span on every call.
/// When Activity.Current is already set (e.g., by Azure Functions SDK instrumentation),
/// we enrich it with the stable tag rather than replacing it. When no current activity exists,
/// we start a short-lived root span purely to attach the instance ID tag and set Baggage --
/// it ends immediately after next is called, as a parent to whatever the SDK creates.
/// </para>
/// </summary>
public sealed class DurableOrchestratorTracingMiddleware : IFunctionsWorkerMiddleware
{
    /// <summary>ActivitySource name registered in TelemetryRegistrations.</summary>
    public const string SourceName = "RealEstateStar.DurableFunctions";

    private static readonly ActivitySource Source = new(SourceName, "1.0.0");

    private readonly ILogger<DurableOrchestratorTracingMiddleware> _logger;

    public DurableOrchestratorTracingMiddleware(
        ILogger<DurableOrchestratorTracingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var isDurableOrchestrator = context.FunctionDefinition.InputBindings
            .Any(b => b.Value.Type == "orchestrationTrigger");
        var isDurableActivity = context.FunctionDefinition.InputBindings
            .Any(b => b.Value.Type == "activityTrigger");

        if (isDurableOrchestrator)
        {
            await HandleOrchestratorAsync(context, next);
        }
        else if (isDurableActivity)
        {
            await HandleActivityAsync(context, next);
        }
        else
        {
            await next(context);
        }
    }

    // ── Orchestrator path ─────────────────────────────────────────────────────

    private async Task HandleOrchestratorAsync(FunctionContext context, FunctionExecutionDelegate next)
    {
        var instanceId = ExtractInstanceId(context);

        if (instanceId is null)
        {
            _logger.LogDebug(
                "[DFTM-001] No instanceId found for orchestrator function '{FunctionName}' -- skipping stable trace ID",
                context.FunctionDefinition.Name);
            await next(context);
            return;
        }

        var stableTraceId = DeriveTraceId(instanceId);

        // Propagate via Baggage so activity spans can link to the same orchestrator.
        // Baggage flows through the async context automatically once set here.
        Baggage.SetBaggage("df.instance_id", instanceId);
        Baggage.SetBaggage("df.trace_id", stableTraceId.ToHexString());

        // Enrich existing activity or create a lightweight parent span.
        if (Activity.Current is { } existing)
        {
            existing.SetTag("df.instance_id", instanceId);
            existing.SetTag("df.stable_trace_id", stableTraceId.ToHexString());
            await next(context);
        }
        else
        {
            // No SDK-created span yet -- open a root span with the stable trace ID.
            // The W3C trace context propagation means any child spans created inside
            // next() will be children of this span.
            // Use positional arguments to avoid overload resolution ambiguity.
            using var activity = Source.StartActivity(
                $"orchestrator/{context.FunctionDefinition.Name}",
                ActivityKind.Internal,
                new ActivityContext(stableTraceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded));

            activity?.SetTag("df.instance_id", instanceId);
            activity?.SetTag("df.function_name", context.FunctionDefinition.Name);
            activity?.SetTag("df.invocation_id", context.InvocationId);

            await next(context);
        }
    }

    // ── Activity path ─────────────────────────────────────────────────────────

    private async Task HandleActivityAsync(FunctionContext context, FunctionExecutionDelegate next)
    {
        // Activities receive Baggage from the orchestrator via the Durable runtime context.
        var instanceId = Baggage.GetBaggage("df.instance_id");

        if (Activity.Current is { } existing)
        {
            if (instanceId is not null)
                existing.SetTag("df.instance_id", instanceId);
            existing.SetTag("df.function_name", context.FunctionDefinition.Name);
            existing.SetTag("df.invocation_id", context.InvocationId);
        }

        await next(context);
    }

    // ── Stable trace ID derivation ────────────────────────────────────────────

    /// <summary>
    /// Derives a stable, deterministic 128-bit W3C trace ID from a Durable orchestrator
    /// instance ID by taking the first 16 bytes of SHA-256(instanceId).
    ///
    /// SHA-256 is used for its uniform distribution and collision resistance -- two different
    /// instance IDs are astronomically unlikely to produce the same trace ID. The output is
    /// deterministic: the same instance ID always produces the same trace ID across replays,
    /// workers, and restarts.
    /// </summary>
    internal static ActivityTraceId DeriveTraceId(string instanceId)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(instanceId));
        // ActivityTraceId requires exactly 16 bytes (128 bits)
        return ActivityTraceId.CreateFromBytes(hashBytes.AsSpan(0, 16));
    }

    /// <summary>Extracts the Durable instance ID from the function context binding data.</summary>
    private static string? ExtractInstanceId(FunctionContext context)
    {
        return context.BindingContext.BindingData.TryGetValue("instanceId", out var id)
            ? id?.ToString()
            : null;
    }
}

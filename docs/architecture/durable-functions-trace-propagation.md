# Durable Functions trace propagation

How the R5 `DurableOrchestratorTracingMiddleware` turns an 18-activity activation into a single connected trace, with correlation ID flowing via `Baggage` instead of method parameters.

**Why it matters**: before R5, each activity function produced its own span via its client `ActivitySource` (Claude, GDrive, Azure Table, etc.) but there was no parent span binding them together. An 8-minute activation produced ~18 disconnected spans in Grafana Tempo with no way to reconstruct "this agent's activation" as one waterfall. R5 fixes this with a thin middleware layer on the DF worker host that creates an orchestrator-level parent span from the instance ID and propagates correlation ID via `Baggage` so every downstream client call inherits it automatically.

```mermaid
sequenceDiagram
    participant Caller as API / Queue trigger
    participant Middleware as DurableOrchestratorTracingMiddleware
    participant Orch as ActivationOrchestratorFunction
    participant Act as Activity Function<br/>e.g. BuildLocalizedSiteContent
    participant Client as Claude / GDrive / Table<br/>existing ActivitySource
    participant Tempo as Grafana Tempo

    Caller->>Middleware: ActivationRequest<br/>correlationId abc123
    Note over Middleware: Detect OrchestrationTrigger<br/>Extract instanceId<br/>Extract correlationId from input
    Middleware->>Middleware: traceId SHA-256 instanceId<br/>stable across replays
    Middleware->>Middleware: Start orchestrator span<br/>df.orchestrator.Activation
    Middleware->>Middleware: Baggage.SetBaggage correlation.id abc123
    Middleware->>Middleware: LogContext.PushProperty CorrelationId abc123
    Middleware->>Orch: Invoke with FunctionContext

    Note over Orch: Orchestrator body runs<br/>Every CallActivityAsync<br/>inherits parent context

    Orch->>Act: CallActivityAsync<br/>BuildLocalizedSiteContent
    Act->>Middleware: Middleware wraps activity too
    Middleware->>Middleware: Start activity span<br/>df.activity.BuildLocalized<br/>parent orchestrator span
    Middleware->>Middleware: Baggage already set<br/>correlation.id abc123

    Act->>Client: SendAsync to Claude
    Note over Client: Claude ActivitySource<br/>creates its own span<br/>inherits orchestrator as parent<br/>via Activity.Current
    Client->>Client: HttpClient call<br/>OTel HTTP instrumentation<br/>auto-propagates Baggage<br/>in traceparent header
    Client-->>Act: Response

    Act-->>Orch: Activity result
    Middleware->>Middleware: Close activity span<br/>result.success true

    Note over Orch: 17 more activities<br/>all under same trace

    Orch-->>Middleware: Orchestrator complete
    Middleware->>Middleware: Close orchestrator span<br/>result.success true
    Middleware-->>Caller: Result

    Middleware->>Tempo: Export all spans<br/>via OTLP
    Note over Tempo: Single trace with<br/>orchestrator as root<br/>18 activity children<br/>client spans as grandchildren
```

**Replay safety**: Durable Functions replays the orchestrator function on every checkpoint. Without care, each replay would produce a new orchestrator span. The middleware avoids this by seeding the trace ID from `SHA-256(instanceId)` — every replay of the same orchestration computes the same trace ID, so spans from all replays collapse into one trace. Activity spans inherit the same trace ID via `Activity.Current?.Context` parent linking.

**Correlation ID is free**: once `Baggage.SetBaggage("correlation.id", ...)` fires in the middleware, OTel's HTTP client instrumentation automatically propagates it in the `baggage` header on every outbound HTTP call. The agent's Gmail call, the Claude call, the GDrive call — all inherit the correlation ID without any code in the activity function having to thread it through. Logs inside the activity also pick it up via the `LogContext.PushProperty` that runs in the same middleware frame.

**What this unlocks**:
- **Single-click reconstruction**: given `instanceId`, Grafana Tempo returns the full 18-activity trace as one waterfall.
- **Error budget math**: orchestrator-level spans let §18.7 SLOs be defined on orchestration success rate directly, not inferred from activity counts.
- **Support runbook works**: `docs/runbooks/trace-reconstruction.md` can now say "paste the ref into Tempo" as a single step.

See design spec §5.8 for the full middleware code, the `IsReplaying` guard pattern, and the `Orchestrators_UseReplaySafeLogger` architecture test that enforces the discipline across every new orchestrator.

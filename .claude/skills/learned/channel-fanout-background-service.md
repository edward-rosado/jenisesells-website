---
name: channel-fanout-background-service
description: "Decompose monolithic BackgroundService workers into Channel<T> fan-out pipelines with independent workers"
user-invocable: false
origin: auto-extracted
---

# Channel<T> Fan-Out BackgroundService Pattern

**Extracted:** 2026-03-20
**Context:** When a BackgroundService worker grows to handle multiple independent pipelines inline (e.g., enrichment + CMA + home search), decompose into separate workers connected by bounded channels.

## Problem

A single BackgroundService doing enrichment → CMA generation → home search inline becomes:
- Hard to test (must mock everything for every test)
- Hard to scale (one slow pipeline blocks others)
- Hard to observe (OTel spans mix concerns)
- Fragile (one pipeline failure can affect others)

## Solution

### 1. Define a Channel per pipeline stage

```csharp
// Bounded channel with backpressure — singleton in DI
public sealed class CmaProcessingChannel
{
    private readonly Channel<CmaProcessingRequest> _channel =
        Channel.CreateBounded<CmaProcessingRequest>(new BoundedChannelOptions(50)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });

    public ChannelReader<CmaProcessingRequest> Reader => _channel.Reader;
    public ChannelWriter<CmaProcessingRequest> Writer => _channel.Writer;
}
```

### 2. Parent worker dispatches via channel write

```csharp
// In LeadProcessingWorker — after enrichment + agent notification:
if (lead.LeadType is LeadType.Seller or LeadType.Both)
    await cmaChannel.Writer.WriteAsync(
        new CmaProcessingRequest(agentId, lead, enrichment, score, correlationId), ct);

if (lead.LeadType is LeadType.Buyer or LeadType.Both)
    await homeSearchChannel.Writer.WriteAsync(
        new HomeSearchProcessingRequest(agentId, lead, correlationId), ct);
```

### 3. Child worker reads from its channel

```csharp
public sealed class CmaProcessingWorker(
    CmaProcessingChannel channel,
    ICompAggregator compAggregator,
    // ... other deps
    ILogger<CmaProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try { await ProcessCmaAsync(request, stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "[CMA-WORKER-002] ...");
            }
        }
    }
}
```

### 4. DI registration pattern

```csharp
// Channels — singleton (shared between producer and consumer)
builder.Services.AddSingleton<CmaProcessingChannel>();
builder.Services.AddSingleton<HomeSearchProcessingChannel>();

// Workers — hosted services
builder.Services.AddHostedService<CmaProcessingWorker>();
builder.Services.AddHostedService<HomeSearchProcessingWorker>();
```

### 5. Test pattern — verify channel dispatch

```csharp
// Use REAL channel instances in tests (not mocks)
private readonly CmaProcessingChannel _cmaChannel = new();

// After worker processes, verify channel contents:
_cmaChannel.Reader.TryRead(out var cmaRequest).Should().BeTrue();
cmaRequest!.AgentId.Should().Be("test-agent");

// Verify other channel is empty:
_homeSearchChannel.Reader.TryRead(out _).Should().BeFalse();
```

### 6. Integration test stubs

Add no-op stubs in TestWebApplicationFactory for all new interfaces:
```csharp
services.AddSingleton<ICompAggregator, NoOpCompAggregator>();
services.AddSingleton<ICmaAnalyzer, NoOpCmaAnalyzer>();
// ... one per interface the worker depends on
```

### 7. Each pipeline gets its own Diagnostics class

```csharp
public static class CmaDiagnostics
{
    public const string ServiceName = "RealEstateStar.Cma";
    public static readonly ActivitySource ActivitySource = new(ServiceName);
    private static readonly Meter Meter = new(ServiceName);
    public static readonly Counter<long> CmaGenerated = Meter.CreateCounter<long>("cma.generated");
    // ... histograms for each step duration
}
```

Register in OTel: `.AddSource(CmaDiagnostics.ServiceName)` for both tracing and metrics.

## Key Pitfalls

- **BoundedChannelOptions.SingleReader = true** — enables optimized fast path; only valid if exactly one BackgroundService reads from the channel
- **FullMode.Wait** — producer blocks if channel is full (backpressure). Use FullMode.DropOldest only if you accept data loss
- **Always catch non-cancellation exceptions** in the `await foreach` loop — one bad message must not kill the worker
- **Channel.Writer.Complete()** in tests — call this before `await worker.ExecuteTask!` so the worker exits cleanly
- **Don't mock channels** — use real instances in unit tests; they're lightweight and let you verify actual dispatch behavior

## When to Use

- A BackgroundService handles 2+ independent async pipelines
- You want independent scaling, failure isolation, or observability per pipeline
- The parent worker produces work items that child workers consume independently

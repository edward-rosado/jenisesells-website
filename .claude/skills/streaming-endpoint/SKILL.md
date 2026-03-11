---
name: streaming-endpoint
description: SSE/streaming endpoint patterns for .NET and React. Use when writing, reviewing, or debugging Server-Sent Events, IAsyncEnumerable endpoints, or streaming chat responses.
---

# Streaming Endpoint Patterns

## .NET SSE Endpoint

```csharp
app.MapPost("/chat/{sessionId}", async (
    string sessionId,
    ChatRequest request,
    IChatService chatService,
    ISessionStore sessionStore,
    CancellationToken ct) =>
{
    var session = await sessionStore.LoadAsync(sessionId, ct);
    if (session is null) return Results.NotFound();

    return Results.Stream(async stream =>
    {
        var writer = new StreamWriter(stream) { AutoFlush = true };

        await foreach (var chunk in chatService.StreamAsync(session, request.Message, ct))
        {
            await writer.WriteAsync($"data: {chunk}\n\n");
        }

        await writer.WriteAsync("data: [DONE]\n\n");
        await sessionStore.SaveAsync(session, ct);
    }, "text/event-stream");
});
```

## Key Patterns

### CancellationToken Propagation
- Pass `ct` through every async call in the chain
- Use `[EnumeratorCancellation]` on IAsyncEnumerable methods
- Check `ct.ThrowIfCancellationRequested()` in tight loops

```csharp
public async IAsyncEnumerable<string> StreamAsync(
    OnboardingSession session,
    string message,
    [EnumeratorCancellation] CancellationToken ct)
{
    // ct is automatically linked to the enumeration
    while ((line = await reader.ReadLineAsync(ct)) is not null)
    {
        ct.ThrowIfCancellationRequested();
        yield return ProcessLine(line);
    }
}
```

### Message Persistence Timing
- Add messages to session AFTER streaming completes (not before)
- This prevents partial messages from being persisted on disconnect

```csharp
// After the streaming loop completes:
session.Messages.Add(new ChatMessage { Role = "user", Content = userMessage });
session.Messages.Add(new ChatMessage { Role = "assistant", Content = fullResponse.ToString() });
```

### Duplicate Message Prevention
- User message should be added in ONE place only (service or endpoint, not both)
- Document which layer is responsible with a comment

## Frontend: Reading SSE Streams

```tsx
const reader = res.body?.getReader();
const decoder = new TextDecoder();
let buffer = "";

while (true) {
  const { done, value } = await reader.read();
  if (done) break;

  buffer += decoder.decode(value, { stream: true });
  const lines = buffer.split("\n");
  buffer = lines.pop() ?? "";  // Keep incomplete line in buffer

  for (const line of lines) {
    if (!line.startsWith("data: ")) continue;
    const data = line.slice(6);
    if (data === "[DONE]") continue;
    // Process chunk
  }
}
```

### State Updates During Streaming
Use functional setState to avoid stale closures:

```tsx
setMessages((prev) => {
  const next = [...prev];
  next[next.length - 1] = { role: "assistant", content: updatedContent };
  return next;
});
```

## Critical: StreamWriter AutoFlush Breaks Kestrel

`new StreamWriter(stream) { AutoFlush = true }` causes a **synchronous** `Flush()` on every write. Kestrel disallows synchronous I/O by default, so this throws `InvalidOperationException: Synchronous operations are disallowed`. Use manual `FlushAsync()` instead:

```csharp
// ❌ WRONG — AutoFlush triggers synchronous Flush(), Kestrel throws
var writer = new StreamWriter(stream) { AutoFlush = true };
await writer.WriteAsync("data: chunk\n\n");  // 💥 Sync Flush() internally

// ✅ CORRECT — manual async flush
var writer = new StreamWriter(stream);
await writer.WriteAsync("data: chunk\n\n");
await writer.FlushAsync();
```

## Critical: Results.Stream() Swallows Exceptions

`Results.Stream()` starts the HTTP response with status 200 immediately. Any exception inside the callback is **silently lost** — the global exception handler never sees it. You MUST wrap the callback body in try-catch and log explicitly:

```csharp
return Results.Stream(async stream =>
{
    try
    {
        var writer = new StreamWriter(stream) { AutoFlush = true };
        await foreach (var chunk in chatService.StreamAsync(session, request.Message, ct))
        {
            await writer.WriteAsync($"data: {chunk}\n\n");
        }
        await writer.WriteAsync("data: [DONE]\n\n");
        await sessionStore.SaveAsync(session, ct);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("[STREAM-CANCEL] Client disconnected for session {SessionId}", sessionId);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[STREAM-FAIL] Stream failed for session {SessionId}", sessionId);
        try
        {
            var errorWriter = new StreamWriter(stream) { AutoFlush = true };
            await errorWriter.WriteAsync("data: [ERROR] Internal error\n\n");
        }
        catch { /* Stream may already be broken */ }
    }
}, "text/event-stream");
```

## Critical: C# Cannot yield in try-catch

C# does not allow `yield return` inside a `try` block that has a `catch` clause (CS1626/CS1631). When you need error handling around yielded values in `IAsyncEnumerable<T>` methods, collect the value into a local variable first, then yield outside the try-catch:

```csharp
// ❌ WRONG — CS1626 compiler error
try
{
    var result = await DoWork();
    yield return result;  // Cannot yield in try with catch
}
catch (Exception ex) { logger.LogError(ex, "Failed"); }

// ✅ CORRECT — collect first, yield outside
string resultText;
try
{
    resultText = await DoWork();
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed");
    resultText = "Error: operation failed";
}
yield return resultText;
```

## Critical: Log Anthropic API Error Bodies

`EnsureSuccessStatusCode()` throws a generic `HttpRequestException` and discards the response body containing the actual error message. Always read the body before throwing:

```csharp
// ❌ WRONG — error body is lost
response.EnsureSuccessStatusCode();

// ✅ CORRECT — log the actual error
if (!response.IsSuccessStatusCode)
{
    var errorBody = await response.Content.ReadAsStringAsync(ct);
    logger.LogError("[API-ERR] Anthropic returned {StatusCode}. Body: {ErrorBody}",
        (int)response.StatusCode, errorBody);
    throw new HttpRequestException($"Anthropic API returned {(int)response.StatusCode}: {errorBody}");
}
```

## Anti-Patterns to Flag

| Anti-Pattern | Fix |
|---|---|
| Missing `[EnumeratorCancellation]` | Add attribute to `CancellationToken` param |
| User message added in both endpoint and service | Pick ONE location, comment why |
| `session.Messages.Add()` before streaming | Move to after streaming completes |
| No `[DONE]` sentinel | Always send termination signal |
| Reading state directly in async callback | Use functional `setState(prev => ...)` |
| No buffer handling for partial SSE chunks | Split on `\n`, keep last incomplete line |
| No try-catch in `Results.Stream()` callback | Wrap body in try-catch, log with unique error code |
| `yield return` inside try-catch | Collect to local var, yield outside try-catch |
| `EnsureSuccessStatusCode()` without body logging | Read error body first, log it, then throw |
| No unique error codes in log messages | Use `[PREFIX-NNN]` format for every log statement |
| `StreamWriter` with `AutoFlush = true` on Kestrel stream | Use manual `FlushAsync()` — AutoFlush calls sync `Flush()` |

## Testing Requirements

- [ ] Happy path: full response streamed and persisted
- [ ] Cancellation: client disconnect stops processing
- [ ] Empty response: handled gracefully
- [ ] Tool use during streaming: tool results included in stream
- [ ] Message persistence: both user and assistant messages saved after completion
- [ ] No duplicate messages: check message count after roundtrip
- [ ] Content-Type header: `text/event-stream`
- [ ] DONE sentinel: last message is `data: [DONE]`

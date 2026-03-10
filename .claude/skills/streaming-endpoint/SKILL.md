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

## Anti-Patterns to Flag

| Anti-Pattern | Fix |
|---|---|
| Missing `[EnumeratorCancellation]` | Add attribute to `CancellationToken` param |
| User message added in both endpoint and service | Pick ONE location, comment why |
| `session.Messages.Add()` before streaming | Move to after streaming completes |
| No `[DONE]` sentinel | Always send termination signal |
| Reading state directly in async callback | Use functional `setState(prev => ...)` |
| No buffer handling for partial SSE chunks | Split on `\n`, keep last incomplete line |

## Testing Requirements

- [ ] Happy path: full response streamed and persisted
- [ ] Cancellation: client disconnect stops processing
- [ ] Empty response: handled gracefully
- [ ] Tool use during streaming: tool results included in stream
- [ ] Message persistence: both user and assistant messages saved after completion
- [ ] No duplicate messages: check message count after roundtrip
- [ ] Content-Type header: `text/event-stream`
- [ ] DONE sentinel: last message is `data: [DONE]`

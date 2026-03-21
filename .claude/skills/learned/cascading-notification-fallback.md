---
name: cascading-notification-fallback
description: "Preferred-channel notification with cascading fallback: WhatsApp → Email → File Storage, no silent drops"
user-invocable: false
origin: auto-extracted
---

# Cascading Notification Fallback Pattern

**Extracted:** 2026-03-20
**Context:** Multi-channel lead notification where no notification should be silently lost

## Problem
Sending notifications via a single channel (email, WhatsApp, etc.) means a channel outage = lost notification. Sending via all channels simultaneously creates duplicate noise. Need a preferred channel with automatic fallback, plus a last-resort persistence layer so devs can always see output.

## Solution
Cascading try/catch with a `sent` flag that gates each subsequent tier.

```csharp
var whatsAppSent = false;
var emailSent = false;

// Tier 1: Preferred channel
try
{
    if (agentConfig?.Integrations?.WhatsApp?.OptedIn == true)
    {
        await whatsAppNotifier.NotifyAsync(...);
        whatsAppSent = true;
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "[LEAD-040] WhatsApp failed, falling back to email");
}

// Tier 2: Fallback (only if preferred didn't send)
if (!whatsAppSent)
{
    try
    {
        await emailNotifier.SendAsync(...);
        emailSent = true;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[LEAD-041] Email fallback also failed");
    }
}

// Tier 3: Last resort — persist to file storage (never silent drop)
if (!whatsAppSent && !emailSent)
{
    try
    {
        await fileStorage.WriteAsync(...);
        logger.LogWarning("[LEAD-042] Both channels failed, persisted to file storage");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[LEAD-043] All channels failed — notification may be lost");
    }
}
```

### Key design decisions
- **Preferred channel skips lower tiers on success** — no duplicate notifications
- **Each tier is independent try/catch** — one failure never blocks the next
- **File storage is the last resort, not a parallel channel** — it's for observability, not user notification
- **LocalFileStorageProvider in dev** — devs browse `~/real-estate-star/storage/` to see all agent output on disk
- **NoopFileStorageProvider in prod** until Google Drive is wired — but the cascade still logs [LEAD-043] so ops knows

### DI registration pattern
```csharp
// Development: real files on disk
if (builder.Environment.IsDevelopment())
    builder.Services.AddSingleton<IFileStorageProvider>(sp =>
        new LocalFileStorageProvider(storagePath, sp.GetRequiredService<ILogger<...>>()));
else
    builder.Services.AddSingleton<IFileStorageProvider, NoopFileStorageProvider>();
```

## When to Use
- Multi-channel notifications where one channel is preferred
- Any system where "notification silently dropped" is unacceptable
- Local dev environments where you need to see agent/system output without external services

## Testing
Each tier combination needs a test:
1. Tier 1 succeeds → tiers 2+3 never called
2. Tier 1 fails → tier 2 succeeds → tier 3 never called
3. Tiers 1+2 fail → tier 3 fires
4. All three fail → logged, does not throw
5. Tier 1 not configured → tier 2 fires directly

---
name: state-machine-guard
description: State machine patterns and guards for workflow systems. Use when writing, reviewing, or debugging state machines, workflow transitions, or multi-step processes (onboarding, payment flows, order processing).
---

# State Machine Guard Patterns

## Core Principles

1. **All transitions go through the machine** — never set `session.CurrentState` directly
2. **Every transition is testable** — including terminal states and invalid transitions
3. **Idempotent external triggers** — webhooks and callbacks may fire multiple times
4. **Concurrent protection** — state changes must be atomic

## State Machine Design

```csharp
public class OnboardingStateMachine
{
    // Define allowed transitions explicitly
    private static readonly Dictionary<OnboardingState, OnboardingState[]> Transitions = new()
    {
        [OnboardingState.ScrapeProfile] = [OnboardingState.ConfirmIdentity],
        [OnboardingState.ConfirmIdentity] = [OnboardingState.CollectBranding],
        // ... all transitions
        [OnboardingState.TrialActivated] = [],  // Terminal — no transitions out
    };

    public bool CanAdvance(OnboardingSession session, OnboardingState target)
        => Transitions.TryGetValue(session.CurrentState, out var allowed)
           && allowed.Contains(target);

    public void Advance(OnboardingSession session, OnboardingState target)
    {
        if (!CanAdvance(session, target))
            throw new InvalidOperationException(
                $"Cannot transition from {session.CurrentState} to {target}");

        session.CurrentState = target;
        session.UpdatedAt = DateTime.UtcNow;
    }
}
```

## Webhook-Driven Transitions

```csharp
// 1. Check idempotency FIRST
if (session.LastEventId == externalEvent.Id)
    return Results.Ok();  // Skip duplicate

// 2. Check if transition is valid
if (!stateMachine.CanAdvance(session, targetState))
{
    logger.LogWarning("Cannot advance {SessionId} from {Current} to {Target}",
        session.Id, session.CurrentState, targetState);
    return Results.Ok();  // Don't error — webhook may be out of order
}

// 3. Advance and save atomically
session.LastEventId = externalEvent.Id;
stateMachine.Advance(session, targetState);
await sessionStore.SaveAsync(session, ct);
```

## Anti-Patterns to Flag

| Anti-Pattern | Fix |
|---|---|
| `session.CurrentState = newState` (direct) | `stateMachine.Advance(session, newState)` |
| No `CanAdvance` check before `Advance` | Always check, especially in webhooks |
| Terminal state has outgoing transitions | Terminal states must have empty transition arrays |
| Missing idempotency on webhook transitions | Store and check external event IDs |
| State mutation without `UpdatedAt` | Machine should set `UpdatedAt` on every transition |
| No logging on rejected transitions | Log current + target state for debugging |

## Testing Requirements

### Transition Coverage (MANDATORY)
Every state must have tests for:
- [ ] All valid outgoing transitions (advance succeeds)
- [ ] At least one invalid transition (advance throws/returns false)
- [ ] Terminal state: no outgoing transitions allowed

### Webhook/External Trigger Tests
- [ ] First invocation: state advances correctly
- [ ] Duplicate invocation: idempotent (no error, no double-advance)
- [ ] Out-of-order invocation: rejected gracefully (200 response, no state change)
- [ ] Missing session: handled without crash

### Concurrent Access Tests
- [ ] Two simultaneous transitions: only one succeeds
- [ ] Load + advance + save: atomic under concurrency

### State Persistence Tests
- [ ] Save and reload preserves current state
- [ ] Save and reload preserves transition metadata (timestamps, event IDs)

## Generating Transition Matrix

For documentation, generate a transition matrix:

```
| From State      | Valid Targets         |
|-----------------|----------------------|
| ScrapeProfile   | ConfirmIdentity      |
| ConfirmIdentity | CollectBranding      |
| ...             | ...                  |
| TrialActivated  | (terminal)           |
```

Every cell in this matrix should have a corresponding test.

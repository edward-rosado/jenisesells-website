# Azure Durable Functions — Operations Guide

**Date:** 2026-04-02
**Status:** Draft — deployed with migration

## Architecture Overview

The API (Container App) handles HTTP/SignalR. Pipeline orchestration moves to Azure Flex Consumption.

- **Activation pipeline**: Durable orchestrator + 19 activity wrappers + queue starter
- **Lead pipeline**: Durable orchestrator + 11 activity wrappers + queue starter
- **WhatsApp**: Queue-triggered webhook processor + timer-triggered retry (30min)
- **TrialExpiryService**: Only BackgroundService remaining in the API

## Cost Expectations

| Component | Before | After | Savings |
|-----------|--------|-------|---------|
| Container App (API + workers) | ~$15-30/mo | ~$5-10/mo (API only) | 50-65% |
| Flex Consumption | N/A | ~$2-5/mo | N/A |
| Table Storage (idempotency + cache) | N/A | ~$0.01/mo | N/A |
| Always-ready instance (lead orchestrator) | N/A | ~$1.80/mo | N/A |
| **Total** | **~$15-30/mo** | **~$9-17/mo** | **~40-50%** |

At scale: pipelines auto-scale per-queue independently. CMA no longer starves lead processing.

## Observability

### Execution History

Every orchestration/activity execution is logged to Azure Table Storage by the Durable Task framework:
- Full audit trail: start/end time, input, output, status per step
- Replay history: see exactly where an orchestration failed and was retried
- Instance management: query running/failed/completed via management API

### Health Checks

- `/health/ready` includes `DurableFunctionsHealthCheck` (queries DF management API)
- Tagged `["ready", "workers"]` for backward compatibility

### Structured Log Codes

| Code Pattern | Component |
|---|---|
| `[ORCH-0xx]` | Lead orchestrator |
| `[ACT-FN-0xx]` | Activation |
| `[WA-FN-0xx]` | WhatsApp |
| `[CACHE-0xx]` | Distributed content cache |
| `[IDEMPOTENCY-0xx]` | Idempotency store |
| `[HEALTH-DF-0xx]` | Health check |
| `[SEND-0xx]` | Email/WhatsApp sends |

### Recommended Azure Monitor Alerts

1. Failed orchestrations: `runtimeStatus == "Failed"` count > 0
2. Poison queue depth: `whatsapp-webhooks-poison` > 0
3. Execution failure rate > 5%
4. P95 execution time > 10s (cold start indicator)

## Idempotency

Non-idempotent operations are guarded by `IIdempotencyStore` (Azure Table Storage):

| Service | Key Pattern | Guarded Operation |
|---------|-------------|-------------------|
| LeadCommunicatorService | `lead:{agentId}-{leadId}:email-send` | Gmail to lead |
| AgentNotifierService | `lead:{agentId}-{leadId}:agent-notify` | WhatsApp/Gmail to agent |
| WelcomeNotificationService | `activation:{agentId}:welcome-notification` | Welcome message |

Content cache (CMA: 24h TTL, HomeSearch: 1h TTL) avoids duplicate API calls for same address.

## Maintainability

### Key Design Decisions

1. **Activity wrappers are thin** (~10-15 lines). Resolve service from DI, call it, return result. Business logic stays in original projects.
2. **Orchestrators are replay-safe**: no I/O, no DateTime.UtcNow, no Guid.NewGuid(). All logs guarded by `!ctx.IsReplaying`.
3. **Same architecture**: Domain to Clients hierarchy unchanged. Architecture tests enforce this.
4. **Explicit serialization DTOs** with `[JsonPropertyName]` and round-trip tests for activity boundaries.

### Adding a New Activity

1. Create service/worker in appropriate project
2. Create thin wrapper in `RealEstateStar.Functions/{Pipeline}/Activities/`
3. Add name to `ActivityNames.cs`
4. Call from orchestrator via `ctx.CallActivityAsync`
5. Add tests + register in `Program.cs`

### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| DI exception in activity | Service not registered | Add to `Program.cs` |
| Infinite replay | Non-deterministic orchestrator code | Remove DateTime/Guid/I/O |
| Duplicate emails | Idempotency not configured | Check AzureStorage + table |
| Messages not processed | App scaled to zero or stopped | Check Azure Portal |
| Health check Degraded | HealthUrl not set or app down | Verify config |

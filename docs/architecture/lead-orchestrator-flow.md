# Lead Orchestrator Flow

The LeadOrchestratorFunction coordinates the full lead pipeline as a Durable Function orchestration: scoring, activity dispatch, parallel CMA + Home Search, PDF generation, email drafting, and agent notification.

```mermaid
flowchart TD
    Submit["Lead Submitted<br/>via API endpoint"] --> Queue["Azure Queue<br/>lead-requests"]
    Queue --> Start["StartLeadProcessingFunction<br/>[QueueTrigger]<br/>starts Durable Orchestration"]
    Start --> Orch["LeadOrchestratorFunction<br/>(Durable Orchestrator)<br/>Instance ID: lead-{agentId}-{leadId}"]

    Orch --> A1["Activity: LoadAgentConfig"]
    A1 --> A2["Activity: ScoreLead"]
    A2 --> A3["Activity: CheckContentCache"]
    A3 --> Parallel["Parallel dispatch<br/>Task.WhenAll"]

    Parallel -->|"individual try/catch"| CMA["Activity: CMA<br/>(seller / both)"]
    Parallel -->|"individual try/catch"| HS["Activity: HomeSearch<br/>(buyer / both)"]

    CMA --> WhenAll["Await Task.WhenAll<br/>partial completion preserved"]
    HS --> WhenAll

    WhenAll -->|"CMA succeeded"| PDF["Activity: GeneratePdf<br/>Lead.Locale → localized headers + labels"]
    WhenAll -->|"CMA failed"| SkipPdf["Skip PDF<br/>pipeline continues"]

    PDF --> A6["Activity: DraftLeadEmail"]
    SkipPdf --> A6

    A6 --> A7["Activity: SendLeadEmail<br/>(idempotency guarded)<br/>Lead.Locale → per-language voice skill<br/>→ localized email template"]
    A7 --> A8["Activity: NotifyAgent<br/>(idempotency guarded)"]
    A8 --> A9["Activity: PersistLeadResults"]
    A9 --> A10["Activity: UpdateContentCache"]
    A10 --> Done["Orchestration Complete"]
```

## Key Design Properties

| Property | Detail |
|----------|--------|
| **Instance ID** | `lead-{agentId}-{leadId}` — deterministic, prevents duplicate orchestrations |
| **Partial completion** | CMA and HomeSearch run in parallel with individual `try/catch` — one failure does not abort the pipeline |
| **Idempotency** | `SendLeadEmail` and `NotifyAgent` are guarded against duplicate sends on replay |
| **Checkpoint/resume** | Handled automatically by the Durable Functions execution history (stored in Azure Table Storage) |
| **Retry** | Handled by DF `RetryPolicy` (maxAttempts: 4, 30s backoff, 2x coefficient) |
| **Locale flow** | `Lead.Locale` (from form submission) flows to `DraftLeadEmail` (loads per-language voice skill via `AgentContext.GetSkill`), `GeneratePdf` (localized CMA headers/labels), and `SendLeadEmail` (localized email template). Agent notification is always English. |

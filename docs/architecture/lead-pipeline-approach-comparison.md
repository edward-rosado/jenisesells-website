# Lead Pipeline — Approach A vs C

## Approach A: Rewrite Worker

Everything runs inside one `LeadProcessingWorker`. CMA and HomeSearch are inline steps.

```mermaid
flowchart TB
    A1[Lead Received] --> A2[Score - pure logic]
    A2 --> A3{Seller? Buyer? Both?}
    A3 -->|Seller| A4[CMA Step - inline]
    A3 -->|Buyer| A5[HomeSearch Step - inline]
    A3 -->|Both| A4
    A3 -->|Both| A5
    A4 --> A6[Email Lead + WhatsApp Agent]
    A5 --> A6

    style A1 fill:#FFF3E0,stroke:#E65100
    style A6 fill:#FFF3E0,stroke:#E65100
```

**Pros:** Simple, fewer moving parts, fast to build.
**Cons:** Tightly coupled. Moving to Azure Functions later = full rewrite.

---

## Approach C: Orchestrator Pattern (Recommended)

An orchestrator dispatches CMA/HomeSearch as independent workers, collects results, then triggers notifications.

```mermaid
flowchart TB
    C1[Lead Received] --> C2[Score - pure logic]
    C2 --> C3[Orchestrator]
    C3 -->|dispatch| C4[CMA Worker]
    C3 -->|dispatch| C5[HomeSearch Worker]
    C4 -->|result| C6[Orchestrator Collects Results]
    C5 -->|result| C6
    C6 --> C7[Email Lead + WhatsApp Agent]

    style C3 fill:#E8F5E9,stroke:#2E7D32
    style C6 fill:#E8F5E9,stroke:#2E7D32
    style C7 fill:#E8F5E9,stroke:#2E7D32
```

**Pros:** Workers are decoupled. Maps 1:1 to Azure Functions later. No second rewrite.
**Cons:** More upfront work — needs a completion/callback mechanism.

---

## Future: Azure Functions Migration

Approach C maps directly to Azure Durable Functions with no architectural changes.

```mermaid
flowchart TB
    F1[Queue Trigger] --> F2[Durable Orchestrator]
    F2 -->|activity| F3[CMA Function]
    F2 -->|activity| F4[HomeSearch Function]
    F3 -->|result| F5[Orchestrator Collects]
    F4 -->|result| F5
    F5 --> F6[Email + WhatsApp]

    style F2 fill:#E3F2FD,stroke:#1565C0
    style F5 fill:#E3F2FD,stroke:#1565C0
    style F6 fill:#E3F2FD,stroke:#1565C0
```

**Key insight:** In Approach A, the lead worker *is* the pipeline. In Approach C, the orchestrator *coordinates* independent workers. That separation is what makes the Azure Functions migration trivial.

---

## Summary

| | Approach A | Approach C |
|---|---|---|
| Complexity | Low | Medium |
| CMA/HomeSearch coupling | Inline steps | Independent workers |
| Azure Functions migration | Full rewrite | 1:1 mapping |
| Notification trigger | After last step | After orchestrator collects |
| Recommended | No | **Yes** |

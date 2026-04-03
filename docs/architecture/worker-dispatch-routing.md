# Worker Dispatch Routing

The orchestrator dispatches CMA and HomeSearch activities in parallel via `ctx.CallActivityAsync`, collects results with `Task.WhenAll`, then dispatches PDF generation if CMA succeeded.

```mermaid
flowchart LR
    Orch["LeadOrchestratorFunction"]

    Orch -->|"seller lead"| CmaAct["RunCmaActivity\npure compute"]
    Orch -->|"buyer lead"| HsAct["RunHomeSearchActivity\npure compute"]
    Orch -->|"both"| CmaAct
    Orch -->|"both"| HsAct

    CmaAct -->|"ActivityResult"| WhenAll["Task.WhenAll\nindividual try/catch"]
    HsAct -->|"ActivityResult"| WhenAll

    WhenAll -->|"CMA succeeded"| PdfAct["GeneratePdfActivity\nQuestPDF"]
    WhenAll -->|"CMA failed"| Skip["Skip PDF\nproceed without"]

    PdfAct -->|"writes PDF"| Storage["Azure Blob Storage"]
    PdfAct -->|"returns path"| Orch

    Skip --> Orch

    Orch --> Email["DraftEmailActivity\nClaude-drafted"]
    Orch --> Agent["NotifyAgentActivity\nWhatsApp + email"]
```

# Project Architecture

## Dependency Graph

```mermaid
graph TD
    subgraph "Composition Root"
        Api["<b>Api</b><br/>Endpoints, DI, Middleware<br/>Health Checks<br/><i>Refs: everything</i>"]
    end

    subgraph "Business Logic Layer"
        Workers["<b>Workers</b><br/>Channel-based BackgroundServices<br/>Pure compute — NO storage<br/><i>Refs: Domain + Workers.Shared</i>"]
        Services["<b>Services</b><br/>Sync calls — orchestrator waits<br/>Persist failure/fallback via DataServices<br/><i>Refs: Domain + DataServices + Workers.Shared</i>"]
        Activities["<b>Activities</b><br/>Compute + persist via DataServices<br/>Orchestrator may or may not wait<br/><i>Refs: Domain + DataServices</i>"]
    end

    subgraph "External Integration"
        Clients["<b>Clients</b><br/>External API wrappers<br/>Anthropic, Gmail, GDrive,<br/>RentCast, WhatsApp, Stripe, Azure<br/><i>Refs: Domain only</i>"]
    end

    subgraph "Storage Layer"
        DataServices["<b>DataServices</b><br/>Storage routing — WHERE to store<br/>*DataService naming<br/><i>Refs: Domain + Data</i>"]
        Data["<b>Data</b><br/>Raw I/O providers — HOW to store<br/>*Provider / *Store naming<br/><i>Refs: Domain only</i>"]
    end

    subgraph "Contracts"
        Domain["<b>Domain</b><br/>Interfaces, Models, Records, Enums<br/>ZERO implementations<br/><i>Refs: NOTHING</i>"]
    end

    Api --> Workers
    Api --> Services
    Api --> Activities
    Api --> Clients
    Api --> DataServices
    Api --> Data
    Api --> Domain

    Workers --> Domain
    Services --> Domain
    Services --> DataServices
    Activities --> Domain
    Activities --> DataServices
    Clients --> Domain
    DataServices --> Domain
    DataServices --> Data
    Data --> Domain

    Workers -.->|"dispatches via channel"| Workers
    Workers -.->|"calls"| Services
    Workers -.->|"calls"| Activities

    style Api fill:#4a90d9,color:white
    style Workers fill:#e6a23c,color:white
    style Services fill:#67c23a,color:white
    style Activities fill:#f56c6c,color:white
    style Clients fill:#909399,color:white
    style DataServices fill:#b37feb,color:white
    style Data fill:#73d13d,color:black
    style Domain fill:#36cfc9,color:black
```

## Lead Pipeline Flow

```mermaid
graph LR
    subgraph "Lead Pipeline Flow"
        Endpoint["SubmitLeadEndpoint"] --> OrcChannel["LeadOrchestratorChannel"]
        OrcChannel --> Orchestrator["LeadOrchestrator"]

        Orchestrator -->|"score"| Scorer["LeadScorer"]
        Orchestrator -->|"dispatch"| CMA["CMA Worker"]
        Orchestrator -->|"dispatch"| HS["HomeSearch Worker"]

        CMA -->|"TCS result"| Orchestrator
        HS -->|"TCS result"| Orchestrator

        Orchestrator -->|"compute + persist"| PdfAct["PdfActivity"]
        PdfAct --> PdfDS["PdfDataService"]
        PdfDS --> Storage["Data Provider"]

        Orchestrator -->|"wait for result"| LeadComm["LeadCommunicatorService"]
        LeadComm -->|"persist failure"| CommDS["LeadDataService"]

        Orchestrator -->|"wait for result"| AgentNot["AgentNotifierService"]
        AgentNot -->|"persist failure"| NotifDS["LeadDataService"]

        Orchestrator -->|"final persist"| PersistAct["PersistActivity"]
        PersistAct --> LeadDS["LeadDataService"]
        LeadDS --> Storage
    end

    style Orchestrator fill:#e6a23c,color:white
    style CMA fill:#e6a23c,color:white
    style HS fill:#e6a23c,color:white
    style PdfAct fill:#f56c6c,color:white
    style PersistAct fill:#f56c6c,color:white
    style LeadComm fill:#67c23a,color:white
    style AgentNot fill:#67c23a,color:white
    style PdfDS fill:#b37feb,color:white
    style CommDS fill:#b37feb,color:white
    style NotifDS fill:#b37feb,color:white
    style LeadDS fill:#b37feb,color:white
    style Storage fill:#73d13d,color:black
```

## Layer Rules

| Layer | Purpose | Naming | Allowed Deps | Forbidden |
|-------|---------|--------|-------------|-----------|
| **Api** | Composition root | `*Endpoint`, `*Middleware`, `*HealthCheck` | Everything | `*Service`, `*Worker`, `*Activity`, `BackgroundService` |
| **Workers** | Channel-based compute | `*Worker`, `*Channel`, `*Scorer`, `*Orchestrator` | Domain, Workers.Shared | Data, DataServices, Clients, Services |
| **Services** | Sync calls, orchestrator waits | `*Service` | Domain, DataServices, Workers.Shared | Data, Clients, Workers |
| **Activities** | Compute + persist | `*Activity` | Domain, DataServices | Data, Clients, Workers, Services |
| **Clients** | External API wrappers | `*Client`, `*Sender`, `*Refresher` | Domain | Everything else |
| **DataServices** | Storage routing (WHERE) | `*DataService`, `*Decorator` | Domain, Data | Clients, Workers, Services, Activities |
| **Data** | Raw I/O (HOW) | `*Provider`, `*Store` | Domain | Everything else |
| **Domain** | Pure contracts | `I*` (interfaces), records, enums | NOTHING | Implementations, Diagnostics, Renderers |

## Architecture Tests (72 passing)

All rules enforced by `RealEstateStar.Architecture.Tests`:
- `DependencyTests` — project reference constraints
- `NamingConventionTests` — class suffix enforcement per layer
- `ProjectTaxonomyTests` — cross-cutting layer boundary rules
- `ApiCompositionRootTests` — Api stays thin
- `LayerTests` — NetArchTest type-level rules

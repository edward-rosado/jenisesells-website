# Project Architecture

## Dependency Graph

```mermaid
graph TD
    subgraph "Composition Root"
        Api["<b>Api</b><br/>Endpoints, DI, Middleware<br/>Health Checks<br/><i>Refs: everything</i>"]
    end

    subgraph "Pipeline Layer"
        Orchestrator["<b>Orchestrator</b><br/>Top-level coordinator<br/>Owns sub-Workers<br/>Calls Activities + Services<br/><i>Refs: Domain + Workers + Activities + Services</i>"]
        Workers["<b>Workers (sub)</b><br/>Channel-based pure compute<br/>Called BY Orchestrator<br/>Do NOT know about Orchestrator<br/><i>Refs: Domain + Workers.Shared + Clients</i>"]
    end

    subgraph "Execution Layer"
        Activities["<b>Activities</b><br/>Compute + persist via DataServices<br/>Can call Services<br/>Launched by Orchestrator ONLY<br/><i>Refs: Domain + DataServices + Services</i>"]
        Services["<b>Services</b><br/>Sync calls — orchestrator waits<br/>Call Clients for external comms<br/>Persist failure/fallback via DataServices<br/>CANNOT call Activities<br/><i>Refs: Domain + DataServices + Clients</i>"]
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

    Api --> Orchestrator
    Api --> Workers
    Api --> Services
    Api --> Activities
    Api --> Clients
    Api --> DataServices
    Api --> Data
    Api --> Domain

    Orchestrator -->|"dispatches"| Workers
    Orchestrator -->|"calls"| Activities
    Orchestrator -->|"calls"| Services
    Orchestrator --> Domain

    Workers --> Domain
    Workers --> Clients

    Activities -->|"can call"| Services
    Activities --> DataServices
    Activities --> Domain

    Services --> Clients
    Services --> DataServices
    Services --> Domain

    Clients --> Domain
    DataServices --> Domain
    DataServices --> Data
    Data --> Domain

    style Api fill:#4a90d9,color:white
    style Orchestrator fill:#d4380d,color:white
    style Workers fill:#e6a23c,color:white
    style Services fill:#67c23a,color:white
    style Activities fill:#f56c6c,color:white
    style Clients fill:#909399,color:white
    style DataServices fill:#b37feb,color:white
    style Data fill:#73d13d,color:black
    style Domain fill:#36cfc9,color:black
```

## Call Direction Rules

```mermaid
graph LR
    subgraph "WHO can call WHOM"
        O["Orchestrator"] -->|"dispatch via channel"| W["Sub-Workers"]
        O -->|"call directly"| A["Activities"]
        O -->|"call directly"| S["Services"]
        A -->|"can call"| S
        A -->|"persist via"| DS["DataServices"]
        S -->|"calls for external comms"| C["Clients"]
        S -->|"persist failure via"| DS
        DS -->|"routes to"| D["Data Providers"]
        W -->|"calls"| C
    end

    subgraph "WHO cannot call WHOM"
        S -.->|"CANNOT"| A
        S -.->|"CANNOT"| W
        W -.->|"CANNOT"| O
        A -.->|"CANNOT"| W
        A -.->|"CANNOT"| O
        C -.->|"CANNOT"| S
        C -.->|"CANNOT"| A
    end

    style O fill:#d4380d,color:white
    style W fill:#e6a23c,color:white
    style A fill:#f56c6c,color:white
    style S fill:#67c23a,color:white
    style DS fill:#b37feb,color:white
    style D fill:#73d13d,color:black
    style C fill:#909399,color:white
```

## Lead Pipeline Flow

```mermaid
graph LR
    subgraph "Lead Pipeline"
        Endpoint["SubmitLeadEndpoint"] --> OrcChannel["LeadOrchestratorChannel"]
        OrcChannel --> Orchestrator["LeadOrchestrator<br/>(Orchestrator)"]

        Orchestrator -->|"score"| Scorer["LeadScorer"]

        Orchestrator -->|"dispatch via channel"| CMA["CMA Worker<br/>(Sub-Worker)"]
        Orchestrator -->|"dispatch via channel"| HS["HomeSearch Worker<br/>(Sub-Worker)"]

        CMA -->|"calls"| RentCast["RentCast Client"]
        CMA -->|"calls"| Claude1["Anthropic Client"]
        HS -->|"calls"| Scraper["Scraper Client"]
        HS -->|"calls"| Claude2["Anthropic Client"]

        CMA -->|"TCS result"| Orchestrator
        HS -->|"TCS result"| Orchestrator

        Orchestrator -->|"call Activity"| PdfAct["PdfActivity"]
        PdfAct --> PdfDS["PdfDataService"]
        PdfDS --> Storage["Data Provider"]

        Orchestrator -->|"call Service"| LeadComm["LeadCommunicatorService"]
        LeadComm -->|"calls"| Claude3["Anthropic Client"]
        LeadComm -->|"calls"| Gmail1["Gmail Client"]
        LeadComm -->|"persist failure"| CommDS["LeadDataService"]

        Orchestrator -->|"call Service"| AgentNot["AgentNotifierService"]
        AgentNot -->|"calls"| WA["WhatsApp Client"]
        AgentNot -->|"calls"| Gmail2["Gmail Client"]
        AgentNot -->|"persist failure"| NotifDS["LeadDataService"]

        Orchestrator -->|"call Activity"| PersistAct["PersistActivity"]
        PersistAct --> LeadDS["LeadDataService"]
        LeadDS --> Storage
    end

    style Orchestrator fill:#d4380d,color:white
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
    style RentCast fill:#909399,color:white
    style Claude1 fill:#909399,color:white
    style Claude2 fill:#909399,color:white
    style Claude3 fill:#909399,color:white
    style Scraper fill:#909399,color:white
    style Gmail1 fill:#909399,color:white
    style Gmail2 fill:#909399,color:white
    style WA fill:#909399,color:white
```

## Orchestrator vs Sub-Worker

```mermaid
graph TD
    subgraph "Orchestrator (top-level coordinator)"
        O["LeadOrchestrator"]
        O_desc["• Manages LeadPipelineContext<br/>• Coordinates multi-step pipeline<br/>• Calls Activities + Services<br/>• Dispatches sub-Workers via channels<br/>• Handles timeouts + partial results<br/>• One instance per lead"]
    end

    subgraph "Sub-Workers (pure compute)"
        W1["CMA Worker"]
        W1_desc["• Receives work via channel<br/>• Does NOT know about Orchestrator<br/>• Calls Clients for external data<br/>• Returns result via TCS<br/>• Retries internally via PipelineWorker<br/>• No storage, no persistence"]

        W2["HomeSearch Worker"]
        W2_desc["• Same pattern as CMA<br/>• Pure compute pipeline<br/>• Channel in, TCS result out"]
    end

    O -->|"dispatches to channel"| W1
    O -->|"dispatches to channel"| W2
    W1 -->|"TCS result back"| O
    W2 -->|"TCS result back"| O

    style O fill:#d4380d,color:white
    style W1 fill:#e6a23c,color:white
    style W2 fill:#e6a23c,color:white
```

## Layer Rules

| Layer | Purpose | Naming | Can Call | Cannot Call |
|-------|---------|--------|---------|-------------|
| **Api** | Composition root | `*Endpoint`, `*Middleware`, `*HealthCheck` | Everything (DI wiring) | — |
| **Orchestrator** | Multi-step coordinator | `*Orchestrator` | Sub-Workers, Activities, Services | — |
| **Sub-Workers** | Pure compute pipelines | `*Worker`, `*Channel` | Clients (external APIs) | Orchestrator, Activities, Services, DataServices |
| **Activities** | Compute + persist | `*Activity` | Services, DataServices | Workers, Orchestrator |
| **Services** | Sync business logic | `*Service` | Clients, DataServices | Activities, Workers, Orchestrator |
| **Clients** | External API wrappers | `*Client`, `*Sender` | Domain only | Everything else |
| **DataServices** | Storage routing (WHERE) | `*DataService` | Data providers | Clients, Workers, Services, Activities |
| **Data** | Raw I/O (HOW) | `*Provider`, `*Store` | Domain only | Everything else |
| **Domain** | Pure contracts | `I*`, records, enums | NOTHING | Implementations |

## Architecture Tests

All rules enforced by `RealEstateStar.Architecture.Tests`:

| Test Class | What it enforces |
|------------|-----------------|
| `DependencyTests` | Project reference constraints (who can reference whom) |
| `NamingConventionTests` | Class suffix enforcement per layer (*Service, *Activity, etc.) |
| `ProjectTaxonomyTests` | Cross-cutting layer boundary rules |
| `ApiCompositionRootTests` | Api stays thin (no *Service, no BackgroundService) |
| `LayerTests` | NetArchTest type-level rules |

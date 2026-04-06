# Activation Pipeline MVP Redesign

Architecture diagrams for the MVP tier activation pipeline redesign.

## MVP Tier Pipeline Flow

```mermaid
flowchart TD
    subgraph Phase1["Phase 1: Gather"]
        E[Email Fetch<br/>inbox + sent]
        D[Drive Index<br/>40 files, most-recent-first<br/>regex text extraction]
        AD[Agent Discovery<br/>own website first<br/>Zillow/Realtor.com fallback]
    end

    subgraph Phase2["Phase 2: Synthesize (MVP — 8 workers)"]
        V[VoiceExtraction<br/>Opus 4.6]
        PE[Personality<br/>Sonnet 4.6]
        CO[Coaching<br/>Sonnet 4.6]
        CM[ComplianceAnalysis<br/>Sonnet 4.6]
        CS[CmaStyle<br/>Sonnet 4.6]
        PI[PipelineAnalysis<br/>Sonnet 4.6<br/>→ pipeline.json]
        BD[BrandingDiscovery<br/>C# ScoreTemplate]
        WS[WebsiteStyle<br/>Sonnet 4.6]
    end

    subgraph Phase3["Phase 3: Persist"]
        PP[PersistProfile<br/>skill files + pipeline.json]
        BM{BrandMerge?}
    end

    subgraph Phase4["Phase 4: Notify"]
        WE[Welcome Email<br/>Sonnet 4.6<br/>4-section personalized]
    end

    E --> Phase2
    D --> Phase2
    AD --> Phase2
    Phase2 --> PP
    PP --> BM
    BM -->|multi-agent| MERGE[BrandMerge<br/>Sonnet 4.6]
    BM -->|single-agent| SKIP[Skip]
    MERGE --> WE
    SKIP --> WE

    style BD fill:#2E7D32,color:white
    style PI fill:#C8A951,color:black
    style V fill:#7B68EE,color:white
    style WE fill:#4A90D9,color:white
```

## Tier-Conditional Worker Dispatch

```mermaid
flowchart LR
    subgraph MVP["MVP Tier — 8 workers, 4 batches"]
        B1[Batch 1<br/>Voice + Personality]
        B2[Batch 2<br/>Branding + WebsiteStyle]
        B3[Batch 3<br/>CmaStyle + Pipeline]
        B4[Batch 4<br/>Coaching + Compliance]
        B1 --> B2 --> B3 --> B4
    end

    subgraph FUTURE["FUTURE Tier — +4 workers, +2 batches"]
        B5[Batch 5<br/>BrandExtraction + BrandVoice]
        B6[Batch 6<br/>MarketingStyle + FeeStructure]
        B4 --> B5 --> B6
    end

    style FUTURE fill:#f5f5f5,stroke:#ccc,stroke-dasharray: 5 5
    style MVP fill:#e8f5e9
```

## Pipeline Query Fast Path (WhatsApp)

```mermaid
sequenceDiagram
    participant Agent as Agent (WhatsApp)
    participant WH as WhatsApp Handler
    participant PQS as PipelineQueryService<br/>(pure C#)
    participant Blob as pipeline.json<br/>(Blob Storage)
    participant Claude as Claude API

    Agent->>WH: "what's happening with Spruce St?"
    WH->>Blob: Deserialize pipeline.json
    Blob-->>WH: AgentPipeline
    WH->>PQS: TryAnswer(pipeline, question)
    
    alt Match found (~95% of queries)
        PQS-->>WH: "Natasha's rental at 2538 Spruce St..."
        WH-->>Agent: Formatted answer (0 Claude cost, <100ms)
    else No match (~5% of queries)
        PQS-->>WH: null
        WH->>Claude: pipeline JSON + question
        Claude-->>WH: Natural language answer
        WH-->>Agent: Claude response (~$0.01-0.05)
    end
```

## Agent Discovery Priority

```mermaid
flowchart TD
    A[Scan DiscoveredUrls<br/>from DriveIndex] --> B{Found agent's<br/>own website?}
    B -->|yes| C[Scrape agent's site<br/>Source = OwnWebsite]
    B -->|no| D[Scan email signatures]
    D --> E{Found website URL?}
    E -->|yes| C
    E -->|no| F[Zillow / Realtor.com<br/>Source = ThirdParty]
    C --> G[BrandingDiscovery<br/>uses real brand]
    F --> G

    style C fill:#2E7D32,color:white
    style F fill:#C8A951,color:black
```

## Drive Index C# Optimizations

```mermaid
flowchart TD
    subgraph DriveIndex["DriveIndex Worker"]
        FILES[40 files<br/>most-recent-first] --> SPLIT{File type?}
        SPLIT -->|PDF| PRIORITY{Email<br/>attachment?}
        PRIORITY -->|yes| CLAUDE_FIRST[Claude Vision<br/>priority extraction]
        PRIORITY -->|no| CLAUDE_LATER[Claude Vision<br/>if budget remains]
        SPLIT -->|Text| REGEX[C# Regex<br/>emails, phones, addresses]
        REGEX --> XREF[Cross-reference<br/>with email corpus names]
    end

    style REGEX fill:#2E7D32,color:white
    style XREF fill:#2E7D32,color:white
    style CLAUDE_FIRST fill:#7B68EE,color:white
    style CLAUDE_LATER fill:#7B68EE,color:white
```

## Cost Comparison

```mermaid
pie title Cost per Activation
    "VoiceExtraction (Opus)" : 1.43
    "7 Sonnet Workers" : 2.03
    "Welcome Email (Sonnet)" : 0.09
    "BrandingDiscovery (C#)" : 0
    "Template Scoring (C#)" : 0
```

# Agent Activation Pipeline

Triggered when an agent completes Google OAuth via the OAuth Authorization Link flow.
The pipeline runs as a `BackgroundService` (`ActivationOrchestrator`) fed by a durable Azure Queue (`activation-requests`).

---

## 6-Phase Overview

```mermaid
flowchart TD
    A["OAuth Callback<br/>(AuthorizeLinkCallbackEndpoint)"] --> B["Azure Queue Storage<br/>activation-requests"]
    B --> C["ActivationOrchestrator<br/>(BackgroundService — polls queue)"]

    C --> P0["Phase 0: Skip-if-Complete<br/>All required files exist? → skip + resend welcome"]

    P0 -->|not complete| P1

    subgraph P1["Phase 1: Gather"]
        direction TB
        E1["AgentEmailFetchWorker<br/>100 sent + 100 inbox emails"]
        E2["DriveIndexWorker<br/>List files, identify RE docs,<br/>download PDFs, Claude Vision extraction"]
        E3["AgentDiscoveryWorker<br/>Web profiles, headshot, languages"]
        E1 & E2 -->|parallel| E3_note["Email + Drive run in parallel<br/>Discovery runs after (needs signature)"]
    end

    subgraph P2["Phase 2: Synthesize (12 workers in parallel)"]
        direction TB
        W1["VoiceExtraction (Opus 4.6)"]
        W2["Personality (Sonnet 4.6)"]
        W3["BrandingDiscovery (Sonnet 4.6)"]
        W4["CmaStyle (Sonnet 4.6)"]
        W5["MarketingStyle (Sonnet 4.6)"]
        W6["WebsiteStyle (Sonnet 4.6)"]
        W7["PipelineAnalysis (Sonnet 4.6)"]
        W8["Coaching (Sonnet 4.6)"]
        W9["BrandExtraction (Sonnet 4.6)"]
        W10["BrandVoice (Sonnet 4.6)"]
        W11["ComplianceAnalysis (Sonnet 4.6)"]
        W12["FeeStructure (Sonnet 4.6)"]
    end

    subgraph P25["Phase 2.5: Contact Detection (NEW — reusable Activity)"]
        CC["ContactDetectionActivity<br/>PDF extraction + lead generator parsing<br/>+ email scanning + dedup + classification"]
        CC2["Also callable by future<br/>Gmail Inbox Check-in Job"]
    end

    subgraph P3["Phase 3: Persist + Brand Merge"]
        PA["AgentProfilePersistActivity<br/>Fan-out writes: Drive + Blob + Config"]
        CI["ContactImportPersistActivity (NEW)<br/>Folder structure + file copy + LeadStore"]
        BM["BrandMergeActivity<br/>Brand Profile.md + Brand Voice.md"]
    end

    subgraph P4["Phase 4: Welcome Notification"]
        WN["WelcomeNotificationService<br/>Claude Opus 4.6 draft → Gmail send<br/>WhatsApp fallback"]
    end

    P1 --> P2 --> P25 --> P3 --> P4
    P4 --> DONE["[ACTV-003] Pipeline Complete<br/>Queue message deleted"]

    style P1 fill:#e3f2fd
    style P2 fill:#f3e5f5
    style P25 fill:#fff3e0
    style P3 fill:#e8f5e9
    style P4 fill:#fce4ec
```

---

## Durable Queue Architecture

```mermaid
flowchart LR
    subgraph Producer
        A[AuthorizeLinkCallbackEndpoint]
    end

    subgraph Queue["Azure Queue Storage"]
        Q[activation-requests<br/>JSON messages<br/>5 min visibility timeout]
    end

    subgraph Consumer
        O[ActivationOrchestrator<br/>BackgroundService<br/>Polls every 3s]
    end

    A -->|EnqueueAsync| Q
    Q -->|DequeueAsync| O
    O -->|success| D[CompleteAsync<br/>delete message]
    O -->|failure| R[Message becomes visible<br/>after timeout → auto-retry]

    style Q fill:#fff9c4
```

**Resilience:**
- Messages survive container restarts (durable storage, not in-memory)
- Failed activations auto-retry after visibility timeout (5 min)
- Poison messages move to `activation-requests-poison` after 5 failures
- Queue health check on `/health/ready`
- Full observability: `[QUEUE-001]` enqueue, `[QUEUE-002]` dequeue, `[QUEUE-003]` complete

---

## Data Flow

```mermaid
flowchart LR
    subgraph Phase1Outputs["Phase 1 Outputs"]
        EC["EmailCorpus<br/>SentEmails, InboxEmails,<br/>Signature, Attachments"]
        DI["ClassifiedDriveIndex<br/>Files, Contents, URLs,<br/>DocumentExtractions (NEW)"]
        AD["AgentDiscovery<br/>Profiles, Reviews, Websites,<br/>Headshot, Logo, Languages"]
    end

    subgraph Phase2Workers["Phase 2 Workers"]
        VE["VoiceExtraction"]
        PE["Personality"]
        BD["BrandingDiscovery"]
        CS["CmaStyle"]
        MS["MarketingStyle"]
        WS["WebsiteStyle"]
        PA["PipelineAnalysis"]
        CW["Coaching"]
        BE["BrandExtraction"]
        BV["BrandVoice"]
        CA["ComplianceAnalysis"]
        FS["FeeStructure"]
    end

    EC --> VE & PE & MS & PA & CW & BE & BV & CA & FS
    DI --> VE & PE & CS & MS & PA & CW & BE & BV & CA & FS
    AD --> PE & BD & WS & CW & BE & BV & CA & FS

    subgraph Phase25["Phase 2.5"]
        CC["ContactClassifier"]
    end

    DI --> CC
    EC --> CC
```

---

## Storage Fan-Out Architecture

```mermaid
flowchart TD
    subgraph PersistActivity["AgentProfilePersistActivity"]
        PA[Writes skill files + config]
    end

    PA --> F["IFileStorageProviderFactory<br/>CreateForAgent(accountId, agentId)"]

    F --> FO["FanOutStorageProvider<br/>(per-agent instance)"]

    FO --> T1["Agent Drive Tier<br/>IGDriveClient(accountId, agentId)<br/>Nested folders via ResolveFolderPathAsync"]
    FO --> T2["Account Drive Tier<br/>IGDriveClient(accountId, __account__)<br/>Skipped when accountId == agentId"]
    FO --> T3["Platform Blob Tier<br/>IDocumentStorageProvider<br/>Azure Blob Storage"]

    T1 -->|success| OK1["Files in agent's Google Drive"]
    T1 -->|failure| W1["[FANOUT-010] Warning logged<br/>best-effort, continues"]
    T2 -->|success| OK2["Files in brokerage Drive"]
    T2 -->|no token| W2["[GDRIVE-010] Skipped"]
    T3 -->|success| OK3["Files in Azure Blob"]

    style T1 fill:#c8e6c9
    style T3 fill:#bbdefb
    style T2 fill:#f5f5f5
```

---

## Google Drive Folder Structure

```mermaid
flowchart TD
    ROOT["real-estate-star/{agentId}/"] --> LEADS["1 - Leads/"]
    ROOT --> ACTIVE["2 - Active Clients/"]
    ROOT --> CONTRACT["3 - Under Contract/"]
    ROOT --> CLOSED["4 - Closed/"]
    ROOT --> INACTIVE["5 - Inactive/"]
    ROOT --> SKILLS["Skill Files<br/>Voice Skill.md, Personality Skill.md,<br/>Branding Kit.md, etc."]

    LEADS --> L1["{Client Name}/"]
    L1 --> L2["{Property Address}/"]
    L2 --> L3["Communications/"]

    ACTIVE --> A1["{Client Name}/"]
    A1 --> A2["Agreements/"]
    A1 --> A3["Documents Sent/"]
    A1 --> A4["Communications/"]

    CONTRACT --> C1["{Client Name}/"]
    C1 --> C2["{Address} Transaction/"]
    C2 --> C3["Contracts/"]
    C2 --> C4["Inspection/"]
    C2 --> C5["Appraisal/"]
    C2 --> C6["Communications/"]

    CLOSED --> D1["{Client Name}/"]
    D1 --> D2["Audit Log/"]
    D1 --> D3["Reports/"]
    D1 --> D4["Communications/"]

    INACTIVE --> I1["Dead Leads/"]
    INACTIVE --> I2["Expired Clients/"]

    style LEADS fill:#f5f5f5
    style ACTIVE fill:#bbdefb
    style CONTRACT fill:#fff9c4
    style CLOSED fill:#c8e6c9
    style INACTIVE fill:#ffcdd2
    style SKILLS fill:#e1bee7
```

---

## Skip-if-Complete Check (Phase 0)

Checks for the existence of all required files before running the full pipeline.
If all are present, the welcome notification is re-sent (idempotent) and the pipeline exits.

**Required per-agent files** (`real-estate-star/{agentId}/`):

| File | Phase |
|------|-------|
| `Voice Skill.md` | Phase 2 |
| `Personality Skill.md` | Phase 2 |
| `Marketing Style.md` | Phase 2 |
| `Sales Pipeline.md` | Phase 2 |
| `Coaching Report.md` | Phase 2 |
| `Agent Discovery.md` | Phase 1 |
| `Branding Kit.md` | Phase 2 |
| `Email Signature.md` | Phase 1 |
| `headshot.jpg` | Phase 1 |
| `Drive Index.md` | Phase 1 |

**Required per-account files** (`real-estate-star/{accountId}/`):

| File | Phase |
|------|-------|
| `Brand Profile.md` | Phase 3 |
| `Brand Voice.md` | Phase 3 |

---

## Checkpoint Strategy

After each phase, a checkpoint JSON is written to `real-estate-star/{agentId}/activation/`:

- `checkpoint-phase1-gather.json` — corpus hash + stats (email count, drive file count, websites found)
- `checkpoint-phase2-synthesis.json` — worker status map (`completed` / `skipped` per worker)

Checkpoints are **cleared before a fresh run** and **deleted after successful completion**.
They serve as observability artifacts for debugging failed activations in storage.

**Individual worker failures are non-fatal** — `RunSafeAsync` wraps each Phase 2 worker
so one failure doesn't abort the whole pipeline. The output for that worker is `null`.

---

## Resilience & Retry Strategy

```mermaid
flowchart TD
    A[Queue message received] --> B{Phase 1 succeeds?}
    B -->|yes| C[Checkpoint saved]
    C --> D{Phase 2 succeeds?}
    D -->|yes| E[Checkpoint saved]
    E --> F{Phase 2.5 succeeds?}
    F -->|yes| G{Phase 3 succeeds?}
    G -->|yes| H{Phase 4 succeeds?}
    H -->|yes| I["[QUEUE-003] Message deleted<br/>Pipeline complete"]

    B -->|no| J["Exception thrown<br/>Message stays in queue"]
    D -->|partial| E
    F -->|no| J
    G -->|no| J
    H -->|no| J

    J --> K["Visibility timeout expires (5 min)"]
    K --> L["Message becomes visible<br/>Orchestrator re-dequeues"]
    L --> M{Retry count < 5?}
    M -->|yes| N["Resume from last checkpoint"]
    M -->|no| O["Moved to poison queue<br/>activation-requests-poison"]

    style I fill:#c8e6c9
    style O fill:#ffcdd2
    style N fill:#fff9c4
```

**Retry behaviors:**
- **Phase 1 failure:** Full retry (no checkpoint yet)
- **Phase 2 partial failure:** Individual workers fail gracefully (`RunSafeAsync`), pipeline continues with null outputs. Only full phase failure triggers retry.
- **Phase 3 failure:** Retry re-runs persist. `WriteOrUpdateAsync` is idempotent — existing files are overwritten, not duplicated.
- **Phase 4 failure:** Welcome email has idempotency check (`Welcome Sent.md`). Safe to retry.
- **GDrive auth errors:** `WithRetryOnAuthErrorAsync` refreshes OAuth token and retries once per call.
- **Claude API errors:** Polly retry policy with exponential backoff (configured per `HttpClient`).

---

## Observability

```mermaid
flowchart LR
    subgraph App["RealEstateStar.Api"]
        AS1["ActivitySource:<br/>RealEstateStar.Activation"]
        AS2["ActivitySource:<br/>RealEstateStar.Queue"]
        AS3["ActivitySource:<br/>RealEstateStar.ContactImport"]
        M1["Meter: activation.*"]
        M2["Meter: queue.*"]
        M3["Meter: contacts.*"]
        L["Serilog structured logs<br/>[ACTV-0xx] [QUEUE-0xx]<br/>[CONTACT-0xx] [CLAUDE-0xx]"]
    end

    subgraph OTel["OpenTelemetry Pipeline"]
        EX["OTLP Exporter<br/>HTTP/Protobuf"]
    end

    subgraph Grafana["Grafana Cloud"]
        T["Tempo<br/>(traces)"]
        P["Mimir/Prometheus<br/>(metrics)"]
        LK["Loki<br/>(logs — not yet configured)"]
    end

    AS1 & AS2 & AS3 --> EX
    M1 & M2 & M3 --> EX
    EX --> T & P

    style Grafana fill:#f3e5f5
```

**Activation spans:**
- `activation.pipeline` — root span (tags: `accountId`, `agentId`, `outcome`)
- `activation.phase1.gather` — Phase 1 (tags: `outcome`, email count, file count)
- `activation.phase2.synthesize` — Phase 2 (tags: `outcome`, worker count)
- `activation.phase2_5.classify` — Phase 2.5 (tags: contacts found, stages)
- `activation.phase3.persist` — Phase 3 (tags: `outcome`, files written)
- `activation.phase4.notify` — Phase 4 (tags: `outcome`, channel used)

**Queue spans:**
- `queue.enqueue` / `queue.dequeue` / `queue.complete` (tags: `queue.name`, `message.id`)

**Contact import spans:**
- `contact-import.pdf-extract` — per PDF (tags: `file_id`, `document_type`)
- `contact-import.email-extract` — per batch
- `contact-import.classify` — classification pass
- `contact-import.persist` — folder creation + file copy

**Metrics:**
| Metric | Type | Dimensions |
|--------|------|------------|
| `activation.started` | Counter | accountId |
| `activation.completed` | Counter | accountId |
| `activation.failed` | Counter | accountId |
| `activation.duration` | Histogram | — |
| `queue.messages.enqueued` | Counter | queue.name |
| `queue.messages.completed` | Counter | queue.name |
| `queue.messages.failed` | Counter | queue.name |
| `queue.processing.duration` | Histogram | — |
| `contacts.imported` | Counter | stage |
| `pdfs.processed` | Counter | — |
| `claude.input_tokens` | Counter | pipeline, model |
| `claude.output_tokens` | Counter | pipeline, model |
| `claude.duration` | Histogram | pipeline, model |

**Error code prefixes:**
| Prefix | Component |
|--------|-----------|
| `ACTV-0xx` | Activation orchestrator |
| `ACTV-1xx` | Activation infrastructure |
| `QUEUE-0xx` | Queue operations |
| `CONTACT-0xx` | Contact import |
| `CLAUDE-0xx` | Claude API client |
| `GDRIVE-0xx` | Google Drive client |
| `PERSIST-AGENT-0xx` | Agent profile persist |
| `CFG-0xx` | Config generation |
| `WELCOME-0xx` | Welcome notification |
| `FANOUT-0xx` | Fan-out storage |
| `TOKEN-0xx` | Token store |
| `BLOB-0xx` | Blob storage |

---

## Model Selection

| Worker | Model | Rationale |
|--------|-------|-----------|
| VoiceExtraction | **Opus 4.6** | Deep communication pattern analysis — only worker that needs Opus |
| All other synthesis workers (11) | Sonnet 4.6 | Good quality at 5x lower cost than Opus |
| BrandMerge | Sonnet 4.6 | Merge/synthesis task |
| Welcome email | Opus 4.6 | Agent's first impression — quality matters |
| PDF extraction (Vision) | Sonnet 4.6 | Structured extraction from images |
| Email contact extraction | Sonnet 4.6 | Batch contact parsing |
| Lead generator parsing | Regex | No Claude needed — known formats |
| Profile scraping | Haiku 4.5 | Lightweight extraction |

**Prompt caching:** System prompts use `cache_control: {"type": "ephemeral"}` for 90% input token discount on subsequent calls within the same model.

---

## Performance Benchmarks (2026-03-31)

| Phase | Duration (personal Gmail) | Duration (real agent) |
|-------|--------------------------|----------------------|
| Phase 1: Gather | ~42s | ~90s (58 docs) |
| Phase 2: Synthesize | ~52s | ~60s |
| Phase 3: Persist + Brand Merge | ~55s | ~60s |
| Phase 4: Welcome | ~9s | ~9s |
| **Total** | **~2 min 39s** | **~3 min 39s** |

**Bottlenecks:**
1. Phase 1 Drive file reading — sequential, ~1-8s per file
2. Brand merge Claude call — single 47s call
3. Coaching worker — 54K input tokens (context trimming opportunity)

---

## Cost Per Activation

| Component | Personal Gmail | Real Agent |
|-----------|---------------|------------|
| Existing pipeline (12 workers + welcome) | $0.47 | $0.80-1.20 |
| Contact import (PDF + email extraction) | — | $0.90-4.50 |
| Container compute (~3-5 min) | $0.02 | $0.02 |
| **Total** | **$0.49** | **$1.72-5.72** |

---

## Project Structure

```
Workers/Activation/
  RealEstateStar.Workers.Activation.Orchestrator/        -- BackgroundService + pipeline coordination
  RealEstateStar.Workers.Activation.EmailFetch/          -- Phase 1: Gmail corpus fetch
  RealEstateStar.Workers.Activation.DriveIndex/          -- Phase 1: Drive indexing + PDF extraction
  RealEstateStar.Workers.Activation.AgentDiscovery/      -- Phase 1: web scraping + profile discovery
  RealEstateStar.Workers.Activation.VoiceExtraction/     -- Phase 2 (Opus 4.6)
  RealEstateStar.Workers.Activation.Personality/         -- Phase 2
  RealEstateStar.Workers.Activation.BrandingDiscovery/   -- Phase 2
  RealEstateStar.Workers.Activation.CmaStyle/            -- Phase 2
  RealEstateStar.Workers.Activation.MarketingStyle/      -- Phase 2
  RealEstateStar.Workers.Activation.WebsiteStyle/        -- Phase 2
  RealEstateStar.Workers.Activation.PipelineAnalysis/    -- Phase 2
  RealEstateStar.Workers.Activation.Coaching/            -- Phase 2
  RealEstateStar.Workers.Activation.BrandExtraction/     -- Phase 2
  RealEstateStar.Workers.Activation.BrandVoice/          -- Phase 2
  RealEstateStar.Workers.Activation.ComplianceAnalysis/  -- Phase 2
  RealEstateStar.Workers.Activation.FeeStructure/        -- Phase 2
Activities/Activation/
  RealEstateStar.Activities.Activation.PersistAgentProfile/   -- Phase 3
  RealEstateStar.Activities.Activation.BrandMerge/            -- Phase 3
  RealEstateStar.Activities.Activation.ContactImportPersist/  -- Phase 3 (NEW)
Activities/Leads/
  RealEstateStar.Activities.Lead.ContactDetection/            -- Phase 2.5 (NEW — reusable)
    ContactDetectionActivity.cs      -- orchestrates extraction + classification
    PdfContactExtractor.cs           -- Claude Vision extraction from PDF pages
    EmailContactExtractor.cs         -- lead generator regex + Claude Sonnet batch
    ContactClassifier.cs             -- dedup + stage classification
    LeadGeneratorPatterns.cs         -- known sender domains + parsing templates
Services/Activation/
  RealEstateStar.Services.AgentConfig/           -- Config generation
  RealEstateStar.Services.BrandMerge/            -- Brand merge logic
  RealEstateStar.Services.WelcomeNotification/   -- Phase 4
```

**Dependency rule**: All individual Activation workers depend only on `Domain` + `Workers.Shared`.
The Orchestrator additionally depends on all workers + activities.
Activities depend on Domain + may call Clients (via factory for per-agent context).

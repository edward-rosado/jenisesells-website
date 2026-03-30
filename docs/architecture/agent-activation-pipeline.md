# Agent Activation Pipeline

Triggered when an agent completes Google OAuth via the OAuth Authorization Link flow.
The pipeline runs as a `BackgroundService` (`ActivationOrchestrator`) fed by a `Channel<ActivationRequest>`.

---

## 5-Phase Overview

```
OAuth Callback
    │
    └─► Channel<ActivationRequest>
              │
              ▼
    ┌─────────────────────────────────────────────────────────┐
    │                  ActivationOrchestrator                 │
    │  (BackgroundService — reads channel, runs all phases)   │
    └─────────────────────────────────────────────────────────┘
              │
              ▼ Phase 0: Skip-if-complete check
              │  - All 12 required files in storage? → skip + send welcome (idempotent)
              │
              ▼ Phase 1: Gather (sequential)
              │  ┌─────────────────────────────────────────┐
              │  │  AgentEmailFetchWorker  (Gmail corpus)  │
              │  │  DriveIndexWorker       (GDrive index)  │  ← parallel
              │  └─────────────────────────────────────────┘
              │  └── AgentDiscoveryWorker  (web scraping + profile discovery)
              │
              ▼ Phase 2: Synthesize (12 workers in parallel)
              │  ┌──────────────────────────────────────────────────────────────┐
              │  │  VoiceExtractionWorker      → Voice Skill.md                │
              │  │  PersonalityWorker          → Personality Skill.md          │
              │  │  BrandingDiscoveryWorker    → Branding Kit.md               │
              │  │  CmaStyleWorker             → CMA Style Guide               │
              │  │  MarketingStyleWorker       → Marketing Style.md            │
              │  │  WebsiteStyleWorker         → Website Style Guide           │
              │  │  PipelineAnalysisWorker     → Sales Pipeline.md             │
              │  │  CoachingWorker             → Coaching Report.md            │
              │  │  BrandExtractionWorker      → brand signals (internal)      │
              │  │  BrandVoiceWorker           → brand voice signals           │
              │  │  ComplianceAnalysisWorker   → compliance analysis           │
              │  │  FeeStructureWorker         → fee structure analysis        │
              │  └──────────────────────────────────────────────────────────────┘
              │
              ▼ Phase 3: Persist + Brand Merge
              │  ┌──────────────────────────────────────────────────────────────┐
              │  │  AgentProfilePersistActivity                                 │
              │  │    - fan-out writes to real-estate-star/{agentId}/           │
              │  │    - creates leads/ and leads/consent/ subdirs               │
              │  │    - calls IAgentConfigService → account.json + content.json │
              │  │  BrandMergeActivity                                          │
              │  │    - merges Branding Kit + Voice Skill → Brand Profile.md   │
              │  │    - merges → Brand Voice.md (account-level)                │
              │  └──────────────────────────────────────────────────────────────┘
              │
              ▼ Phase 4: Welcome Notification
                 IWelcomeNotificationService
                   - WhatsApp (primary) or email fallback
                   - Idempotent (WelcomeSent flag)
```

---

## Skip-if-Complete Check (Phase 0)

Checks for the existence of all 12 required files before running the full pipeline.
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

## Data Flow

```
Phase 1 outputs                  Phase 2 inputs
─────────────────                ────────────────────────────────────
EmailCorpus                ──►   VoiceExtraction, Personality, Coaching
 └─ Signature (name/phone)       BrandExtraction, BrandVoice, Compliance
 └─ SentEmails                   FeeStructure, Marketing, Pipeline
 └─ InboxEmails
DriveIndex                 ──►   CmaStyle, Marketing, Pipeline, Coaching
 └─ Files + categories           BrandExtraction
AgentDiscovery             ──►   Personality, Branding, Website, Coaching
 └─ Profiles + reviews           BrandExtraction, BrandVoice, Compliance, Fee
 └─ Websites
 └─ HeadshotBytes
 └─ LogoBytes
```

---

## Observability

- `ActivitySource`: `RealEstateStar.Activation` — spans for each phase + root pipeline span
- Metrics: `activation.started`, `activation.completed`, `activation.skipped`, `activation.failed`
- All spans tagged with `accountId` and `agentId`
- Each phase writes `outcome: complete | failed` tag on its span
- Error code prefix: `ACTV-0xx`

---

## Project Structure

```
Workers/Activation/
  RealEstateStar.Workers.Activation.Orchestrator/   ← BackgroundService + pipeline coordination
  RealEstateStar.Workers.Activation.EmailFetch/     ← Phase 1: Gmail corpus fetch
  RealEstateStar.Workers.Activation.DriveIndex/     ← Phase 1: Google Drive indexing
  RealEstateStar.Workers.Activation.AgentDiscovery/ ← Phase 1: web scraping + profile discovery
  RealEstateStar.Workers.Activation.VoiceExtraction/
  RealEstateStar.Workers.Activation.Personality/
  RealEstateStar.Workers.Activation.BrandingDiscovery/
  RealEstateStar.Workers.Activation.CmaStyle/
  RealEstateStar.Workers.Activation.MarketingStyle/
  RealEstateStar.Workers.Activation.WebsiteStyle/
  RealEstateStar.Workers.Activation.PipelineAnalysis/
  RealEstateStar.Workers.Activation.Coaching/
  RealEstateStar.Workers.Activation.BrandExtraction/
  RealEstateStar.Workers.Activation.BrandVoice/
  RealEstateStar.Workers.Activation.ComplianceAnalysis/
  RealEstateStar.Workers.Activation.FeeStructure/
Activities/Activation/
  RealEstateStar.Activities.Activation.PersistAgentProfile/
  RealEstateStar.Activities.Activation.BrandMerge/
```

**Dependency rule**: All individual Activation workers depend only on `Domain` + `Workers.Shared`.
The Orchestrator additionally depends on all 15 workers + 2 Activities.

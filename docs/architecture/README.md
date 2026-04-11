# Architecture Documentation

Architecture diagrams for Real Estate Star, rendered as Mermaid diagrams viewable directly on GitHub.

## Diagrams

| Document | Description |
|----------|------------|
| [System Overview](system-overview.md) | Monorepo structure, app relationships, tech stack |
| [Multi-Tenant Routing](multi-tenant-routing.md) | DNS → middleware → config → template request flow |
| [CMA Pipeline](cma-pipeline.md) | End-to-end CMA flow: form → API → PDF → email |
| [Template Rendering](template-rendering.md) | Config loading → template selection → section rendering → CSS variables |
| [Data Model](data-model.md) | Agent config entity model, section types, file relationships |
| [Skill Integration](skill-integration.md) | Config-driven skills, onboarding flow, field mapping |
| [Frontend Package Dependencies](frontend-package-dependencies.md) | 5 shared packages, dependency rules, app composition |
| [Agent-Site Feature Isolation](agent-site-feature-isolation.md) | 6 feature modules, allowed/blocked import directions |
| [Observability Flow](observability-flow.md) | Telemetry, error tracking, correlation IDs frontend→backend |
| [Google API Token Flow](google-api-token-flow.md) | Three-tier identity model, token persistence, client consumption |
| [OAuth Token Lifecycle](oauth-token-lifecycle.md) | Token resolution with optimistic concurrency (Mermaid) |
| [Fan-Out Storage](fan-out-storage.md) | Three-tier document writes: Agent + Account + Platform Drive |
| [Notification Email Flow](notification-email-flow.md) | Pipeline → Notifier → Gmail API → Fan-Out storage (Mermaid) |
| [Google Client Dependency Graph](google-client-dependency-graph.md) | Interface ownership + DI wiring (Mermaid) |
| [Lead Orchestrator Flow](lead-orchestrator-flow.md) | Lead pipeline: scoring → dispatch → collect → email → notify |
| [Worker Dispatch Routing](worker-dispatch-routing.md) | CMA/HomeSearch/PDF activity dispatch via Durable Functions fan-out (Task.WhenAll + CallActivityAsync) |
| [Agent Notification Flow](agent-notification-flow.md) | WhatsApp-first agent notification with email fallback |
| [Per-Lead Orchestrator Lifecycle](per-lead-orchestrator-lifecycle.md) | Per-lead instance: score → fan-out → PDF → email → notify → persist |
| [Shared Project Dependencies](shared-project-dependencies.md) | Shared vs lead-specific worker project dependency graph |
| [Pipeline Context Data Flow](pipeline-context-data-flow.md) | LeadPipelineContext class diagram — what each activity reads/writes |
| [Communication Draft/Send Split](communication-draft-send-split.md) | Draft and send as separate activities with content-hash dedup |
| [Lead Scoring with Engagement](lead-scoring-engagement.md) | Weighted scoring factors including repeat submission engagement |
| [Observability Span Tree](observability-span-tree.md) | Trace span hierarchy with per-activity metrics |
| [Security Hardening Layers](security-hardening-layers.md) | Defense-in-depth: input sanitization, output encoding, secret management |

## API Project Structure (44+ Projects + 34 Test Projects)

The .NET 10 backend (`apps/api/`) is split into 44+ isolated production projects and 34 test projects following a strict dependency graph:

| Layer | Projects | Depends On |
|-------|----------|------------|
| **Domain** | `RealEstateStar.Domain` | Nothing (pure models, interfaces, enums) |
| **Data** | `RealEstateStar.Data` | Domain only |
| **DataServices** | `RealEstateStar.DataServices` | Domain only |
| **Workers** | `Workers.Shared`, `Workers.Lead.Orchestrator`, `Workers.Lead.CMA`, `Workers.Lead.HomeSearch`, `Workers.WhatsApp`, + 16 Activation workers | Domain + Workers.Shared |
| **Activities** | `Activities.Pdf`, `Activities.Persist`, `Activities.Activation.PersistAgentProfile`, `Activities.Activation.BrandMerge` | Domain + Workers.Shared |
| **Services** | `Services.AgentNotifier`, `Services.LeadCommunicator`, `Services.AgentConfig`, `Services.BrandMerge`, `Services.WelcomeNotification` | Domain only |
| **Clients** | `Clients.Anthropic`, `Clients.Scraper`, `Clients.WhatsApp`, `Clients.GDrive`, `Clients.Gmail`, `Clients.GDocs`, `Clients.GSheets`, `Clients.GoogleOAuth`, `Clients.Stripe`, `Clients.Cloudflare`, `Clients.Turnstile`, `Clients.Azure`, `Clients.Gws`, `Clients.RentCast` | Domain only (own internal DTOs) |
| **Api** | `RealEstateStar.Api` | Everything (sole composition root) |

Key rules:
- **Domain owns ALL interfaces** — storage, client, sender contracts
- **Api is the sole composition root** — only project that wires DI bindings
- **Each Client has its own DTOs** — maps to Domain types internally, no leaking
- **Architecture enforced** at compile-time (csproj refs) and CI-time (ArchUnit tests in `tests/RealEstateStar.Architecture.Tests/`)
- **1175+ tests** across 23 test projects (1:1 with production projects + `Architecture.Tests` + `TestUtilities`)

Design spec: `docs/superpowers/specs/2026-03-21-api-project-restructure-design.md`

## Shared LeadForm Component

| Document | Description |
|----------|------------|
| [Component Hierarchy](shared-lead-form-component-hierarchy.md) | How shared packages are consumed by agent sites and platform |
| [Data Flow](lead-form-data-flow.md) | User input through submission to the .NET CMA API |
| [Data Model](lead-form-data-model.md) | Shared type hierarchy for buyer and seller lead capture |
| [Google Maps Autocomplete](google-maps-autocomplete-lifecycle.md) | Lazy SDK loading and address autocomplete lifecycle |

## CMA Integration & Multi-Template

| Document | Description |
|----------|------------|
| [CMA Form Submission Flow](cma-form-submission-flow.md) | Seller vs buyer lead routing through shared hook to .NET API |
| [Multi-Template Composition](multi-template-composition.md) | How 10 templates compose section variants from the shared library |
| [Multi-Template Selection](multi-template-selection.md) | How requests resolve to one of 10 templates and render enabled sections |
| [Section Variant Architecture](section-variant-architecture.md) | How section categories map to template-specific variants across all 10 templates |
| [Shared Package Dependencies](shared-package-dependencies.md) | How packages/ui and shared-types are consumed by apps |

## Compliance and Legal

| Document | Description |
|----------|------------|
| [Compliance Component Hierarchy](compliance-component-hierarchy.md) | How shared TCPA, EHO, and CMA compliance components flow through apps |
| [Legal Page Rendering](legal-page-rendering.md) | Dynamic state-specific content rendering on terms and privacy pages |
| [Legal Content System](legal-content-system.md) | Above/below-the-fold legal markdown discovery and rendering pipeline |

## CMA Comp Data

| Document | Description |
|----------|------------|
| [RentCast Comp Flow](rentcast-comp-flow.md) | LeadForm → SubmitEndpoint → CmaWorker → CompAggregator → RentCastCompSource → RentCastClient → api.rentcast.io |

## Lead Submission & Processing

| Document | Description |
|----------|------------|
| [Lead Processing Pipeline](lead-processing-pipeline.md) | Lead submission through enrichment to parallel CMA and Home Search pipelines |
| [Background Service Health](background-service-health.md) | Durable Functions health check via DF management API — reports running/failed orchestration instance counts |

## Agent Activation (OAuth + Profiling)

| Document | Description |
|----------|------------|
| [OAuth Authorization Flow](oauth-authorization-flow.md) | HMAC-signed link → Google OAuth → token storage → activation enqueue |
| [Agent Activation Pipeline](agent-activation-pipeline.md) | 5-phase pipeline: Gather → Synthesize → Persist → Notify |
| [Activation MVP Redesign](activation-mvp-redesign.md) | MVP tier: 8 workers, pipeline.json, C# optimizations, tier dispatch |
| [Activation Pipeline Data Flow](activation-pipeline-data-flow.md) | 6-phase pipeline: Gather → Classify → Synthesize → Merge → Persist → Notify |
| [Activation Synthesis Merge](activation-synthesis-merge.md) | Phase 2.25: coaching enrichment, contradiction detection, strengths summary |
| [Activation Data Enrichment](activation-data-enrichment.md) | How extraction records and reviews flow into synthesis workers |
| [Activation Cost Model](activation-cost-model.md) | Per-agent and per-lead Claude API cost breakdown |
| [Activation Analysis Dimensions](activation-analysis-dimensions.md) | User-facing explainer of the nine analysis dimensions |

## WhatsApp Integration

| Document | Description |
|----------|------------|
| [WhatsApp Message Flow](whatsapp-message-flow.md) | Inbound webhook verification, deduplication, durable processing, and conversation handling |

## Durable Functions Migration (PR #127)

| Document | Description |
|----------|------------|
| [Before vs After Architecture](pr127-before-after-architecture.md) | Migration from BackgroundServices to Azure Durable Functions |
| [Lead Pipeline Durable Flow](pr127-lead-pipeline-durable-flow.md) | Lead processing via Durable orchestrator with parallel fan-out and idempotency |
| [Idempotency and Replay Safety](pr127-idempotency-and-replay.md) | How duplicate sends are prevented during Durable Functions replay |

## How to Read

All diagrams use [Mermaid](https://mermaid.js.org/) syntax. GitHub renders them natively in Markdown preview. For local viewing, use a Mermaid-compatible Markdown viewer or the [Mermaid Live Editor](https://mermaid.live/).

## Color Key

| Color | Meaning |
|-------|---------|
| Blue (#4A90D9) | Next.js frontend applications |
| Purple (#7B68EE) | .NET backend / API |
| Green (#2E7D32) | Skills / AI workflows |
| Gold (#C8A951) | Configuration / data |

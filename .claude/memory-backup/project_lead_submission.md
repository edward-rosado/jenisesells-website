---
name: project_lead_submission
description: "Lead Submission API feature — architecture, endpoints, services, security, compliance status"
type: project
---

# Lead Submission API Feature

**Branch:** `feat/lead-submission-api` (worktree: `.worktrees/lead-submission-api`)
**Status as of 2026-03-20:** 50 commits, 1071 tests, all remediations complete, pending PR to main

## Architecture
- Server-side submission: browser → Next.js server action (CF Worker) → HMAC-SHA256 signed → .NET API
- Lead data stored as markdown with YAML frontmatter in Google Drive
- Fire-and-forget background pipeline: enrich → score → notify → CMA/home search
- Pluggable abstractions: IFileStorageProvider, ILeadStore, ILeadEnricher, ILeadNotifier, IHomeSearchProvider

## API Endpoints (Features/Leads/)
| Endpoint | Method | Path |
|----------|--------|------|
| SubmitLead | POST | /agents/{agentId}/leads |
| OptOut | POST | /agents/{agentId}/leads/opt-out |
| Subscribe | POST | /agents/{agentId}/leads/subscribe |
| DeleteData | DELETE | /agents/{agentId}/leads/data |
| RequestDeletion | POST | /agents/{agentId}/leads/request-deletion |
| RetryFailed | POST | /agents/{agentId}/leads/retry-failed |
| PollDriveChanges | POST | /internal/drive/poll |

## Key Services
- `IFileStorageProvider` — GDriveStorageProvider (prod) / LocalStorageProvider (dev)
- `ILeadStore` — GDriveLeadStore / FileLeadStore, markdown + YAML frontmatter
- `IMarketingConsentLog` — Google Sheets audit trail
- `IDeletionAuditLog` — GDPR/CCPA deletion event log
- `ILeadNotifier` — Multi-channel: email, SMS, Google Chat
- `ILeadEnricher` — ScraperLeadEnricher (Claude API + ScraperAPI)
- `IHomeSearchProvider` — ScraperHomeSearchProvider
- `ILeadDataDeletion` — GDriveLeadDataDeletion with email verification tokens

## Security
- HMAC-SHA256 middleware with per-agent key derivation
- Rate limiting on all endpoints
- Cloudflare Turnstile + honeypot bot protection
- YAML frontmatter injection protection (escaped user values)
- Constant-time token comparisons
- PII hashed in all structured logs (GwsService, DeletionAuditLog)
- ForwardedHeaders configured for Cloudflare proxy chain
- OAuth tokens encrypted at rest via Data Protection API + Azure Key Vault

## Compliance (audited 2026-03-20)
- ADA/WCAG 2.1 AA: aria-describedby, hint contrast, Turnstile widget, honeypot a11y
- CCPA: Right to Delete, Right to Know, Right to Opt-Out, non-discrimination
- GDPR: Explicit consent, erasure, consent audit trail, data minimization, portability
- TCPA: Unchecked consent checkbox, consent text, channel recording
- CAN-SPAM: Physical address fallback, one-click unsubscribe URL, transactional classification

## Frontend (apps/agent-site)
- LeadForm with Turnstile, honeypot, TCPA consent checkbox
- Privacy pages: /privacy/opt-out, /privacy/delete, /privacy/subscribe
- Server action with HMAC signing
- Marketing consent piped through LeadFormData → server action → API
- Grafana Faro browser tracing

## CI/CD
- Drive monitor cron workflow
- Lead pipeline smoke test
- TypeScript API client package (packages/api-client/)

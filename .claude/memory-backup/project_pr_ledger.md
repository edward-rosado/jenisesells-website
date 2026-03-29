---
name: project_pr_ledger
description: Ledger of open/recent PRs with their last known status — check and update each session
type: project
---

# PR Ledger

## Open PRs
| PR | Title | Branch | Status | Last Checked |
|----|-------|--------|--------|-------------|
| #58 | Wire Grafana Cloud OTLP + fail deploy without observability | — | Open | 2026-03-24 |

## Recently Merged
| PR | Title | Merged |
|----|-------|--------|
| #59 | Azure Blob Storage as durable Platform tier for lead documents | 2026-03-24 |
| #57 | Fix CORS: remove WithOrigins for production telemetry | 2026-03-24 |
| #56 | refactor: ISP split, DeleteFileByNameAsync, accountId from config | 2026-03-24 |
| #54 | CORS fix + security hardening + debug config + correlation ID | 2026-03-24 |
| #53 | Fix dropdown: seller card overflow visible when active | 2026-03-24 |
| #52 | Google API clients + token persistence + shared Anthropic client | 2026-03-24 |
| #51 | Fix dropdown width — match input, not form | 2026-03-24 |
| #50 | Fix dropdown attribution spacing | 2026-03-24 |
| #49 | Fix autocomplete dropdown: max height + remove bullets | 2026-03-24 |
| #48 | Fix Google Maps: use street_address type + drop loading=async | 2026-03-24 |
| #47 | Fix Google Maps CSP: add places.googleapis.com + drop loading=async | 2026-03-24 |
| #46 | Fix CORS: replace dead pages.dev with workers.dev for previews | 2026-03-24 |
| #45 | Fix Google Maps async loading — use callback pattern | 2026-03-24 |
| #44 | Migrate all agent-site API calls to typed client | 2026-03-24 |
| #43 | Fix OpenAPI export — complete config hydration + flaky test | 2026-03-23 |
| #42 | OpenAPI pipeline + typed API client migration | 2026-03-23 |
| #41 | API observability dashboard + Claude token tracking | 2026-03-23 |
| #40 | Migrate Google Places autocomplete from legacy to Data API | 2026-03-23 |
| #39 | Pipeline context + ScraperAPI observability | 2026-03-23 |
| #30 | WhatsApp agent channel, status page upgrade, and agent site fixes | 2026-03-21 |
| #28 | Lead Submission API with enrichment, GDPR/CCPA, and Drive storage | 2026-03-20 |

## Closed (not merged)
| PR | Title | Reason |
|----|-------|--------|
| #55 | CORS fix + security hardening + debug logging + correlation ID | Superseded by #54 |
| #29 | WhatsApp agent channel (original) | Superseded by #30 |
| #25 | ADA/WCAG 2.1 AA compliance and security hardening | Recreated as #26 |
| #6 | fix: show auto-sent URL as user bubble and render SSE card markers | Superseded |
| #5 | Claude/purchase domain aq zo1 | Auto-generated, not needed |

## Notes
- Branch protection on main requires PR + CI pass
- Use `--admin` flag on merge only if CI passes but branch protection blocks
- Always check this ledger at session start to see what's in flight

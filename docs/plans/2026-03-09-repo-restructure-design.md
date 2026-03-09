# Real Estate Star - Repository Restructure Design

**Date:** 2026-03-09
**Status:** Approved
**Author:** Eddie Rosado + Claude

## Executive Summary

Restructure the jenisesells-website prototype into a multi-tenant SaaS monorepo
for Real Estate Star — a platform that automates real estate agent workflows
including CMA generation, contract drafting, email automation, website generation,
and market analysis.

## Architecture

### Tech Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| Portal (Real Estate Star branded) | Next.js | 16.x |
| Agent Sites (white-label) | Next.js | 16.x |
| API Backend | .NET | 10.x |
| Task Coordination | GitHub Issues + Projects | Free |

### Repository Structure

```
Real-Estate-Star/
├── apps/
│   ├── portal/                  # Real Estate Star dashboard (Next.js 16)
│   │   └── Agent dashboard, tools, analytics, profile management
│   ├── agent-site/              # White-label per-agent websites (Next.js 16)
│   │   └── Lead gen, market analysis, campaigns
│   └── api/                     # Shared backend (.NET 10)
│       └── Multi-tenant logic, integrations, business rules
│
├── packages/
│   ├── shared-types/            # TypeScript types shared across frontends
│   └── ui/                      # Shared component library
│
├── skills/                      # Product skills (multi-tenant, state-agnostic)
│   ├── cma/                     # Comparative Market Analysis
│   │   └── SKILL.md
│   ├── contracts/               # Contract drafting
│   │   ├── SKILL.md
│   │   └── templates/           # State-specific form templates
│   │       └── NJ/              # First state implementation
│   ├── email/                   # Automated email responses
│   │   └── SKILL.md
│   └── deploy/                  # Site deployment automation
│       └── SKILL.md
│
├── config/
│   ├── agent.schema.json        # Agent profile JSON schema (source of truth)
│   └── agents/
│       └── jenise-buckalew.json # First tenant (reference implementation)
│
├── .claude/                     # Contributor tooling (auto-loaded by Claude Code)
│   ├── CLAUDE.md                # Project instructions
│   ├── settings.json            # Plugin manifest
│   ├── skills/                  # Dev workflow skills
│   ├── rules/                   # Project coding standards
│   └── memory/                  # Shared project memory
│
├── docs/
│   ├── plans/                   # Design docs, PRDs
│   ├── pm-skills-setup.md       # How to install PM skills (deanpeters repo)
│   └── onboarding.md            # New contributor guide
│
├── infra/                       # Docker, CI/CD (hosting TBD)
│
├── prototype/                   # Mom's original HTML site (reference only)
│
├── setup.sh                     # One-command project setup
└── setup.ps1                    # Windows PowerShell setup alternative
```

## Agent Profile (Config-File Approach)

Each agent is a JSON file in `config/agents/`. Skills read from this config
instead of hardcoding values. When the portal is built, it manages these files
(or migrates to a database).

### Schema

```json
{
  "id": "string (slug)",
  "identity": {
    "name": "string",
    "title": "string (e.g. REALTOR®)",
    "license_id": "string",
    "brokerage": "string",
    "brokerage_id": "string",
    "phone": "string",
    "email": "string",
    "website": "string",
    "languages": ["string"],
    "tagline": "string"
  },
  "location": {
    "state": "string (2-letter code)",
    "office_address": "string",
    "service_areas": ["string"]
  },
  "branding": {
    "primary_color": "string (hex)",
    "secondary_color": "string (hex)",
    "accent_color": "string (hex)",
    "font_family": "string"
  },
  "integrations": {
    "email_provider": "string (gmail | outlook | smtp)",
    "hosting": "string (TBD — analysis pending)",
    "form_handler": "string (formspree | custom)"
  },
  "compliance": {
    "state_form": "string (template key, e.g. NJ-REALTORS-118)",
    "licensing_body": "string",
    "disclosure_requirements": ["string"]
  }
}
```

### First Tenant (Reference)

Jenise Buckalew's profile serves as the reference implementation, populated
from the data in the original prototype.

## Skill Transformations

### CMA (State-Agnostic)

**Before:** NJ counties hardcoded, Jenise branding, fixed signature.
**After:** Reads agent config for state, service areas, branding. Web search
scoped to agent's market. PDF uses agent's colors, logo, contact info.
**Variables:** agent.identity.*, agent.location.*, agent.branding.*

### Contracts (State-Agnostic)

**Before:** NJ REALTORS Form 118 only, Jenise's license hardcoded.
**After:** Loads state-specific form template from `skills/contracts/templates/{state}/`.
Agent license, brokerage, contact from config. Designed to support any state —
NJ is the first implementation.
**Variables:** agent.identity.*, agent.location.state, agent.compliance.*

### Email (Multi-Tenant)

**Before:** Hardcoded jenisesellsnj@gmail.com, fixed signature block.
**After:** Email provider from config. Signature block built dynamically from
agent identity (name, title, brokerage, phone, email, website, languages, tagline).
**Variables:** agent.identity.*, agent.integrations.email_provider

### Deploy (Provider-Neutral)

**Before:** Pushes to one GitHub repo, Netlify project ID hardcoded.
**After:** Hosting provider from config. Supports multiple providers (TBD after
hosting analysis). Per-agent deployment target.
**Variables:** agent.integrations.hosting, agent.branding.*, agent.identity.website

## Setup Script

A single `setup.sh` (with `setup.ps1` for Windows) that:

1. Checks prerequisites (Node.js, .NET SDK, git, gh CLI)
2. Installs npm dependencies (portal, agent-site, packages)
3. Restores .NET packages (api)
4. Sets up .claude/ config (symlinks or copies)
5. Recommends optional PM skills installation (deanpeters repo)
6. Validates the setup with a health check
7. Prints next steps

## Contributor Skills

### Included in Repo (.claude/skills/)

**From Superpowers:** brainstorming, writing-plans, executing-plans,
test-driven-development, verification-before-completion, systematic-debugging,
requesting-code-review, receiving-code-review, subagent-driven-development,
dispatching-parallel-agents, finishing-a-development-branch, using-git-worktrees

**From Personal:** open-pr, pr-diagrams, update-pr, dotnet, eddie-voice

**From Everything Claude Code:** frontend-patterns, api-design,
database-migrations, postgres-patterns, security-review, e2e-testing,
deployment-patterns, docker-patterns, coding-standards, continuous-learning,
search-first, cost-aware-llm-pipeline, market-research, content-engine,
article-writing, strategic-compact, continuous-learning-v2, eval-harness,
skill-creator

**From Figma:** implement-design, code-connect-components, create-design-system-rules

### External (Contributors Install Locally)

**deanpeters/Product-Manager-Skills** (CC BY-NC-SA 4.0 — personal dev tool):
- prd-development, roadmap-planning, discovery-process
- epic-breakdown-advisor, user-story, user-story-mapping
- prioritization-advisor, problem-statement, proto-persona
- tam-sam-som-calculator, positioning-statement
- saas-economics-efficiency-metrics, business-health-diagnostic
- finance-based-pricing-advisor

## PM Workflow

```
PRD (via prd-development skill) → docs/plans/YYYY-MM-DD-<feature>-prd.md
  → brainstorming skill → design doc
    → writing-plans skill → implementation plan
      → GitHub Milestone + Issues (free kanban via GitHub Projects)
        → executing-plans → PRs linked to issues → auto-close on merge
```

## Decisions Deferred

- **Hosting provider:** Requires cost/capability analysis (Netlify vs Vercel vs AWS vs self-hosted)
- **Database selection:** Postgres likely, but deferred until API work begins
- **Auth provider:** TBD when portal authentication is built
- **MLS integration approach:** Requires research into MLS data access
- **DocuSign API integration:** Requires account setup and API analysis

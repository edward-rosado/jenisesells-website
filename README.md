# Real Estate Star

A SaaS platform that automates real estate agent workflows — from lead response and market analysis to contract drafting and website deployment.

## Why

Small independent agents spend hours on repetitive tasks: responding to leads, pulling comps, drafting contracts, updating their website. Real Estate Star automates all of it with AI-powered skills that read from a simple JSON config per agent.

## Quick Start

```bash
git clone https://github.com/edward-rosado/Real-Estate-Star.git
cd Real-Estate-Star

# macOS / Linux / Git Bash
bash setup.sh

# Windows PowerShell
.\setup.ps1
```

See [docs/onboarding.md](docs/onboarding.md) for the full contributor guide.

## Repo Structure

```
apps/
  portal/              Next.js 16 — Real Estate Star admin portal
  agent-site/          Next.js 16 — white-label agent websites
  api/                 .NET 10 — backend API
packages/
  shared-types/        TypeScript types shared across apps
  ui/                  Shared UI component library
skills/
  cma/                 Comparative Market Analysis generator
  contracts/           State-specific contract drafting
  email/               Multi-provider email sending
  deploy/              Website deployment
config/
  agent.schema.json    JSON Schema for agent profiles
  agents/              Per-tenant agent configurations
prototype/             Original jenisesellsnj.com static site
infra/                 Infrastructure and hosting config
docs/                  Design docs, onboarding, plans
```

## Multi-Tenant Architecture

Every agent (tenant) has a JSON config file at `config/agents/{agent-id}.json`. All skills read from this config — no hardcoded agent data anywhere.

```json
{
  "id": "jenise-buckalew",
  "identity": { "name": "Jenise Buckalew", "email": "...", "phone": "..." },
  "location": { "state": "NJ", "service_areas": ["Middlesex County", "..."] },
  "branding": { "primary_color": "#1B5E20", "accent_color": "#C8A951" },
  "integrations": { "email_provider": "gmail" },
  "compliance": { "state_form": "NJ-REALTORS-118" }
}
```

Adding a new agent = creating a new JSON file. No code changes needed.

## Skills

| Skill | What it does |
|-------|-------------|
| **CMA** | Generates a branded PDF market analysis from a property address, emails it to the lead |
| **Contracts** | Drafts state-specific sales contracts (NJ Form 118 supported, extensible) |
| **Email** | Sends emails via Gmail, Outlook, or SMTP with dynamic signature blocks |
| **Deploy** | Deploys agent websites to configured hosting provider |

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Portal | Next.js 16 |
| Agent Sites | Next.js 16 (white-label) |
| API | .NET 10 |
| Config | JSON + JSON Schema |
| AI Tooling | Claude Code with custom skills |
| PM | GitHub Issues + Projects |

## Adding a New State

Contracts are state-agnostic. To add support for a new state:

1. Create `skills/contracts/templates/{STATE}/README.md`
2. Document the state's standard form, sections, and fields
3. Add entry to the supported states table in `skills/contracts/SKILL.md`

## License

Private — not open source.

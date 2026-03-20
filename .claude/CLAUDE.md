# Real Estate Star

A SaaS platform that automates real estate agent workflows — from lead response to contract drafting to website deployment.

## Monorepo Structure

```
apps/
  portal/          # Real Estate Star admin portal (Next.js 16)
  agent-site/      # White-label agent websites (Next.js 16)
  api/             # Backend API (.NET 10)
    Features/
      Leads/       # Lead submission, storage, and markdown rendering
packages/
  shared-types/    # TypeScript types shared across apps
  ui/              # Shared UI component library
skills/
  cma/             # Comparative Market Analysis generator
  contracts/       # State-specific contract drafting
  email/           # Multi-provider email sending
  deploy/          # Website deployment
config/
  agent.schema.json          # JSON Schema for agent profiles
  agents/{agent-id}.json     # Per-tenant agent configurations
prototype/         # Original jenisesellsnj.com static site
infra/             # Infrastructure and hosting config
docs/              # Design docs, onboarding, plans
```

## Multi-Tenant Architecture

Every agent (tenant) has a JSON config file at `config/agents/{agent-id}.json` validated against `config/agent.schema.json`.

**All skills read from agent config — never hardcode agent-specific data.**

### Loading an Agent Profile

When working on a skill, load the agent profile first:

```
1. Read config/agents/{agent-id}.json
2. Use {agent.identity.*} for name, phone, email, brokerage, etc.
3. Use {agent.location.*} for state, service areas, office address
4. Use {agent.branding.*} for colors, fonts
5. Use {agent.integrations.*} for email provider, hosting, form handler
6. Use {agent.compliance.*} for state forms, licensing body, disclosures
```

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Portal | Next.js 16 |
| Agent Sites | Next.js 16 (white-label) |
| API | .NET 10 |
| Agent Config | JSON + JSON Schema |
| PM | GitHub Issues + Projects |

## Key Conventions

- **Domain name**: The domain is `real-estate-star.com` (hyphenated). Use `real-estate-star` everywhere -- DNS, Azure resource names, Cloudflare, GitHub, etc. NEVER use `realestatestar` (no hyphens).
- **Commits**: Conventional commits (`feat:`, `fix:`, `docs:`, `chore:`, etc.)
- **Skills**: Reference agent config with `{agent.*}` variable syntax
- **Contracts**: State-specific templates live in `skills/contracts/templates/{STATE}/`
- **No hardcoding**: Agent identity, branding, and compliance data always come from config

## File Storage Abstraction

The `IFileStorageProvider` interface abstracts lead storage across Google Drive and local file system:

- **Google Drive** (`GDriveStorageProvider`): Production storage in agent's Drive folder (folder ID from config)
- **Local** (`LocalStorageProvider`): Development/testing in `data/leads/{agent-id}/`
- **Configuration**: `Storage:UseLocal` (bool) in appsettings selects provider at startup

All lead files are markdown with YAML frontmatter. Frontmatter keys are validated against the Lead schema; user content goes in the markdown body.

## Docs

- Design: `docs/plans/2026-03-09-repo-restructure-design.md`
- CMA Pipeline Design: `docs/plans/2026-03-09-cma-pipeline-design.md`
- CMA Pipeline Plan: `docs/plans/2026-03-09-cma-pipeline-plan.md`
- Lead Submission Design: `docs/superpowers/specs/2026-03-19-lead-submission-api-design.md`
- Onboarding: `docs/onboarding.md`
- PM Skills: `docs/pm-skills-setup.md`

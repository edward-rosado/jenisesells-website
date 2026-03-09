# Contributor Onboarding Guide

## Quick Start

```bash
# Clone the repo
git clone https://github.com/edward-rosado/Real-Estate-Star.git
cd Real-Estate-Star

# Run the setup script
bash setup.sh        # macOS / Linux / WSL
# or
.\setup.ps1          # Windows PowerShell
```

The setup script installs dependencies, symlinks Claude Code configuration, and validates your environment.

## Repo Structure Overview

| Directory | Purpose |
|-----------|---------|
| `apps/` | Deployable applications (portal, agent sites, API) |
| `packages/` | Shared libraries and utilities across apps |
| `skills/` | Claude Code skills (contracts, comms, CMA, etc.) |
| `config/` | Agent profiles, schemas, and shared configuration |
| `prototype/` | Original prototype files (preserved for reference) |
| `infra/` | Infrastructure-as-code and deployment configs |
| `docs/` | Documentation, plans, and guides |

For the full architectural rationale, see the [design doc](plans/2026-03-09-repo-restructure-design.md).

## How Agent Profiles Work

Every real estate agent on the platform has a JSON profile at:

```
config/agents/{id}.json
```

Profiles are validated against `config/agent.schema.json`. A profile contains the agent's identity, branding, contact info, service areas, and feature flags. All skills and apps read from these profiles rather than hardcoding agent-specific data.

Example structure:

```jsonc
{
  "identity": {
    "name": "Jenise Buckalew",
    "license_state": "NJ",
    "brokerage": "RE/MAX"
  },
  "branding": {
    "primary_color": "#1a3c6e",
    "tagline": "Your home, your way"
  },
  "contact": { ... },
  "features": { ... }
}
```

## How Product Skills Reference Agent Config

Skills use variable syntax to pull data from the active agent profile at runtime:

- `{agent.identity.name}` -- agent's display name
- `{agent.branding.primary_color}` -- brand color for generated content
- `{agent.contact.email}` -- contact email
- `{agent.identity.license_state}` -- determines which contract templates apply

Skills **never** hardcode agent data. This keeps every skill multi-tenant by default.

## How to Add a New Agent

1. Copy the reference tenant profile:
   ```bash
   cp config/agents/reference.json config/agents/{new-id}.json
   ```
2. Update all fields (identity, branding, contact, service areas).
3. Validate against the schema:
   ```bash
   npx ajv validate -s config/agent.schema.json -d config/agents/{new-id}.json
   ```
4. Test with each skill to confirm variable substitution works correctly.

## How to Add a New State for Contracts

1. Create the state directory and README:
   ```bash
   mkdir -p skills/contracts/templates/{STATE}
   ```
2. Add a `README.md` documenting the state-specific form (e.g., field mappings, legal requirements, source form name).
3. Add contract templates to the directory following the existing format.
4. Update the supported states table in `skills/contracts/README.md`.

## Development Workflow

The project follows a structured pipeline:

1. **Brainstorm** -- Identify the problem and potential solutions.
2. **Plan** -- Use the planner agent to create an implementation plan with phases.
3. **TDD** -- Write tests first (RED), implement (GREEN), refactor (IMPROVE).
4. **Code Review** -- Use the code-reviewer agent to catch issues before committing.
5. **PR** -- Open a pull request with a comprehensive summary and test plan.

For full details on each step, see the rules in `.claude/rules/`.

## Key Tools

| Tool | Purpose |
|------|---------|
| **Claude Code** | AI pair programmer -- used for planning, coding, reviewing, and committing |
| **GitHub CLI (`gh`)** | Issues, PRs, searches, and CI interaction from the terminal |
| **PM Skills** | Optional product management skills for Claude Code (see [PM Skills Setup](pm-skills-setup.md)) |

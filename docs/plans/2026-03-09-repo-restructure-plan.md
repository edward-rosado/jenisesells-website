# Repository Restructure Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Restructure the flat jenisesells prototype into the multi-tenant Real Estate Star monorepo.

**Architecture:** Monorepo with three apps (portal, agent-site, api), shared packages, multi-tenant product skills, agent config files, contributor tooling, and setup scripts. See `docs/plans/2026-03-09-repo-restructure-design.md` for full design.

**Tech Stack:** Next.js 16, .NET 10, TypeScript, JSON Schema

---

## Task 1: Move Prototype Files

**Files:**
- Move: `index.html` → `prototype/index.html`
- Move: `thank-you.html` → `prototype/thank-you.html`
- Move: `selling-home-*.html` → `prototype/`
- Move: `headshot.jpg` → `prototype/headshot.jpg`
- Move: `logo.png` → `prototype/logo.png`

**Step 1: Create prototype directory**

```bash
mkdir -p prototype
```

**Step 2: Move all original site files**

```bash
mv index.html thank-you.html headshot.jpg logo.png prototype/
mv selling-home-*.html prototype/
```

**Step 3: Verify prototype directory**

```bash
ls prototype/
```

Expected: `headshot.jpg`, `index.html`, `logo.png`, 5 `selling-home-*.html` files, `thank-you.html`

**Step 4: Commit**

```bash
git add -A
git commit -m "chore: move prototype files to prototype/"
```

---

## Task 2: Create Directory Scaffolding

**Files:**
- Create directories for the full repo structure

**Step 1: Create all directories**

```bash
mkdir -p apps/portal
mkdir -p apps/agent-site
mkdir -p apps/api
mkdir -p packages/shared-types
mkdir -p packages/ui
mkdir -p skills/cma
mkdir -p skills/contracts/templates/NJ
mkdir -p skills/email
mkdir -p skills/deploy
mkdir -p config/agents
mkdir -p infra
```

**Step 2: Create .gitkeep files so empty dirs are tracked**

```bash
for dir in apps/portal apps/agent-site apps/api packages/shared-types packages/ui skills/contracts/templates/NJ infra; do
  touch "$dir/.gitkeep"
done
```

**Step 3: Verify structure**

```bash
find . -name ".gitkeep" -not -path "./.git/*" | sort
```

Expected: 7 `.gitkeep` files across the scaffold.

**Step 4: Commit**

```bash
git add -A
git commit -m "chore: create monorepo directory scaffolding"
```

---

## Task 3: Create Agent Profile Schema and First Tenant

**Files:**
- Create: `config/agent.schema.json`
- Create: `config/agents/jenise-buckalew.json`

**Step 1: Write the JSON Schema**

Create `config/agent.schema.json`:

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://real-estate-star.com/agent.schema.json",
  "title": "Agent Profile",
  "description": "Configuration for a Real Estate Star tenant agent",
  "type": "object",
  "required": ["id", "identity", "location", "branding"],
  "properties": {
    "id": {
      "type": "string",
      "pattern": "^[a-z0-9-]+$",
      "description": "URL-safe slug identifier for the agent"
    },
    "identity": {
      "type": "object",
      "required": ["name", "email", "phone"],
      "properties": {
        "name": { "type": "string" },
        "title": { "type": "string", "default": "REALTOR®" },
        "license_id": { "type": "string" },
        "brokerage": { "type": "string" },
        "brokerage_id": { "type": "string" },
        "phone": { "type": "string" },
        "email": { "type": "string", "format": "email" },
        "website": { "type": "string" },
        "languages": {
          "type": "array",
          "items": { "type": "string" },
          "default": ["English"]
        },
        "tagline": { "type": "string" }
      }
    },
    "location": {
      "type": "object",
      "required": ["state"],
      "properties": {
        "state": {
          "type": "string",
          "pattern": "^[A-Z]{2}$",
          "description": "Two-letter US state code"
        },
        "office_address": { "type": "string" },
        "service_areas": {
          "type": "array",
          "items": { "type": "string" }
        }
      }
    },
    "branding": {
      "type": "object",
      "properties": {
        "primary_color": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" },
        "secondary_color": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" },
        "accent_color": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" },
        "font_family": { "type": "string", "default": "Segoe UI" }
      }
    },
    "integrations": {
      "type": "object",
      "properties": {
        "email_provider": {
          "type": "string",
          "enum": ["gmail", "outlook", "smtp"]
        },
        "hosting": {
          "type": "string",
          "description": "Hosting provider (TBD — analysis pending)"
        },
        "form_handler": {
          "type": "string",
          "enum": ["formspree", "custom"]
        },
        "form_handler_id": {
          "type": "string",
          "description": "Provider-specific form ID (e.g. Formspree form ID)"
        }
      }
    },
    "compliance": {
      "type": "object",
      "properties": {
        "state_form": {
          "type": "string",
          "description": "Template key for state-specific contract form"
        },
        "licensing_body": { "type": "string" },
        "disclosure_requirements": {
          "type": "array",
          "items": { "type": "string" }
        }
      }
    }
  }
}
```

**Step 2: Write Jenise's agent profile**

Create `config/agents/jenise-buckalew.json`:

```json
{
  "id": "jenise-buckalew",
  "identity": {
    "name": "Jenise Buckalew",
    "title": "REALTOR®",
    "license_id": "0676823",
    "brokerage": "Green Light Realty LLC",
    "brokerage_id": "1751390",
    "phone": "(347) 393-5993",
    "email": "jenisesellsnj@gmail.com",
    "website": "jenisesellsnj.com",
    "languages": ["English", "Spanish"],
    "tagline": "Forward. Moving."
  },
  "location": {
    "state": "NJ",
    "office_address": "1109 Englishtown Rd, Old Bridge, NJ 08857",
    "service_areas": [
      "Middlesex County",
      "Monmouth County",
      "Ocean County"
    ]
  },
  "branding": {
    "primary_color": "#1B5E20",
    "secondary_color": "#2E7D32",
    "accent_color": "#C8A951",
    "font_family": "Segoe UI"
  },
  "integrations": {
    "email_provider": "gmail",
    "form_handler": "formspree",
    "form_handler_id": "xkovdywn"
  },
  "compliance": {
    "state_form": "NJ-REALTORS-118",
    "licensing_body": "NJ Real Estate Commission",
    "disclosure_requirements": [
      "Lead-based paint disclosure (pre-1978)",
      "Seller property condition disclosure"
    ]
  }
}
```

**Step 3: Commit**

```bash
git add config/
git commit -m "feat: add agent profile schema and first tenant (Jenise)"
```

---

## Task 4: Transform CMA Skill (Multi-Tenant)

**Files:**
- Create: `skills/cma/SKILL.md`

**Step 1: Write the multi-tenant CMA skill**

The skill must:
- Reference `config/agents/{agent-id}.json` instead of hardcoding Jenise
- Use `agent.location.state` and `agent.location.service_areas` to scope comp searches
- Use `agent.branding.*` for PDF colors and styling
- Use `agent.identity.*` for signature, contact info, branding
- Keep the same 7-step workflow (parse → research → estimate → PDF → save → draft email → send)

Key changes from original:
- Replace all "Jenise Buckalew" references with `{agent.identity.name}`
- Replace "Green Light Realty LLC" with `{agent.identity.brokerage}`
- Replace hardcoded colors with `{agent.branding.*}`
- Replace NJ-specific county references with `{agent.location.service_areas}`
- Replace hardcoded phone/email with `{agent.identity.*}`
- Add "## Agent Config" section at top explaining how to load the profile
- Add "## State-Specific Notes" section for state-level nuances

**Step 2: Verify SKILL.md contains no hardcoded Jenise references**

Search the file for "Jenise", "Green Light", "347", "jenisesells" — should find zero matches outside of example blocks.

**Step 3: Commit**

```bash
git add skills/cma/
git commit -m "feat: add multi-tenant CMA skill"
```

---

## Task 5: Transform Contracts Skill (State-Agnostic)

**Files:**
- Create: `skills/contracts/SKILL.md`
- Create: `skills/contracts/templates/NJ/README.md`

**Step 1: Write the state-agnostic contracts skill**

The skill must:
- Reference `config/agents/{agent-id}.json` for agent identity and compliance info
- Use `agent.compliance.state_form` to select the correct template directory
- Use `agent.location.state` to determine state-specific legal requirements
- Load template from `skills/contracts/templates/{STATE}/`
- Keep the same gathering flow (parties, price, financing, broker info, etc.)

Key changes from original:
- Replace all Jenise/Green Light references with agent config variables
- Replace "NJ REALTORS® Form 118" with `{agent.compliance.state_form}`
- Add "## Supported States" section listing available templates
- Add "## Adding a New State" section with instructions for contributors
- Move NJ-specific details (form line numbers, section references) into `templates/NJ/`
- Keep the mathematical validation (purchase price check)

**Step 2: Write NJ template README**

Create `skills/contracts/templates/NJ/README.md` documenting:
- Form 118-Statewide (07/2025.2 edition)
- 14 pages, sections 1-43, lines 1-770
- Required fields and NJ-specific sections
- Reference to blank template PDF location

**Step 3: Verify no hardcoded agent data in SKILL.md**

**Step 4: Commit**

```bash
git add skills/contracts/
git commit -m "feat: add state-agnostic contracts skill with NJ template"
```

---

## Task 6: Transform Email Skill (Multi-Tenant)

**Files:**
- Create: `skills/email/SKILL.md`

**Step 1: Write the multi-tenant email skill**

The skill must:
- Reference `config/agents/{agent-id}.json` for all identity fields
- Build signature block dynamically from agent config
- Use `agent.integrations.email_provider` to determine send method
- Support gmail, outlook, and generic SMTP
- BCC the agent's own email (from config) on all lead emails

Key changes from original:
- Replace hardcoded Gmail address with `{agent.identity.email}`
- Replace hardcoded signature with dynamic template using agent identity fields
- Replace "Jenise" with `{agent.identity.name}` throughout
- Add provider-specific instructions (Gmail MCP, Outlook, SMTP)
- Keep the CMA email format as a template with variables

**Step 2: Verify no hardcoded agent data**

**Step 3: Commit**

```bash
git add skills/email/
git commit -m "feat: add multi-tenant email skill"
```

---

## Task 7: Transform Deploy Skill (Provider-Neutral)

**Files:**
- Create: `skills/deploy/SKILL.md`

**Step 1: Write the provider-neutral deploy skill**

The skill must:
- Reference `config/agents/{agent-id}.json` for hosting config
- Support multiple hosting providers (placeholder for analysis)
- Deploy agent-specific site files, not a single hardcoded repo
- Use `agent.identity.website` as the expected domain

Key changes from original:
- Remove Netlify project ID
- Remove hardcoded GitHub repo reference
- Add "## Supported Providers" section (initially: GitHub Pages, with others TBD)
- Add "## Provider Configuration" section explaining what each provider needs
- Keep the verify-after-deploy step
- Keep the rollback-via-git-history guidance

**Step 2: Verify no hardcoded hosting references**

**Step 3: Commit**

```bash
git add skills/deploy/
git commit -m "feat: add provider-neutral deploy skill"
```

---

## Task 8: Set Up .claude/ Project Config

**Files:**
- Update: `.claude/CLAUDE.md`
- Create: `.claude/settings.json`
- Create: `.claude/rules/project.md`

**Step 1: Write project CLAUDE.md**

This file is auto-loaded by Claude Code for every contributor. It should contain:
- Project overview (what Real Estate Star is)
- Monorepo structure summary
- Key conventions (agent config approach, skill variable syntax)
- Link to design doc and onboarding guide
- How to load an agent profile when working on skills
- Tech stack summary

**Step 2: Write project settings.json**

Plugin manifest for the repo (superpowers, everything-claude-code, figma, etc.)

**Step 3: Write project rules**

Create `.claude/rules/project.md` with:
- Multi-tenant rules: never hardcode agent data, always read from config
- Skill conventions: how to reference agent variables
- Commit conventions: conventional commits
- PR process: link to GitHub Issues

**Step 4: Commit**

```bash
git add .claude/CLAUDE.md .claude/settings.json .claude/rules/
git commit -m "feat: add project-level Claude config and rules"
```

---

## Task 9: Create Setup Scripts

**Files:**
- Create: `setup.sh`
- Create: `setup.ps1`

**Step 1: Write setup.sh (Bash — macOS/Linux/Git Bash on Windows)**

The script should:

```bash
#!/usr/bin/env bash
set -euo pipefail

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

info()  { echo -e "${GREEN}[OK]${NC}    $1"; }
warn()  { echo -e "${YELLOW}[WARN]${NC}  $1"; }
fail()  { echo -e "${RED}[FAIL]${NC}  $1"; }

echo "================================================"
echo "  Real Estate Star - Project Setup"
echo "================================================"
echo ""

# 1. Check prerequisites
echo "Checking prerequisites..."

# Node.js (require 20+)
if command -v node &>/dev/null; then
  NODE_VER=$(node -v | sed 's/v//')
  info "Node.js $NODE_VER"
else
  fail "Node.js not found. Install from https://nodejs.org/"
  MISSING=true
fi

# npm
if command -v npm &>/dev/null; then
  info "npm $(npm -v)"
else
  fail "npm not found"
  MISSING=true
fi

# .NET SDK (require 10+)
if command -v dotnet &>/dev/null; then
  DOTNET_VER=$(dotnet --version)
  info ".NET SDK $DOTNET_VER"
else
  fail ".NET SDK not found. Install from https://dotnet.microsoft.com/"
  MISSING=true
fi

# git
if command -v git &>/dev/null; then
  info "git $(git --version | awk '{print $3}')"
else
  fail "git not found"
  MISSING=true
fi

# gh CLI (optional but recommended)
if command -v gh &>/dev/null; then
  info "GitHub CLI $(gh --version | head -1 | awk '{print $3}')"
else
  warn "GitHub CLI not found (optional). Install from https://cli.github.com/"
fi

if [ "${MISSING:-}" = "true" ]; then
  echo ""
  fail "Missing required tools. Install them and re-run setup."
  exit 1
fi

echo ""

# 2. Install frontend dependencies
echo "Installing frontend dependencies..."

if [ -f "apps/portal/package.json" ]; then
  (cd apps/portal && npm install)
  info "Portal dependencies installed"
else
  warn "apps/portal/package.json not found (app not scaffolded yet)"
fi

if [ -f "apps/agent-site/package.json" ]; then
  (cd apps/agent-site && npm install)
  info "Agent Site dependencies installed"
else
  warn "apps/agent-site/package.json not found (app not scaffolded yet)"
fi

if [ -f "packages/ui/package.json" ]; then
  (cd packages/ui && npm install)
  info "UI package dependencies installed"
fi

if [ -f "packages/shared-types/package.json" ]; then
  (cd packages/shared-types && npm install)
  info "Shared types dependencies installed"
fi

# 3. Restore .NET packages
echo ""
echo "Restoring .NET packages..."

if [ -f "apps/api/api.csproj" ] || [ -f "apps/api/Api.csproj" ]; then
  (cd apps/api && dotnet restore)
  info ".NET packages restored"
else
  warn "apps/api/*.csproj not found (API not scaffolded yet)"
fi

# 4. Validate config
echo ""
echo "Validating agent configuration..."

if [ -f "config/agent.schema.json" ]; then
  info "Agent schema present"
else
  fail "config/agent.schema.json missing"
fi

AGENT_COUNT=$(find config/agents -name "*.json" 2>/dev/null | wc -l)
if [ "$AGENT_COUNT" -gt 0 ]; then
  info "$AGENT_COUNT agent profile(s) found"
else
  warn "No agent profiles found in config/agents/"
fi

# 5. Validate skills
echo ""
echo "Validating product skills..."

for skill in cma contracts email deploy; do
  if [ -f "skills/$skill/SKILL.md" ]; then
    info "Skill: $skill"
  else
    warn "Skill missing: skills/$skill/SKILL.md"
  fi
done

# 6. Claude setup check
echo ""
echo "Checking Claude Code config..."

if [ -f ".claude/CLAUDE.md" ]; then
  info "Project CLAUDE.md present"
else
  warn ".claude/CLAUDE.md not found"
fi

# 7. Optional: PM Skills
echo ""
echo "================================================"
echo "  Optional: Product Manager Skills"
echo "================================================"
echo ""
echo "For PRD creation, roadmap planning, and backlog management,"
echo "contributors can install PM Skills locally:"
echo ""
echo "  git clone https://github.com/deanpeters/Product-Manager-Skills.git ~/pm-skills"
echo ""
echo "License: CC BY-NC-SA 4.0 (personal dev tool, non-commercial)"
echo "See docs/pm-skills-setup.md for detailed instructions."

# 8. Summary
echo ""
echo "================================================"
echo "  Setup Complete!"
echo "================================================"
echo ""
echo "Next steps:"
echo "  1. Review config/agents/jenise-buckalew.json (reference tenant)"
echo "  2. Read docs/onboarding.md for contributor guide"
echo "  3. Check docs/plans/ for design docs and implementation plans"
echo ""
```

**Step 2: Write setup.ps1 (PowerShell — native Windows)**

Same logic as setup.sh but in PowerShell syntax. Check for `node`, `dotnet`, `git`, `gh`. Use `Write-Host` with colors. Same validation steps.

**Step 3: Make setup.sh executable**

```bash
chmod +x setup.sh
```

**Step 4: Commit**

```bash
git add setup.sh setup.ps1
git commit -m "feat: add setup scripts (bash + powershell)"
```

---

## Task 10: Create Onboarding Docs

**Files:**
- Create: `docs/onboarding.md`
- Create: `docs/pm-skills-setup.md`

**Step 1: Write onboarding guide**

`docs/onboarding.md` should cover:
- Quick start (clone + run setup script)
- Repo structure overview (link to design doc)
- How agent profiles work
- How product skills reference agent config
- How to add a new agent
- How to add a new state for contracts
- Development workflow (brainstorming → plan → TDD → review → PR)
- Key tools: Claude Code, GitHub CLI, PM Skills

**Step 2: Write PM skills setup guide**

`docs/pm-skills-setup.md` should cover:
- What PM Skills are and why we use them
- Installation: `git clone https://github.com/deanpeters/Product-Manager-Skills.git`
- How to invoke in Claude Code (reference skill paths)
- Which 14 skills we recommend (with brief descriptions)
- License note (CC BY-NC-SA 4.0, personal dev tool only)

**Step 3: Commit**

```bash
git add docs/onboarding.md docs/pm-skills-setup.md
git commit -m "docs: add onboarding guide and PM skills setup"
```

---

## Task 11: Create .gitignore

**Files:**
- Create: `.gitignore`

**Step 1: Write .gitignore**

```gitignore
# Dependencies
node_modules/
.pnp.*

# Build output
.next/
out/
bin/
obj/
dist/

# Environment
.env
.env.local
.env.*.local

# IDE
.vs/
.vscode/
.idea/
*.swp
*.swo

# OS
.DS_Store
Thumbs.db

# .NET
*.user
*.suo

# Claude Code (local only)
.claude/settings.local.json
.claude/memory/

# Agent credentials (never commit)
config/agents/*.credentials.json
```

**Step 2: Commit**

```bash
git add .gitignore
git commit -m "chore: add .gitignore for monorepo"
```

---

## Task 12: Final Verification

**Step 1: Verify directory structure matches design**

```bash
find . -not -path "./.git/*" -not -path "./node_modules/*" -type f | sort
```

Compare against the design doc structure.

**Step 2: Verify no Jenise-hardcoded data in skills**

Search all SKILL.md files for hardcoded values:

```bash
grep -r "jenisesells\|347.*393.*5993\|Green Light Realty\|xkovdywn\|prismatic-naiad" skills/
```

Expected: zero results (all values should reference agent config).

**Step 3: Verify setup script runs cleanly**

```bash
bash setup.sh
```

Expected: All checks pass (with warnings for un-scaffolded apps, which is expected).

**Step 4: Run git log to verify commit history**

```bash
git log --oneline
```

Expected: Clean series of commits following conventional commit format.

**Step 5: Final commit if any cleanup needed**

```bash
git status
```

If clean, done. If not, commit remaining changes.

---

## Execution Summary

| Task | Description | Estimated Time |
|------|-------------|---------------|
| 1 | Move prototype files | 2 min |
| 2 | Create directory scaffolding | 3 min |
| 3 | Agent profile schema + first tenant | 5 min |
| 4 | Transform CMA skill | 10 min |
| 5 | Transform contracts skill | 10 min |
| 6 | Transform email skill | 8 min |
| 7 | Transform deploy skill | 8 min |
| 8 | .claude/ project config | 8 min |
| 9 | Setup scripts | 10 min |
| 10 | Onboarding docs | 8 min |
| 11 | .gitignore | 2 min |
| 12 | Final verification | 5 min |

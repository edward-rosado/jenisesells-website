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

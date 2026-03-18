#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Info($msg)  { Write-Host "[OK]    $msg" -ForegroundColor Green }
function Warn($msg)  { Write-Host "[WARN]  $msg" -ForegroundColor Yellow }
function Fail($msg)  { Write-Host "[FAIL]  $msg" -ForegroundColor Red }

Write-Host "================================================"
Write-Host "  Real Estate Star - Project Setup"
Write-Host "================================================"
Write-Host ""

# 1. Check prerequisites
Write-Host "Checking prerequisites..."

$Missing = $false

# Node.js (require 20+)
if (Get-Command node -ErrorAction SilentlyContinue) {
    $nodeVer = (node -v) -replace '^v', ''
    Info "Node.js $nodeVer"
} else {
    Fail "Node.js not found. Install from https://nodejs.org/"
    $Missing = $true
}

# npm
if (Get-Command npm -ErrorAction SilentlyContinue) {
    $npmVer = npm -v
    Info "npm $npmVer"
} else {
    Fail "npm not found"
    $Missing = $true
}

# .NET SDK (require 10+)
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $dotnetVer = dotnet --version
    Info ".NET SDK $dotnetVer"
} else {
    Fail ".NET SDK not found. Install from https://dotnet.microsoft.com/"
    $Missing = $true
}

# git
if (Get-Command git -ErrorAction SilentlyContinue) {
    $gitVer = (git --version) -replace 'git version ', ''
    Info "git $gitVer"
} else {
    Fail "git not found"
    $Missing = $true
}

# gh CLI (optional but recommended)
if (Get-Command gh -ErrorAction SilentlyContinue) {
    $ghVer = ((gh --version) | Select-Object -First 1) -replace 'gh version ', '' -replace ' .*', ''
    Info "GitHub CLI $ghVer"
} else {
    Warn "GitHub CLI not found (optional). Install from https://cli.github.com/"
}

if ($Missing) {
    Write-Host ""
    Fail "Missing required tools. Install them and re-run setup."
    exit 1
}

Write-Host ""

# 2. Install frontend dependencies
Write-Host "Installing frontend dependencies..."

if (Test-Path "apps/portal/package.json") {
    Push-Location "apps/portal"
    npm install
    Pop-Location
    Info "Portal dependencies installed"
} else {
    Warn "apps/portal/package.json not found (app not scaffolded yet)"
}

if (Test-Path "apps/agent-site/package.json") {
    Push-Location "apps/agent-site"
    npm install
    Pop-Location
    Info "Agent Site dependencies installed"
} else {
    Warn "apps/agent-site/package.json not found (app not scaffolded yet)"
}

if (Test-Path "packages/ui/package.json") {
    Push-Location "packages/ui"
    npm install
    Pop-Location
    Info "UI package dependencies installed"
}

if (Test-Path "packages/shared-types/package.json") {
    Push-Location "packages/shared-types"
    npm install
    Pop-Location
    Info "Shared types dependencies installed"
}

# 3. Restore .NET packages
Write-Host ""
Write-Host "Restoring .NET packages..."

$apiCsproj = Get-ChildItem -Path "apps/api" -Filter "*.csproj" -ErrorAction SilentlyContinue
if ($apiCsproj) {
    Push-Location "apps/api"
    dotnet restore
    Pop-Location
    Info ".NET packages restored"
} else {
    Warn "apps/api/*.csproj not found (API not scaffolded yet)"
}

# 4. Validate config
Write-Host ""
Write-Host "Validating agent configuration..."

if (Test-Path "config/agent.schema.json") {
    Info "Agent schema present"
} else {
    Fail "config/agent.schema.json missing"
}

$accountDirs = Get-ChildItem -Path "config/accounts" -Directory -ErrorAction SilentlyContinue
$accountCount = if ($accountDirs) { $accountDirs.Count } else { 0 }
if ($accountCount -gt 0) {
    Info "$accountCount account(s) found"
} else {
    Warn "No accounts found in config/accounts/"
}

# 5. Validate skills
Write-Host ""
Write-Host "Validating product skills..."

foreach ($skill in @("cma", "contracts", "email", "deploy")) {
    if (Test-Path "skills/$skill/SKILL.md") {
        Info "Skill: $skill"
    } else {
        Warn "Skill missing: skills/$skill/SKILL.md"
    }
}

# 6. Claude setup check
Write-Host ""
Write-Host "Checking Claude Code config..."

if (Test-Path ".claude/CLAUDE.md") {
    Info "Project CLAUDE.md present"
} else {
    Warn ".claude/CLAUDE.md not found"
}

# 7. Optional: PM Skills
Write-Host ""
Write-Host "================================================"
Write-Host "  Optional: Product Manager Skills"
Write-Host "================================================"
Write-Host ""
Write-Host "For PRD creation, roadmap planning, and backlog management,"
Write-Host "contributors can install PM Skills locally:"
Write-Host ""
Write-Host "  git clone https://github.com/deanpeters/Product-Manager-Skills.git ~/pm-skills"
Write-Host ""
Write-Host "License: CC BY-NC-SA 4.0 (personal dev tool, non-commercial)"
Write-Host "See docs/pm-skills-setup.md for detailed instructions."

# 8. Summary
Write-Host ""
Write-Host "================================================"
Write-Host "  Setup Complete!"
Write-Host "================================================"
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Review config/accounts/jenise-buckalew/account.json (reference tenant)"
Write-Host "  2. Read docs/onboarding.md for contributor guide"
Write-Host "  3. Check docs/plans/ for design docs and implementation plans"
Write-Host ""

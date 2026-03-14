#Requires -Version 5.1
<#
.SYNOPSIS
    Build and deploy the Real Estate Star agent site (Next.js) to Cloudflare Pages.

.DESCRIPTION
    Uses Docker (node:22-slim) to build the Next.js app with OpenNext inside Linux,
    then deploys to Cloudflare Workers via wrangler from the host.

    The Docker build is required because Turbopack chunk filenames contain brackets
    (e.g. [root-of-the-server]__aa3583ab._.js) that the wrangler bundler silently
    drops on Windows. Building inside Linux avoids this entirely.

    The config directory is mounted read-only so the prebuild script can read
    agent profiles from config/agents/ at build time.

    First run creates the Pages project. Subsequent runs update it.

.PARAMETER Production
    Deploy to production (default deploys to preview).

.PARAMETER SkipBuild
    Skip Docker build step (deploy existing .open-next output).

.PARAMETER SetupOnly
    Only verify prerequisites and configure OpenNext -- do not build or deploy.

.EXAMPLE
    .\deploy-agent-site.ps1 -Production
    .\deploy-agent-site.ps1
    .\deploy-agent-site.ps1 -SkipBuild -Production
#>

param(
    [switch]$Production,
    [switch]$SkipBuild,
    [switch]$SetupOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# --- Configuration ---
$ProjectName   = "real-estate-star-agents"
$AccountId     = "7674efd9381763796f39ea67fe5e0505"
$CustomDomain  = "agents.real-estate-star.com"
$ApiUrl        = "https://api.real-estate-star.com"
$ScriptDir     = $PSScriptRoot
$RepoRoot      = (Resolve-Path (Join-Path $ScriptDir "..\..")).Path
$AppDir        = Join-Path $RepoRoot "apps\agent-site"
$DockerImage   = "node:22-slim"

# --- Helpers ---
function Write-Ok($Msg)   { Write-Host "    [OK]   $Msg" -ForegroundColor Green }
function Write-Fail($Msg) { Write-Host "    [FAIL] $Msg" -ForegroundColor Red }
function Write-Info($Msg)  { Write-Host "    [-->]  $Msg" -ForegroundColor Cyan }
function Write-Warn($Msg) { Write-Host "    [WARN] $Msg" -ForegroundColor Yellow }

function Test-CommandExists($Name) {
    $null = Get-Command $Name -ErrorAction SilentlyContinue
    return $?
}

Write-Host ""
Write-Host "========================================" -ForegroundColor White
Write-Host " Real Estate Star Agent Site -- Deploy" -ForegroundColor White
Write-Host "========================================" -ForegroundColor White

# --- Branch Guard: only deploy from main ---
$currentBranch = git -C $RepoRoot rev-parse --abbrev-ref HEAD 2>$null
if ($currentBranch -ne "main") {
    Write-Host ""
    Write-Fail "You are on branch '$currentBranch' -- production deploys must come from 'main'."
    Write-Fail "Merge your changes to main first, then re-run."
    exit 1
}
Write-Host ""
Write-Ok "On branch 'main'"

# --- Prerequisites ---
Write-Host ""
Write-Host "=== Prerequisites ===" -ForegroundColor White
$prereqFailed = $false

# Node.js + npm needed on host for wrangler deploy
if (Test-CommandExists "node") {
    $nodeVer = node --version
    Write-Ok "Node.js found ($nodeVer)"
} else {
    Write-Fail "Node.js not found. Install: https://nodejs.org"
    $prereqFailed = $true
}

if (Test-CommandExists "npm") {
    Write-Ok "npm found"
} else {
    Write-Fail "npm not found"
    $prereqFailed = $true
}

if (Test-CommandExists "npx") {
    Write-Ok "npx found"
} else {
    Write-Fail "npx not found"
    $prereqFailed = $true
}

# Docker is required for the build step (Linux container avoids Turbopack bracket issue)
if (-not $SkipBuild) {
    if (Test-CommandExists "docker") {
        Write-Ok "Docker CLI found"
        $savedEAP = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        $null = docker info 2>&1
        $dockerRunning = ($LASTEXITCODE -eq 0)
        $ErrorActionPreference = $savedEAP

        if ($dockerRunning) {
            Write-Ok "Docker daemon is running"
        } else {
            Write-Fail "Docker daemon not running. Start Docker Desktop and re-run."
            $prereqFailed = $true
        }
    } else {
        Write-Fail "Docker not found. Install Docker Desktop and re-run."
        Write-Host "    https://www.docker.com/products/docker-desktop/" -ForegroundColor Cyan
        $prereqFailed = $true
    }
}

if ($prereqFailed) { exit 1 }

# --- Verify app directory ---
if (-not (Test-Path (Join-Path $AppDir "package.json"))) {
    Write-Fail "Agent site app not found at $AppDir"
    exit 1
}
Write-Ok "Agent site app found at $AppDir"

# --- Ensure open-next.config.ts exists ---
$openNextConfig = Join-Path $AppDir "open-next.config.ts"
if (-not (Test-Path $openNextConfig)) {
    Write-Info "Creating open-next.config.ts..."
    $configContent = @"
import type { OpenNextConfig } from "@opennextjs/cloudflare";

const config: OpenNextConfig = {
  default: {
    override: {
      wrapper: "cloudflare-node",
      converter: "edge",
      proxyExternalRequest: "fetch",
      incrementalCache: "dummy",
      tagCache: "dummy",
      queue: "direct",
    },
  },
  edgeExternals: ["node:crypto"],
  middleware: {
    external: true,
    override: {
      wrapper: "cloudflare-edge",
      converter: "edge",
      proxyExternalRequest: "fetch",
      incrementalCache: "dummy",
      tagCache: "dummy",
      queue: "direct",
    },
  },
};

export default config;
"@
    Set-Content -Path $openNextConfig -Value $configContent -Encoding ASCII
    Write-Ok "open-next.config.ts created"
} else {
    Write-Ok "open-next.config.ts exists"
}

# --- Ensure wrangler.jsonc exists ---
$wranglerConfig = Join-Path $AppDir "wrangler.jsonc"
$wranglerToml = Join-Path $AppDir "wrangler.toml"
if (-not (Test-Path $wranglerConfig) -and -not (Test-Path $wranglerToml)) {
    Write-Info "Creating wrangler.jsonc..."
    $wranglerContent = @"
{
  "`$schema": "node_modules/wrangler/config-schema.json",
  "name": "$ProjectName",
  "main": ".open-next/worker.js",
  "compatibility_date": "2025-06-01",
  "compatibility_flags": ["nodejs_compat"],
  "assets": {
    "directory": ".open-next/assets",
    "binding": "ASSETS"
  },
  "services": [
    {
      "binding": "WORKER_SELF_REFERENCE",
      "service": "$ProjectName"
    }
  ]
}
"@
    Set-Content -Path $wranglerConfig -Value $wranglerContent -Encoding ASCII
    Write-Ok "wrangler.jsonc created"
} else {
    Write-Ok "wrangler config exists"
}

if ($SetupOnly) {
    Write-Host ""
    Write-Host "=== Setup Complete ===" -ForegroundColor White
    Write-Info "Run without -SetupOnly to build and deploy"
    exit 0
}

# --- Build via Docker ---
if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "=== Build (Docker: $DockerImage) ===" -ForegroundColor White
    Write-Info "Building inside Linux container to avoid Turbopack bracket issue on Windows"
    Write-Info "Config directory will be mounted read-only for prebuild agent config access"

    # Create production .env for build
    # NEXT_PUBLIC_* vars are baked in at build time -- must match deploy-agent-site.yml
    $envFile = Join-Path $AppDir ".env.production"
    $envLines = @(
        "NEXT_PUBLIC_API_URL=$ApiUrl"
    )
    Set-Content -Path $envFile -Value ($envLines -join "`n") -Encoding ASCII
    Write-Ok "Created .env.production (API_URL=$ApiUrl)"

    # Temporarily rename .env.local so it does not override .env.production during build
    $envLocal = Join-Path $AppDir ".env.local"
    $envLocalBak = Join-Path $AppDir ".env.local.bak"
    $movedEnvLocal = $false
    if (Test-Path $envLocal) {
        Move-Item -Path $envLocal -Destination $envLocalBak -Force
        $movedEnvLocal = $true
        Write-Info "Moved .env.local aside (production build must use .env.production)"
    }

    # Clean previous build output (Docker will create fresh)
    $openNextDir = Join-Path $AppDir ".open-next"
    if (Test-Path $openNextDir) {
        Remove-Item -Path $openNextDir -Recurse -Force
        Write-Info "Cleaned previous .open-next output"
    }

    # Convert Windows paths to Docker-compatible paths
    # Docker Desktop on Windows accepts forward-slash paths from the drive root
    $dockerAppDir = $AppDir -replace '\\', '/'
    if ($dockerAppDir -match '^([A-Za-z]):(.*)') {
        $driveLetter = $Matches[1].ToLower()
        $restOfPath = $Matches[2]
        $dockerAppDir = "/$driveLetter$restOfPath"
    }

    $dockerRepoRoot = $RepoRoot -replace '\\', '/'
    if ($dockerRepoRoot -match '^([A-Za-z]):(.*)') {
        $driveLetter = $Matches[1].ToLower()
        $restOfPath = $Matches[2]
        $dockerRepoRoot = "/$driveLetter$restOfPath"
    }

    Write-Info "Mounting $dockerAppDir into container..."
    Write-Info "Mounting ${dockerRepoRoot}/config into container (read-only)..."
    Write-Info "Running: node scripts/generate-config-registry.mjs && npm install && npx opennextjs-cloudflare build"
    Write-Info "This may take 2-3 minutes on first run (downloading image + npm install)..."

    $savedEAP = $ErrorActionPreference
    $ErrorActionPreference = "Continue"

    docker run --rm `
        -v "${dockerAppDir}:/app" `
        -v "${dockerRepoRoot}/config:/repo/config:ro" `
        -w /app `
        -e "NEXT_PUBLIC_API_URL=$ApiUrl" `
        $DockerImage `
        sh -c "node scripts/generate-config-registry.mjs && npm install && npx opennextjs-cloudflare build" 2>&1 | ForEach-Object { Write-Host "    $_" }

    $buildExitCode = $LASTEXITCODE
    $ErrorActionPreference = $savedEAP

    # Restore .env.local regardless of build outcome
    if ($movedEnvLocal -and (Test-Path $envLocalBak)) {
        Move-Item -Path $envLocalBak -Destination $envLocal -Force
        Write-Info "Restored .env.local"
    }

    if ($buildExitCode -ne 0) {
        Write-Fail "Docker build failed (exit code: $buildExitCode)"
        exit 1
    }
    Write-Ok "Build complete"
}

# --- Verify build output ---
$workerJs = Join-Path $AppDir ".open-next\worker.js"
if (-not (Test-Path $workerJs)) {
    Write-Fail "Build output not found at .open-next/worker.js"
    Write-Info "Run without -SkipBuild to build first"
    exit 1
}
Write-Ok "Build output verified (.open-next/worker.js exists)"

# --- Ensure wrangler is installed on host (needed for deploy) ---
Write-Host ""
Write-Host "=== Wrangler (host) ===" -ForegroundColor White

$hasWrangler = Test-Path (Join-Path $AppDir "node_modules\.bin\wrangler.cmd")
if (-not $hasWrangler) {
    Write-Info "Installing wrangler on host for deploy..."
    Push-Location $AppDir
    try {
        $savedEAP = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        $null = npm install -D wrangler 2>&1
        $npmExit = $LASTEXITCODE
        $ErrorActionPreference = $savedEAP

        if ($npmExit -ne 0) {
            Write-Fail "Failed to install wrangler"
            exit 1
        }
        Write-Ok "wrangler installed"
    } finally {
        Pop-Location
    }
} else {
    Write-Ok "wrangler already installed"
}

# --- Authenticate with Cloudflare ---
Write-Host ""
Write-Host "=== Cloudflare Authentication ===" -ForegroundColor White

# OpenNext's deploy spawns wrangler in a non-interactive child process.
# OAuth login does NOT work for this -- CLOUDFLARE_API_TOKEN is required.
if ($env:CLOUDFLARE_API_TOKEN) {
    Write-Ok "CLOUDFLARE_API_TOKEN set"
} else {
    Write-Warn "CLOUDFLARE_API_TOKEN not set"
    Write-Host ""
    Write-Host "  OpenNext deploys require an API token (OAuth login won't work)." -ForegroundColor Yellow
    Write-Host "  Create one at: https://dash.cloudflare.com/profile/api-tokens" -ForegroundColor Yellow
    Write-Host "  Use Custom Token with: Pages Edit, Workers Edit, Account Settings Read." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  To skip this prompt next time, set it permanently:" -ForegroundColor Yellow
    Write-Host "    [Environment]::SetEnvironmentVariable('CLOUDFLARE_API_TOKEN', 'your-token', 'User')" -ForegroundColor Yellow
    Write-Host ""

    $tokenInput = Read-Host -Prompt "  Paste your Cloudflare API token (or press Enter to exit)"
    if ([string]::IsNullOrWhiteSpace($tokenInput)) {
        exit 1
    }
    $env:CLOUDFLARE_API_TOKEN = $tokenInput.Trim()
    Write-Ok "CLOUDFLARE_API_TOKEN set for this session"
}

# Verify the token works
Push-Location $AppDir
try {
    $savedEAP = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $null = npx wrangler whoami 2>&1
    $whoamiExit = $LASTEXITCODE
    $ErrorActionPreference = $savedEAP

    if ($whoamiExit -ne 0) {
        Write-Fail "Cloudflare authentication failed -- check your token"
        exit 1
    }
    Write-Ok "Cloudflare authentication verified"
} finally {
    Pop-Location
}

# --- Deploy ---
Write-Host ""
Write-Host "=== Deploy to Cloudflare Pages ===" -ForegroundColor White

$envLabel = if ($Production) { "production" } else { "preview" }
Write-Info "Deploying to $envLabel..."

Push-Location $AppDir
try {
    $savedEAP = $ErrorActionPreference
    $ErrorActionPreference = "Continue"

    # Workers deployments don't use --env (that's a Pages concept).
    # Preview vs production is just about which custom domain is mapped.
    npx wrangler deploy 2>&1 | ForEach-Object { Write-Host "    $_" }
    $deployExitCode = $LASTEXITCODE
    $ErrorActionPreference = $savedEAP

    if ($deployExitCode -ne 0) {
        Write-Fail "wrangler deploy failed (exit code: $deployExitCode)"
        exit 1
    }
    Write-Ok "Deployed to $envLabel"
} finally {
    Pop-Location
}

# --- Post-deploy info ---
Write-Host ""
Write-Host "=== Deploy Complete ===" -ForegroundColor White
Write-Info "Project: $ProjectName"

if ($Production) {
    Write-Info "URL: https://$CustomDomain"
    Write-Host ""
    Write-Host "  If this is the first deploy, set up the custom domain:" -ForegroundColor Yellow
    Write-Host "    1. Go to Cloudflare dashboard > Workers & Pages > $ProjectName" -ForegroundColor Yellow
    Write-Host "    2. Click Custom domains > Set up a custom domain" -ForegroundColor Yellow
    Write-Host "    3. Enter: {slug}.agents.real-estate-star.com (wildcard via DNS)" -ForegroundColor Yellow
    Write-Host "    4. Also set up: $CustomDomain as the primary workers route" -ForegroundColor Yellow
} else {
    Write-Info "Preview URL will be shown in the wrangler output above"
    Write-Info "Deploy to production with: .\deploy-agent-site.ps1 -Production"
}

Write-Host ""

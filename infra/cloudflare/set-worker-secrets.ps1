#Requires -Version 5.1
<#
.SYNOPSIS
    Set runtime secrets on the Cloudflare Worker for the agent site.

.DESCRIPTION
    These secrets are NOT stored in wrangler.jsonc (they'd be plaintext).
    They must be set via `wrangler secret put` or `wrangler secret bulk`.

    Prompts for each secret value interactively. Press Enter to skip (keeps
    the current value on the worker).

.PARAMETER FromEnv
    Read secret values from environment variables instead of prompting.

.EXAMPLE
    .\set-worker-secrets.ps1
    .\set-worker-secrets.ps1 -FromEnv
#>

param(
    [switch]$FromEnv
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptDir = $PSScriptRoot
$RepoRoot  = (Resolve-Path (Join-Path $ScriptDir "..\..")).Path
$AppDir    = Join-Path $RepoRoot "apps\agent-site"

# Secrets to configure (name : display name)
$Secrets = @(
    @{ Name = "TURNSTILE_SECRET_KEY"; Display = "Cloudflare Turnstile Secret Key" }
    @{ Name = "LEAD_API_KEY";         Display = "Lead API Key" }
    @{ Name = "LEAD_HMAC_SECRET";     Display = "Lead HMAC Secret" }
    @{ Name = "LEAD_API_URL";         Display = "Lead API URL" }
)

# --- Helpers ---
function Write-Ok($Msg)   { Write-Host "  [OK]   $Msg" -ForegroundColor Green }
function Write-Fail($Msg) { Write-Host "  [FAIL] $Msg" -ForegroundColor Red }
function Write-Info($Msg)  { Write-Host "  [-->]  $Msg" -ForegroundColor Cyan }

Write-Host ""
Write-Host "============================================================================" -ForegroundColor White
Write-Host "  Real Estate Star -- Set Cloudflare Worker Secrets" -ForegroundColor White
Write-Host "============================================================================" -ForegroundColor White
Write-Host ""

# --- Preflight ---
if (-not (Get-Command npx -ErrorAction SilentlyContinue)) {
    Write-Fail "npx not found. Install Node.js first."
    exit 1
}

Write-Host "Verifying Cloudflare authentication..."
Push-Location $AppDir
try {
    $savedEAP = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $null = npx wrangler whoami 2>&1
    $whoamiExit = $LASTEXITCODE
    $ErrorActionPreference = $savedEAP

    if ($whoamiExit -ne 0) {
        Write-Fail "Cloudflare authentication failed."
        Write-Info "Set CLOUDFLARE_API_TOKEN or run: npx wrangler login"
        exit 1
    }
    Write-Ok "Authenticated"
} finally {
    Pop-Location
}

Write-Host ""

# --- Collect secrets ---
$secretMap = @{}
$skipped = 0

if ($FromEnv) {
    Write-Host "Reading secrets from environment variables..."
    Write-Host ""
}

foreach ($secret in $Secrets) {
    $name = $secret.Name
    $display = $secret.Display

    if ($FromEnv) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if ([string]::IsNullOrWhiteSpace($value)) {
            Write-Host "  [SKIP] $name not set in environment"
            $skipped++
            continue
        }
        Write-Ok "$name (from env)"
    } else {
        $secureValue = Read-Host -Prompt "  $display ($name)" -AsSecureString
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureValue)
        $value = [Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)

        if ([string]::IsNullOrWhiteSpace($value)) {
            Write-Host "    -> Skipped (empty). Secret keeps its current value."
            $skipped++
            continue
        }
    }

    $secretMap[$name] = $value
}

Write-Host ""

if ($secretMap.Count -eq 0) {
    Write-Host "No secrets to update. Exiting."
    exit 0
}

# --- Build JSON and apply ---
Write-Info "Setting $($secretMap.Count) secret(s) on worker..."

$jsonParts = @()
foreach ($key in $secretMap.Keys) {
    $escapedValue = $secretMap[$key] -replace '"', '\"'
    $jsonParts += "`"$key`":`"$escapedValue`""
}
$json = "{" + ($jsonParts -join ",") + "}"

Push-Location $AppDir
try {
    $json | npx wrangler secret bulk 2>&1 | ForEach-Object { Write-Host "    $_" }
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "wrangler secret bulk failed"
        exit 1
    }
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "============================================================================" -ForegroundColor White
Write-Host "  Secrets updated: $($secretMap.Count)" -ForegroundColor White
Write-Host "  Secrets skipped: $skipped" -ForegroundColor White
Write-Host "  Worker will use new values on next request (no restart needed)." -ForegroundColor White
Write-Host "============================================================================" -ForegroundColor White
Write-Host ""

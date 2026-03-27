#Requires -Version 5.1
<#
.SYNOPSIS
    Set all 18 GitHub Actions secrets for Real Estate Star.

.DESCRIPTION
    Fills in all repository secrets required by the CI/CD workflows.
    Edit the values section at the top of this file, then run the script.
    No interactive prompts — all values must be set before running.

    Secrets are piped to `gh secret set` so they never appear as
    command-line arguments (avoids process-listing exposure).

    Leave a value empty to skip that secret (existing value is kept).
    Script exits with code 1 if any non-empty secret fails to set.

.NOTES
    Prerequisites:
      - GitHub CLI (gh): https://cli.github.com
      - Authenticated:   gh auth login

.EXAMPLE
    .\set-all-secrets.ps1
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ============================================================================
#  FILL IN YOUR VALUES BEFORE RUNNING
# ============================================================================

# --- Shared / Cloudflare (deploy-agent-site.yml, deploy-platform.yml) -------
$CLOUDFLARE_API_TOKEN            = ""   # Cloudflare API token with Pages+Workers edit
$CLOUDFLARE_ACCOUNT_ID           = ""   # Cloudflare account ID (dashboard → right sidebar)

# --- Agent Site (deploy-agent-site.yml, Cloudflare Worker runtime) ----------
$API_URL                         = ""   # https://api.real-estate-star.com
$GOOGLE_MAPS_API_KEY             = ""   # Google Maps JavaScript API key
$TURNSTILE_SITE_KEY              = ""   # Cloudflare Turnstile site key (public)
$TURNSTILE_SECRET_KEY            = ""   # Cloudflare Turnstile secret key (server-side)
$LEAD_API_KEY                    = ""   # Per-agent API key for lead submission endpoint
$LEAD_HMAC_SECRET                = ""   # HMAC signing secret for lead submission requests
$LEAD_API_URL                    = ""   # https://api.real-estate-star.com

# --- Platform (deploy-platform.yml) -----------------------------------------
$STRIPE_PUBLISHABLE_KEY          = ""   # Stripe publishable key (pk_live_... or pk_test_...)

# --- API / Azure (deploy-api.yml) --------------------------------------------
$AZURE_CREDENTIALS               = ""   # JSON from: az ad sp create-for-rbac --sdk-auth
$AZURE_STORAGE_CONNECTION_STRING = ""   # Azure Table Storage connection string (WhatsApp state)
$INTERNAL_API_TOKEN              = ""   # Bearer token for internal service-to-service calls

# --- WhatsApp (deploy-api.yml, Workers.WhatsApp) ----------------------------
$WHATSAPP_PHONE_NUMBER_ID        = ""   # WhatsApp Business phone number ID
$WHATSAPP_ACCESS_TOKEN           = ""   # WhatsApp Business access token
$WHATSAPP_APP_SECRET             = ""   # WhatsApp App Secret (webhook signature verification)
$WHATSAPP_VERIFY_TOKEN           = ""   # Webhook verify token (set during webhook registration)
$WHATSAPP_WABA_ID                = ""   # WhatsApp Business Account (WABA) ID

# ============================================================================

# --- Helpers ---
function Write-Ok($Msg)   { Write-Host "  [OK]   $Msg" -ForegroundColor Green }
function Write-Fail($Msg) { Write-Host "  [FAIL] $Msg" -ForegroundColor Red }
function Write-Info($Msg) { Write-Host "  [-->]  $Msg" -ForegroundColor Cyan }
function Write-Warn($Msg) { Write-Host "  [WARN] $Msg" -ForegroundColor Yellow }
function Write-Skip($Msg) { Write-Host "  [SKIP] $Msg" -ForegroundColor DarkGray }

Write-Host ""
Write-Host "============================================================================" -ForegroundColor White
Write-Host "  Real Estate Star — Set GitHub Actions Secrets" -ForegroundColor White
Write-Host "============================================================================" -ForegroundColor White
Write-Host ""

# --- Preflight ---
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Fail "GitHub CLI (gh) not found. Install: https://cli.github.com"
    exit 1
}

Write-Host "Verifying GitHub authentication..."
$savedEAP = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$null = gh auth status 2>&1
$authOk = ($LASTEXITCODE -eq 0)
$ErrorActionPreference = $savedEAP

if (-not $authOk) {
    Write-Fail "Not authenticated with GitHub CLI."
    Write-Info "Run: gh auth login"
    exit 1
}
Write-Ok "Authenticated"
Write-Host ""

# --- Secrets table ---
$Secrets = @(
    # Shared / Cloudflare
    @{ Name = "CLOUDFLARE_API_TOKEN";            Value = $CLOUDFLARE_API_TOKEN;            Group = "Shared / Cloudflare" }
    @{ Name = "CLOUDFLARE_ACCOUNT_ID";           Value = $CLOUDFLARE_ACCOUNT_ID;           Group = "Shared / Cloudflare" }
    # Agent Site
    @{ Name = "API_URL";                         Value = $API_URL;                         Group = "Agent Site" }
    @{ Name = "GOOGLE_MAPS_API_KEY";             Value = $GOOGLE_MAPS_API_KEY;             Group = "Agent Site" }
    @{ Name = "TURNSTILE_SITE_KEY";              Value = $TURNSTILE_SITE_KEY;              Group = "Agent Site" }
    @{ Name = "TURNSTILE_SECRET_KEY";            Value = $TURNSTILE_SECRET_KEY;            Group = "Agent Site" }
    @{ Name = "LEAD_API_KEY";                    Value = $LEAD_API_KEY;                    Group = "Agent Site" }
    @{ Name = "LEAD_HMAC_SECRET";                Value = $LEAD_HMAC_SECRET;                Group = "Agent Site" }
    @{ Name = "LEAD_API_URL";                    Value = $LEAD_API_URL;                    Group = "Agent Site" }
    # Platform
    @{ Name = "STRIPE_PUBLISHABLE_KEY";          Value = $STRIPE_PUBLISHABLE_KEY;          Group = "Platform" }
    # API / Azure
    @{ Name = "AZURE_CREDENTIALS";               Value = $AZURE_CREDENTIALS;               Group = "API / Azure" }
    @{ Name = "AZURE_STORAGE_CONNECTION_STRING"; Value = $AZURE_STORAGE_CONNECTION_STRING; Group = "API / Azure" }
    @{ Name = "INTERNAL_API_TOKEN";              Value = $INTERNAL_API_TOKEN;              Group = "API / Azure" }
    # WhatsApp
    @{ Name = "WHATSAPP_PHONE_NUMBER_ID";        Value = $WHATSAPP_PHONE_NUMBER_ID;        Group = "WhatsApp" }
    @{ Name = "WHATSAPP_ACCESS_TOKEN";           Value = $WHATSAPP_ACCESS_TOKEN;           Group = "WhatsApp" }
    @{ Name = "WHATSAPP_APP_SECRET";             Value = $WHATSAPP_APP_SECRET;             Group = "WhatsApp" }
    @{ Name = "WHATSAPP_VERIFY_TOKEN";           Value = $WHATSAPP_VERIFY_TOKEN;           Group = "WhatsApp" }
    @{ Name = "WHATSAPP_WABA_ID";               Value = $WHATSAPP_WABA_ID;               Group = "WhatsApp" }
)

# --- Validation ---
Write-Host "=== Validation ===" -ForegroundColor White
$emptyNames = @()
foreach ($s in $Secrets) {
    if ([string]::IsNullOrWhiteSpace($s.Value)) {
        Write-Warn "$($s.Name) is empty — will be skipped"
        $emptyNames += $s.Name
    }
}

if ($emptyNames.Count -gt 0) {
    Write-Host ""
    Write-Warn "$($emptyNames.Count) of $($Secrets.Count) secret(s) are empty and will be skipped."
    Write-Warn "Fill in the missing values at the top of this script and re-run to set them."
    Write-Host ""
} else {
    Write-Ok "All $($Secrets.Count) secrets have values"
}

Write-Host ""

# --- Set secrets ---
Write-Host "=== Setting Secrets ===" -ForegroundColor White
Write-Host ""

$setCount     = 0
$skippedCount = 0
$failedCount  = 0
$currentGroup = ""

foreach ($s in $Secrets) {
    if ($s.Group -ne $currentGroup) {
        $currentGroup = $s.Group
        Write-Host "  # $currentGroup" -ForegroundColor DarkGray
    }

    if ([string]::IsNullOrWhiteSpace($s.Value)) {
        Write-Skip $s.Name
        $skippedCount++
        continue
    }

    $savedEAP = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $s.Value | gh secret set $s.Name 2>&1 | Out-Null
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = $savedEAP

    if ($exitCode -eq 0) {
        Write-Ok $s.Name
        $setCount++
    } else {
        Write-Fail "$($s.Name) — gh secret set exited $exitCode"
        $failedCount++
    }
}

# --- Summary ---
Write-Host ""
Write-Host "============================================================================" -ForegroundColor White
Write-Host "  Secrets set:     $setCount" -ForegroundColor $(if ($setCount -gt 0) { "Green" } else { "White" })
Write-Host "  Secrets skipped: $skippedCount" -ForegroundColor $(if ($skippedCount -gt 0) { "Yellow" } else { "White" })
Write-Host "  Secrets failed:  $failedCount" -ForegroundColor $(if ($failedCount -gt 0) { "Red" } else { "White" })
Write-Host "============================================================================" -ForegroundColor White
Write-Host ""

if ($skippedCount -gt 0) {
    Write-Info "Skipped secrets keep their current value in GitHub. Fill in the"
    Write-Info "missing values above and re-run to set them."
    Write-Host ""
}

if ($failedCount -gt 0) {
    exit 1
}

#Requires -Version 5.1
<#
.SYNOPSIS
    Interactive setup wizard for Cloudflare Worker secrets + GitHub secrets.

.DESCRIPTION
    Walks through generating/collecting all required secrets, sets them on the
    Cloudflare Worker, and optionally sets the matching GitHub Actions secrets.

.EXAMPLE
    .\setup-worker-secrets.ps1
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptDir = $PSScriptRoot
$RepoRoot  = (Resolve-Path (Join-Path $ScriptDir "..\..")).Path
$AppDir    = Join-Path $RepoRoot "apps\agent-site"

function Write-Ok($Msg)   { Write-Host "  [OK]   $Msg" -ForegroundColor Green }
function Write-Info($Msg)  { Write-Host "  [-->]  $Msg" -ForegroundColor Cyan }
function Write-Warn($Msg) { Write-Host "  [WARN] $Msg" -ForegroundColor Yellow }
function Write-Step($Num, $Msg) { Write-Host "`n=== Step $Num: $Msg ===" -ForegroundColor White }

Write-Host ""
Write-Host "============================================================================" -ForegroundColor White
Write-Host "  Real Estate Star — Worker Secrets Setup Wizard" -ForegroundColor White
Write-Host "============================================================================" -ForegroundColor White
Write-Host ""
Write-Host "  This wizard sets up 4 runtime secrets on the Cloudflare Worker"
Write-Host "  and optionally saves them as GitHub Actions secrets."
Write-Host ""

# --- Preflight ---
$ghAvailable = $null -ne (Get-Command gh -ErrorAction SilentlyContinue)
$npxAvailable = $null -ne (Get-Command npx -ErrorAction SilentlyContinue)

if (-not $npxAvailable) {
    Write-Host "  ERROR: npx not found. Install Node.js first." -ForegroundColor Red
    exit 1
}

# ============================================================================
# STEP 1: Turnstile
# ============================================================================
Write-Step 1 "Cloudflare Turnstile (CAPTCHA)"

Write-Host ""
Write-Host "  Get these from: https://dash.cloudflare.com > Turnstile" -ForegroundColor Yellow
Write-Host "  If you haven't created a Turnstile widget yet:" -ForegroundColor Yellow
Write-Host "    1. Click 'Add site'" -ForegroundColor Yellow
Write-Host "    2. Site name: 'Real Estate Star Agent Sites'" -ForegroundColor Yellow
Write-Host "    3. Domain: real-estate-star.com" -ForegroundColor Yellow
Write-Host "    4. Widget mode: Managed" -ForegroundColor Yellow
Write-Host ""

$turnstileSecret = Read-Host -Prompt "  Turnstile SECRET Key (starts with 0x)"
$turnstileSite   = Read-Host -Prompt "  Turnstile SITE Key (starts with 0x)"

if ([string]::IsNullOrWhiteSpace($turnstileSecret)) {
    Write-Warn "Turnstile secret key is empty — Turnstile verification will fail"
}
if ([string]::IsNullOrWhiteSpace($turnstileSite)) {
    Write-Warn "Turnstile site key is empty — widget won't render on forms"
}

# ============================================================================
# STEP 2: Lead API Key
# ============================================================================
Write-Step 2 "Lead API Key"

Write-Host ""
Write-Host "  This identifies the agent site when calling the .NET Lead API." -ForegroundColor Yellow
Write-Host "  If you already have one, paste it. Otherwise, we'll generate one." -ForegroundColor Yellow
Write-Host ""

$leadApiKey = Read-Host -Prompt "  Lead API Key (press Enter to auto-generate)"

if ([string]::IsNullOrWhiteSpace($leadApiKey)) {
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $leadApiKey = -join ($bytes | ForEach-Object { $_.ToString("x2") })
    Write-Ok "Generated: $leadApiKey"
    Write-Host ""
    Write-Host "  IMPORTANT: Save this value! You also need it in the .NET API config:" -ForegroundColor Yellow
    Write-Host "    Hmac:ApiKeys:jenise-buckalew = $leadApiKey" -ForegroundColor Yellow
}

# ============================================================================
# STEP 3: HMAC Secret
# ============================================================================
Write-Step 3 "Lead HMAC Secret"

Write-Host ""
Write-Host "  Shared secret for request signing between Worker and API." -ForegroundColor Yellow
Write-Host "  If you already have one, paste it. Otherwise, we'll generate one." -ForegroundColor Yellow
Write-Host ""

$leadHmacSecret = Read-Host -Prompt "  HMAC Secret (press Enter to auto-generate)"

if ([string]::IsNullOrWhiteSpace($leadHmacSecret)) {
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $leadHmacSecret = -join ($bytes | ForEach-Object { $_.ToString("x2") })
    Write-Ok "Generated: $leadHmacSecret"
    Write-Host ""
    Write-Host "  IMPORTANT: Save this value! You also need it in the .NET API config:" -ForegroundColor Yellow
    Write-Host "    Hmac:HmacSecret = $leadHmacSecret" -ForegroundColor Yellow
}

# ============================================================================
# STEP 4: Lead API URL
# ============================================================================
Write-Step 4 "Lead API URL"

Write-Host ""
$leadApiUrl = Read-Host -Prompt "  Lead API URL [https://api.real-estate-star.com]"
if ([string]::IsNullOrWhiteSpace($leadApiUrl)) {
    $leadApiUrl = "https://api.real-estate-star.com"
}
Write-Ok "Using: $leadApiUrl"

# ============================================================================
# SUMMARY
# ============================================================================
Write-Host ""
Write-Host "============================================================================" -ForegroundColor White
Write-Host "  Summary" -ForegroundColor White
Write-Host "============================================================================" -ForegroundColor White
Write-Host ""
Write-Host "  TURNSTILE_SECRET_KEY : $(if ($turnstileSecret) { $turnstileSecret.Substring(0, [Math]::Min(8, $turnstileSecret.Length)) + '...' } else { '(empty)' })"
Write-Host "  TURNSTILE_SITE_KEY   : $(if ($turnstileSite) { $turnstileSite.Substring(0, [Math]::Min(8, $turnstileSite.Length)) + '...' } else { '(empty)' })"
Write-Host "  LEAD_API_KEY         : $($leadApiKey.Substring(0, [Math]::Min(8, $leadApiKey.Length)))..."
Write-Host "  LEAD_HMAC_SECRET     : $($leadHmacSecret.Substring(0, [Math]::Min(8, $leadHmacSecret.Length)))..."
Write-Host "  LEAD_API_URL         : $leadApiUrl"
Write-Host ""

$confirm = Read-Host -Prompt "  Apply these secrets? (y/N)"
if ($confirm -ne "y") {
    Write-Host "  Aborted." -ForegroundColor Yellow
    exit 0
}

# ============================================================================
# APPLY: Cloudflare Worker
# ============================================================================
Write-Host ""
Write-Host "=== Applying to Cloudflare Worker ===" -ForegroundColor White

$jsonParts = @()
if ($turnstileSecret) { $jsonParts += "`"TURNSTILE_SECRET_KEY`":`"$($turnstileSecret -replace '"', '\"')`"" }
if ($leadApiKey)      { $jsonParts += "`"LEAD_API_KEY`":`"$($leadApiKey -replace '"', '\"')`"" }
if ($leadHmacSecret)  { $jsonParts += "`"LEAD_HMAC_SECRET`":`"$($leadHmacSecret -replace '"', '\"')`"" }
if ($leadApiUrl)      { $jsonParts += "`"LEAD_API_URL`":`"$($leadApiUrl -replace '"', '\"')`"" }

if ($jsonParts.Count -gt 0) {
    $json = "{" + ($jsonParts -join ",") + "}"

    Push-Location $AppDir
    try {
        $savedEAP = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        $json | npx wrangler secret bulk 2>&1 | ForEach-Object { Write-Host "    $_" }
        $exitCode = $LASTEXITCODE
        $ErrorActionPreference = $savedEAP

        if ($exitCode -ne 0) {
            Write-Host "  [FAIL] wrangler secret bulk failed" -ForegroundColor Red
            Write-Host "  Make sure CLOUDFLARE_API_TOKEN is set or run 'npx wrangler login' first" -ForegroundColor Yellow
        } else {
            Write-Ok "$($jsonParts.Count) secret(s) set on Cloudflare Worker"
        }
    } finally {
        Pop-Location
    }
} else {
    Write-Warn "No secrets to set on Worker (all values empty)"
}

# ============================================================================
# APPLY: GitHub Secrets
# ============================================================================
if ($ghAvailable) {
    Write-Host ""
    $setGh = Read-Host -Prompt "  Also set these as GitHub Actions secrets? (y/N)"

    if ($setGh -eq "y") {
        Write-Host ""
        Write-Host "=== Setting GitHub Secrets ===" -ForegroundColor White

        $ghSecrets = @{
            "TURNSTILE_SECRET_KEY" = $turnstileSecret
            "TURNSTILE_SITE_KEY"   = $turnstileSite
            "LEAD_API_KEY"         = $leadApiKey
            "LEAD_HMAC_SECRET"     = $leadHmacSecret
            "LEAD_API_URL"         = $leadApiUrl
        }

        foreach ($name in $ghSecrets.Keys) {
            $value = $ghSecrets[$name]
            if ([string]::IsNullOrWhiteSpace($value)) {
                Write-Warn "Skipping $name (empty)"
                continue
            }

            $savedEAP = $ErrorActionPreference
            $ErrorActionPreference = "Continue"
            $value | gh secret set $name --repo edward-rosado/jenisesells-website 2>&1 | Out-Null
            $ghExit = $LASTEXITCODE
            $ErrorActionPreference = $savedEAP

            if ($ghExit -eq 0) {
                Write-Ok "$name"
            } else {
                Write-Host "  [FAIL] $name" -ForegroundColor Red
            }
        }
    }
} else {
    Write-Host ""
    Write-Warn "GitHub CLI (gh) not found — set GitHub secrets manually:"
    Write-Host "    gh secret set TURNSTILE_SECRET_KEY --body '...'" -ForegroundColor Yellow
    Write-Host "    gh secret set TURNSTILE_SITE_KEY --body '...'" -ForegroundColor Yellow
    Write-Host "    gh secret set LEAD_API_KEY --body '...'" -ForegroundColor Yellow
    Write-Host "    gh secret set LEAD_HMAC_SECRET --body '...'" -ForegroundColor Yellow
    Write-Host "    gh secret set LEAD_API_URL --body '...'" -ForegroundColor Yellow
}

# ============================================================================
# NEXT STEPS
# ============================================================================
Write-Host ""
Write-Host "============================================================================" -ForegroundColor White
Write-Host "  Next Steps" -ForegroundColor White
Write-Host "============================================================================" -ForegroundColor White
Write-Host ""
Write-Host "  1. Set the same LEAD_API_KEY and LEAD_HMAC_SECRET in the .NET API:" -ForegroundColor Cyan
Write-Host "     - Azure: infra/azure/set-secrets.sh" -ForegroundColor Cyan
Write-Host "     - Local: apps/api/RealEstateStar.Api/appsettings.Development.json" -ForegroundColor Cyan
Write-Host ""
Write-Host "  2. Verify the worker has the secrets:" -ForegroundColor Cyan
Write-Host "     cd apps/agent-site && npx wrangler secret list" -ForegroundColor Cyan
Write-Host ""
Write-Host "  3. Test the lead form on production" -ForegroundColor Cyan
Write-Host ""

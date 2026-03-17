#Requires -Version 5.1
<#
.SYNOPSIS
    Post-deployment verification for Real Estate Star

.DESCRIPTION
    Run after every production deployment to verify everything is working.
    Can target Azure URL or custom domain.

.PARAMETER Domain
    Custom domain to verify (e.g., real-estate-star.com). If omitted, uses Azure FQDN.

.EXAMPLE
    .\infra\verify-live.ps1                               # Uses Azure FQDN
    .\infra\verify-live.ps1 -Domain real-estate-star.com    # Uses custom domain
#>

param(
    [string]$Domain = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# --- Configuration -----------------------------------------------------------
$ResourceGroup = "real-estate-star-rg"
$AppName       = "real-estate-star-api"
$Timeout       = 10

$script:Pass = 0
$script:Fail = 0
$script:Warnings = 0

function Write-Ok($Msg)   { Write-Host "    [OK]   $Msg" -ForegroundColor Green; $script:Pass++ }
function Write-Warn($Msg) { Write-Host "    [WARN] $Msg" -ForegroundColor Yellow; $script:Warnings++ }
function Write-Fail($Msg) { Write-Host "    [FAIL] $Msg" -ForegroundColor Red; $script:Fail++ }
function Write-Info($Msg)  { Write-Host "    [-->]  $Msg" -ForegroundColor Cyan }

# --- Prerequisites -----------------------------------------------------------
$azPath = Get-Command az -ErrorAction SilentlyContinue
if (-not $azPath) {
    Write-Host ""
    Write-Host "    [FAIL] Azure CLI (az) not found!" -ForegroundColor Red
    Write-Host ""
    $hasWinget = Get-Command winget -ErrorAction SilentlyContinue
    if ($hasWinget) {
        Write-Host "    Attempting auto-install..." -ForegroundColor Cyan
        winget install --id Microsoft.AzureCLI --accept-package-agreements --accept-source-agreements
        # Refresh PATH
        $machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
        $userPath    = [Environment]::GetEnvironmentVariable("Path", "User")
        $env:Path = "$machinePath;$userPath"
        $azPath = Get-Command az -ErrorAction SilentlyContinue
        if (-not $azPath) {
            Write-Host "    Installed but not on PATH. Close and reopen PowerShell, then re-run." -ForegroundColor Red
            exit 1
        }
        Write-Host "    [OK] Azure CLI installed!" -ForegroundColor Green
    } else {
        Write-Host "    Install it: winget install Microsoft.AzureCLI" -ForegroundColor Cyan
        Write-Host "    Or download: https://aka.ms/installazurecliwindows" -ForegroundColor Cyan
        exit 1
    }
    Write-Host ""
}

# --- Determine URLs ----------------------------------------------------------
if ($Domain) {
    $ApiBase      = "https://api.$Domain"
    $PlatformBase = "https://platform.$Domain"
} else {
    try {
        $fqdn = az containerapp show `
            --name $AppName `
            --resource-group $ResourceGroup `
            --query "properties.configuration.ingress.fqdn" `
            --output tsv 2>$null
        if (-not $fqdn) { throw "empty" }
        $ApiBase = "https://$fqdn"
        $PlatformBase = ""
    } catch {
        Write-Host "  ERROR: Could not determine API URL. Are you logged into Azure?" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "  ================================================================" -ForegroundColor Cyan
Write-Host "    Real Estate Star -- Production Verification" -ForegroundColor Cyan
Write-Host "  ================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "    API:      $ApiBase"
if ($PlatformBase) { Write-Host "    Platform: $PlatformBase" }
Write-Host ""

# --- Health Endpoints --------------------------------------------------------
Write-Host "  Health Endpoints" -ForegroundColor White
Write-Host ""

try {
    $r = Invoke-WebRequest -Uri "$ApiBase/health/live" -TimeoutSec $Timeout -UseBasicParsing
    if ($r.StatusCode -eq 200) { Write-Ok "Liveness: $ApiBase/health/live -> 200" }
    else { Write-Fail "Liveness: -> $($r.StatusCode)" }
} catch { Write-Fail "Liveness: -> unreachable" }

try {
    $r = Invoke-WebRequest -Uri "$ApiBase/health/ready" -TimeoutSec $Timeout -UseBasicParsing
    if ($r.StatusCode -eq 200) { Write-Ok "Readiness: $ApiBase/health/ready -> 200" }
    else { Write-Warn "Readiness: -> $($r.StatusCode)" }
} catch { Write-Warn "Readiness: -> unreachable (dependencies may be missing)" }

Write-Host ""

# --- Security Headers --------------------------------------------------------
Write-Host "  Security Headers" -ForegroundColor White
Write-Host ""

try {
    $r = Invoke-WebRequest -Uri "$ApiBase/health/live" -Method Head -TimeoutSec $Timeout -UseBasicParsing
    $hdrs = $r.Headers

    $headerChecks = @(
        @{ Name = "X-Content-Type-Options"; Critical = $true },
        @{ Name = "X-Frame-Options"; Critical = $true },
        @{ Name = "Referrer-Policy"; Critical = $false },
        @{ Name = "Strict-Transport-Security"; Critical = $false }
    )

    foreach ($h in $headerChecks) {
        if ($hdrs.ContainsKey($h.Name)) {
            Write-Ok "$($h.Name) present"
        } elseif ($h.Critical) {
            Write-Fail "$($h.Name) MISSING"
        } else {
            Write-Warn "$($h.Name) missing"
        }
    }
} catch {
    Write-Warn "Could not check security headers"
}

Write-Host ""

# --- SSL ---------------------------------------------------------------------
Write-Host "  SSL/TLS" -ForegroundColor White
Write-Host ""

try {
    $r = Invoke-WebRequest -Uri "$ApiBase/health/live" -TimeoutSec $Timeout -UseBasicParsing -ErrorAction Stop
    Write-Ok "HTTPS connection successful"
} catch {
    if ($_.Exception.Message -match "SSL|TLS|certificate") {
        Write-Fail "SSL/TLS error: $($_.Exception.Message)"
    } else {
        Write-Ok "HTTPS handshake OK (endpoint may have returned error)"
    }
}

Write-Host ""

# --- Azure Secrets -----------------------------------------------------------
Write-Host "  Azure Container App Secrets" -ForegroundColor White
Write-Host ""

$azLoggedIn = $false
try {
    $null = az account show --output none 2>$null
    $azLoggedIn = ($LASTEXITCODE -eq 0)
} catch {}

if ($azLoggedIn) {
    $secretsJson = az containerapp secret list `
        --name $AppName `
        --resource-group $ResourceGroup `
        --output json 2>$null

    if ($secretsJson) {
        $secretsList = $secretsJson | ConvertFrom-Json
        $secretNames = $secretsList | ForEach-Object { $_.name }

        $expected = @(
            "anthropic-api-key", "stripe-secret-key", "stripe-webhook-secret",
            "google-client-id", "google-client-secret", "cloudflare-api-token",
            "cloudflare-account-id", "scraper-api-key", "attom-api-key"
        )

        foreach ($s in $expected) {
            if ($secretNames -contains $s) {
                Write-Ok "Secret '$s' exists"
            } else {
                Write-Fail "Secret '$s' MISSING"
            }
        }
    } else {
        Write-Fail "Could not list secrets"
    }
} else {
    Write-Warn "Not logged into Azure CLI -- skipping secret verification"
}

Write-Host ""

# --- DNS (custom domain) ----------------------------------------------------
if ($Domain) {
    Write-Host "  DNS Resolution" -ForegroundColor White
    Write-Host ""

    foreach ($sub in @("api", "platform", "www")) {
        try {
            $dns = Resolve-DnsName "$sub.$Domain" -ErrorAction SilentlyContinue
            if ($dns) {
                $target = ($dns | Where-Object { $_.QueryType -eq "CNAME" } | Select-Object -First 1).NameHost
                if (-not $target) { $target = ($dns | Select-Object -First 1).IPAddress }
                Write-Ok "$sub.$Domain -> $target"
            } else {
                Write-Fail "$sub.$Domain does not resolve"
            }
        } catch {
            Write-Warn "$sub.$Domain -- DNS lookup failed"
        }
    }

    Write-Host ""
}

# --- API Functionality -------------------------------------------------------
Write-Host "  API Functionality" -ForegroundColor White
Write-Host ""

try {
    $body = '{"profileUrl": "https://www.zillow.com/profile/test-agent"}'
    $r = Invoke-WebRequest -Uri "$ApiBase/onboarding/sessions" `
        -Method Post -Body $body -ContentType "application/json" `
        -TimeoutSec $Timeout -UseBasicParsing -ErrorAction Stop

    $data = $r.Content | ConvertFrom-Json
    if ($data.sessionId) {
        Write-Ok "Onboarding session creation works"
    } else {
        Write-Warn "Onboarding response missing sessionId"
    }
} catch {
    Write-Warn "Onboarding session creation failed (may need secrets configured)"
}

Write-Host ""

# --- Platform ----------------------------------------------------------------
if ($PlatformBase) {
    Write-Host "  Platform" -ForegroundColor White
    Write-Host ""

    try {
        $r = Invoke-WebRequest -Uri $PlatformBase -TimeoutSec $Timeout -UseBasicParsing
        if ($r.StatusCode -eq 200) {
            Write-Ok "Platform loads: $PlatformBase -> 200"
        } else {
            Write-Warn "Platform: -> $($r.StatusCode)"
        }
    } catch {
        Write-Warn "Platform not reachable (may not be deployed yet)"
    }

    Write-Host ""
}

# --- Summary -----------------------------------------------------------------
Write-Host "  ----------------------------------------------------------------" -ForegroundColor Blue

if ($script:Fail -eq 0) {
    Write-Host "    Production looks good!" -ForegroundColor Green
} else {
    Write-Host "    $($script:Fail) critical issue(s) need attention." -ForegroundColor Red
}

Write-Host "    $($script:Pass) passed  |  $($script:Fail) failed  |  $($script:Warnings) warnings" -ForegroundColor DarkGray
Write-Host "  ----------------------------------------------------------------" -ForegroundColor Blue
Write-Host ""

exit $script:Fail

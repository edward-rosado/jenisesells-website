#Requires -Version 5.1
<#
.SYNOPSIS
    Add Cloudflare Workers custom domains for a Real Estate Star agent.

.DESCRIPTION
    Reads the agent config from config/accounts/{slug}/ or config/accounts/{slug}.json,
    then registers the following custom domains on the Workers service:

      - {slug}.real-estate-star.com  (always)
      - {identity.website}                  (if set in config)
      - www.{identity.website}              (if set in config)

    Domain status is checked once after registration and reported. No polling.

    Requires three environment variables:
      CLOUDFLARE_API_TOKEN    -- API token with Workers:Edit + Zone:Read permissions
      CLOUDFLARE_ACCOUNT_ID   -- Cloudflare account ID
      CLOUDFLARE_ZONE_ID      -- Zone ID for real-estate-star.com

.PARAMETER Slug
    The agent slug (e.g. jenise-buckalew). Must match a file or directory under
    config/accounts/.

.EXAMPLE
    .\add-agent-domain.ps1 -Slug jenise-buckalew

.EXAMPLE
    $env:CLOUDFLARE_API_TOKEN = "abc123"
    $env:CLOUDFLARE_ACCOUNT_ID = "7674efd9381763796f39ea67fe5e0505"
    $env:CLOUDFLARE_ZONE_ID = "your-zone-id"
    .\add-agent-domain.ps1 -Slug jenise-buckalew
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Slug
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# --- Constants ---
$WorkersService      = "real-estate-star-agents"
$BaseDomain          = "real-estate-star.com"
$AgentSubdomain      = "$BaseDomain"
$CfApiBase           = "https://api.cloudflare.com/client/v4"
$DefaultAccountId    = "7674efd9381763796f39ea67fe5e0505"
$ScriptDir           = $PSScriptRoot
$RepoRoot            = (Resolve-Path (Join-Path $ScriptDir "..\..")).Path
$AgentsDir           = Join-Path $RepoRoot "config\accounts"

# --- Helpers ---
function Write-Ok($Msg)   { Write-Host "    [OK]   $Msg" -ForegroundColor Green }
function Write-Fail($Msg) { Write-Host "    [FAIL] $Msg" -ForegroundColor Red }
function Write-Info($Msg) { Write-Host "    [-->]  $Msg" -ForegroundColor Cyan }
function Write-Warn($Msg) { Write-Host "    [WARN] $Msg" -ForegroundColor Yellow }

Write-Host ""
Write-Host "============================================" -ForegroundColor White
Write-Host " Real Estate Star -- Add Agent Domain" -ForegroundColor White
Write-Host "============================================" -ForegroundColor White
Write-Host ""

# --- Prerequisites: environment variables ---
Write-Host "=== Prerequisites ===" -ForegroundColor White

$prereqFailed = $false

if ($env:CLOUDFLARE_API_TOKEN) {
    Write-Ok "CLOUDFLARE_API_TOKEN set"
} else {
    Write-Fail "CLOUDFLARE_API_TOKEN is not set"
    Write-Host "    Create a token at: https://dash.cloudflare.com/profile/api-tokens" -ForegroundColor Yellow
    Write-Host "    Required permissions: Workers Scripts:Edit, Zone:Read" -ForegroundColor Yellow
    $prereqFailed = $true
}

if ($env:CLOUDFLARE_ACCOUNT_ID) {
    Write-Ok "CLOUDFLARE_ACCOUNT_ID set (from env)"
} elseif ($DefaultAccountId) {
    $env:CLOUDFLARE_ACCOUNT_ID = $DefaultAccountId
    Write-Ok "CLOUDFLARE_ACCOUNT_ID set (default)"
} else {
    Write-Fail "CLOUDFLARE_ACCOUNT_ID is not set"
    $prereqFailed = $true
}

if ($env:CLOUDFLARE_ZONE_ID) {
    Write-Ok "CLOUDFLARE_ZONE_ID set"
} else {
    Write-Fail "CLOUDFLARE_ZONE_ID is not set"
    Write-Host "    Find it in the Cloudflare dashboard under real-estate-star.com > Overview (right sidebar)" -ForegroundColor Yellow
    $prereqFailed = $true
}

if ($prereqFailed) {
    Write-Host ""
    Write-Fail "One or more required environment variables are missing. Aborting."
    exit 1
}

$ApiToken   = $env:CLOUDFLARE_API_TOKEN
$AccountId  = $env:CLOUDFLARE_ACCOUNT_ID
$ZoneId     = $env:CLOUDFLARE_ZONE_ID

# --- Validate slug and load config ---
Write-Host ""
Write-Host "=== Agent Config ===" -ForegroundColor White

# Support both flat layout (config/accounts/{slug}.json) and
# directory layout (config/accounts/{slug}/config.json)
$configPath = $null
$accountConfig = Join-Path $AgentsDir "$Slug\account.json"

if (Test-Path $accountConfig) {
    $configPath = $accountConfig
    Write-Ok "Found account config at config/accounts/$Slug/account.json"
} else {
    Write-Fail "No config found for slug '$Slug'"
    Write-Info "Expected: config/accounts/$Slug/account.json"
    exit 1
}

$agentConfig = Get-Content -Path $configPath -Raw | ConvertFrom-Json
Write-Ok "Agent config loaded (id: $($agentConfig.id))"

# Determine custom domain from identity.website (may be absent)
$customWebsite = $null
if ($agentConfig.PSObject.Properties["identity"] -and
    $agentConfig.identity.PSObject.Properties["website"] -and
    -not [string]::IsNullOrWhiteSpace($agentConfig.identity.website)) {
    $customWebsite = $agentConfig.identity.website.Trim().TrimStart("https://").TrimStart("http://").TrimEnd("/")
    Write-Ok "Custom website found: $customWebsite"
} else {
    Write-Info "No identity.website in config -- only subdomain will be registered"
}

# Build list of hostnames to register
$subdomain = "$Slug.$AgentSubdomain"
$hostnames = @($subdomain)
if ($customWebsite) {
    $apexDomain = $customWebsite -replace '^www\.', ''
    $hostnames += $apexDomain
    $hostnames += "www.$apexDomain"
}

Write-Host ""
Write-Info "Domains to register:"
foreach ($h in $hostnames) {
    Write-Info "  $h"
}

# --- Cloudflare API helpers ---
function New-CfHeaders {
    return @{
        "Authorization" = "Bearer $ApiToken"
        "Content-Type"  = "application/json"
    }
}

function Get-ExistingDomains {
    $url = "$CfApiBase/accounts/$AccountId/workers/domains"
    $savedEAP = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $resp = Invoke-RestMethod -Uri $url -Method GET -Headers (New-CfHeaders) -ErrorAction Stop
        $ErrorActionPreference = $savedEAP
        if ($resp.success) {
            return $resp.result
        }
        Write-Warn "Cloudflare API returned success=false when listing domains"
        return @()
    } catch {
        $ErrorActionPreference = $savedEAP
        Write-Warn "Failed to list existing domains: $_"
        return @()
    }
}

function Add-WorkersDomain($Hostname) {
    $url  = "$CfApiBase/accounts/$AccountId/workers/domains"
    $body = @{
        hostname    = $Hostname
        service     = $WorkersService
        zone_id     = $ZoneId
        environment = "production"
    } | ConvertTo-Json -Compress

    $savedEAP = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $resp = Invoke-RestMethod -Uri $url -Method PUT -Headers (New-CfHeaders) -Body $body -ErrorAction Stop
        $ErrorActionPreference = $savedEAP
        if ($resp.success) {
            return @{ ok = $true; result = $resp.result }
        }
        $errMsg = ($resp.errors | ForEach-Object { $_.message }) -join "; "
        return @{ ok = $false; error = $errMsg }
    } catch {
        $ErrorActionPreference = $savedEAP
        return @{ ok = $false; error = $_.ToString() }
    }
}

function Get-DomainStatus($Hostname, $AllDomains) {
    $match = $AllDomains | Where-Object { $_.hostname -eq $Hostname }
    if ($match) {
        return $match
    }
    return $null
}

# --- Register domains ---
Write-Host ""
Write-Host "=== Registering Domains ===" -ForegroundColor White

# Fetch current state once upfront to enable idempotency
$existingDomains = Get-ExistingDomains
$existingHostnames = $existingDomains | ForEach-Object { $_.hostname }

$results = @{}

foreach ($hostname in $hostnames) {
    if ($existingHostnames -contains $hostname) {
        Write-Warn "$hostname already registered -- skipping"
        $results[$hostname] = "already-exists"
    } else {
        Write-Info "Registering $hostname ..."
        $addResult = Add-WorkersDomain $hostname
        if ($addResult.ok) {
            Write-Ok "$hostname registered successfully"
            $results[$hostname] = "registered"
        } else {
            Write-Fail "Failed to register $hostname : $($addResult.error)"
            $results[$hostname] = "failed"
        }
    }
}

# --- Check domain status (once, no polling) ---
Write-Host ""
Write-Host "=== Domain Status Check ===" -ForegroundColor White
Write-Info "Fetching current domain list from Cloudflare (one-time check)..."

$currentDomains = Get-ExistingDomains

foreach ($hostname in $hostnames) {
    $domainObj = Get-DomainStatus $hostname $currentDomains
    if ($domainObj) {
        $svc = $domainObj.service
        $env = $domainObj.environment
        Write-Ok "$hostname -- service: $svc, environment: $env"
    } else {
        Write-Warn "$hostname -- not found in Cloudflare domain list"
        Write-Info "  It may take a moment to propagate. Re-run this script to recheck."
    }
}

# --- Summary ---
Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor White
Write-Info "Slug:    $Slug"
Write-Info "Service: $WorkersService"
Write-Host ""

$hasFailure = $false
foreach ($hostname in $hostnames) {
    $status = $results[$hostname]
    switch ($status) {
        "registered"    { Write-Ok   "$hostname -- registered" }
        "already-exists" { Write-Ok  "$hostname -- already registered (no change)" }
        "failed"        { Write-Fail "$hostname -- registration failed"; $hasFailure = $true }
        default         { Write-Warn "$hostname -- unknown status: $status" }
    }
}

Write-Host ""
if ($hasFailure) {
    Write-Fail "One or more domains failed to register. Check errors above."
    Write-Info "Common causes:"
    Write-Info "  - API token missing Workers:Edit permission"
    Write-Info "  - Zone ID does not match the domain's zone"
    Write-Info "  - Custom domain zone not added to this Cloudflare account"
    exit 1
} else {
    Write-Ok "All domains registered successfully."
    Write-Host ""
    Write-Info "Next steps:"
    Write-Info "  - DNS for $subdomain is auto-managed by Cloudflare (same account zone)"
    if ($customWebsite) {
        Write-Info "  - For ${customWebsite}: ensure the zone is added to this Cloudflare account"
        Write-Info "    and the nameservers at the registrar point to Cloudflare"
    }
    Write-Info "  - Verify live traffic at https://$subdomain"
}

Write-Host ""

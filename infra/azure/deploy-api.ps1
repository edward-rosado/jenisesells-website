#Requires -Version 5.1
<#
.SYNOPSIS
    Build, push, and deploy the Real Estate Star API to Azure Container Apps.

.DESCRIPTION
    Builds the Docker image, pushes to ACR, and updates the Container App with a
    unique version tag to ensure Azure picks up the new image.

.PARAMETER SkipBuild
    Skip the Docker build step (useful if image was already built locally).

.EXAMPLE
    .\deploy-api.ps1
    .\deploy-api.ps1 -SkipBuild
#>

param(
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# --- Configuration ---
$AppName       = "real-estate-star-api"
$ResourceGroup = "real-estate-star-rg"
$Registry      = "realestatestar"
$AcrHost       = "$Registry.azurecr.io"
$ImageName     = "$AcrHost/real-estate-star-api"
$DockerContext  = Join-Path $PSScriptRoot "..\..\apps\api"
$VersionTag    = "v" + (Get-Date -Format "yyyyMMdd-HHmmss")

# --- Helpers ---
function Write-Ok($Msg)   { Write-Host "    [OK]   $Msg" -ForegroundColor Green }
function Write-Fail($Msg) { Write-Host "    [FAIL] $Msg" -ForegroundColor Red }
function Write-Info($Msg)  { Write-Host "    [-->]  $Msg" -ForegroundColor Cyan }

function Test-CommandExists($Name) {
    $null = Get-Command $Name -ErrorAction SilentlyContinue
    return $?
}

# --- Branch Guard: only deploy from main ---
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$currentBranch = git -C $repoRoot rev-parse --abbrev-ref HEAD 2>$null
if ($currentBranch -ne "main") {
    Write-Host ""
    Write-Fail "You are on branch '$currentBranch' -- production deploys must come from 'main'."
    Write-Fail "Merge your changes to main first, then re-run."
    exit 1
}
Write-Ok "On branch 'main'"

# --- Prerequisites ---
Write-Host ""
Write-Host "=== Prerequisites ===" -ForegroundColor White
$prereqFailed = $false

if (Test-CommandExists "az") {
    Write-Ok "Azure CLI found"
} else {
    Write-Fail "Azure CLI not found. Install: https://aka.ms/installazurecliwindows"
    $prereqFailed = $true
}

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
    Write-Fail "Docker not found. Install Docker Desktop: https://www.docker.com/products/docker-desktop/"
    $prereqFailed = $true
}

if ($prereqFailed) { exit 1 }

# --- Verify Azure login ---
Write-Host ""
Write-Host "=== Azure Login ===" -ForegroundColor White
$savedEAP = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$account = az account show --output json 2>&1 | ConvertFrom-Json
$ErrorActionPreference = $savedEAP

if ($account.id) {
    Write-Ok "Logged in: $($account.name)"
} else {
    Write-Info "Not logged in. Running az login..."
    az login
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Azure login failed."
        exit 1
    }
}

# --- Build ---
if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "=== Docker Build ===" -ForegroundColor White
    Write-Info "Building from $DockerContext"
    Write-Info "Tag: ${ImageName}:${VersionTag}"

    docker build --no-cache -t "${ImageName}:${VersionTag}" -t "${ImageName}:latest" $DockerContext
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Docker build failed."
        exit 1
    }
    Write-Ok "Image built"
} else {
    Write-Host ""
    Write-Host "=== Docker Build ===" -ForegroundColor White
    Write-Info "Skipped (using existing image)"
    # Tag existing latest with version tag
    docker tag "${ImageName}:latest" "${ImageName}:${VersionTag}"
}

# --- Push ---
Write-Host ""
Write-Host "=== ACR Push ===" -ForegroundColor White
Write-Info "Logging into ACR..."

$savedEAP = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$null = az acr login --name $Registry 2>&1
$loginOk = ($LASTEXITCODE -eq 0)
$ErrorActionPreference = $savedEAP

if (-not $loginOk) {
    Write-Fail "ACR login failed."
    exit 1
}
Write-Ok "ACR login succeeded"

Write-Info "Pushing ${ImageName}:${VersionTag}..."
docker push "${ImageName}:${VersionTag}"
if ($LASTEXITCODE -ne 0) {
    Write-Fail "Docker push failed."
    exit 1
}
Write-Ok "Image pushed"

# --- Deploy ---
Write-Host ""
Write-Host "=== Container App Update ===" -ForegroundColor White
Write-Info "Deploying ${VersionTag} to $AppName..."

$savedEAP = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$null = az containerapp update `
    --name $AppName `
    --resource-group $ResourceGroup `
    --image "${ImageName}:${VersionTag}" `
    --output none 2>&1
$updateOk = ($LASTEXITCODE -eq 0)
$ErrorActionPreference = $savedEAP

if (-not $updateOk) {
    Write-Fail "Container App update failed."
    exit 1
}
Write-Ok "Container App updated with ${VersionTag}"

# --- Verify ---
Write-Host ""
Write-Host "=== Waiting for new revision ===" -ForegroundColor White
Write-Info "Waiting 15 seconds for revision to start..."
Start-Sleep -Seconds 15

$savedEAP = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$revisions = az containerapp revision list `
    --name $AppName `
    --resource-group $ResourceGroup `
    --output json 2>&1 | ConvertFrom-Json
$ErrorActionPreference = $savedEAP

if ($revisions) {
    $active = $revisions | Where-Object { $_.properties.active -eq $true } | Select-Object -First 1
    if ($active) {
        $revName = $active.name
        $health = $active.properties.healthState
        $replicas = $active.properties.replicas
        Write-Ok "Active revision: $revName (health: $health, replicas: $replicas)"
    }
}

# --- Health check ---
Write-Host ""
Write-Host "=== Health Check ===" -ForegroundColor White

try {
    $liveResp = Invoke-WebRequest -Uri "https://api.real-estate-star.com/health/live" -UseBasicParsing -TimeoutSec 10
    Write-Ok "/health/live -> $($liveResp.StatusCode)"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    if ($code) {
        Write-Fail "/health/live -> $code"
    } else {
        Write-Fail "/health/live -> no response"
    }
}

try {
    $readyResp = Invoke-WebRequest -Uri "https://api.real-estate-star.com/health/ready" -UseBasicParsing -TimeoutSec 10
    Write-Ok "/health/ready -> $($readyResp.StatusCode)"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    if ($code) {
        Write-Fail "/health/ready -> $code (may need a few more seconds)"
    } else {
        Write-Fail "/health/ready -> no response"
    }
}

Write-Host ""
Write-Host "=== Deploy Complete ===" -ForegroundColor White
Write-Info "Image: ${ImageName}:${VersionTag}"
Write-Info "Run .\diagnose-api.ps1 for full diagnostics"
Write-Host ""

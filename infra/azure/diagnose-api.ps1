#Requires -Version 5.1
<#
.SYNOPSIS
    Diagnoses Real Estate Star API deployment on Azure Container Apps.
.DESCRIPTION
    Checks Azure resources, secrets, ingress, DNS, container health, and logs
    to identify why the API is not responding. Safe to run multiple times.
.PARAMETER Fix
    Auto-fix missing env vars and scale settings.
.EXAMPLE
    .\diagnose-api.ps1
    .\diagnose-api.ps1 -Fix
    .\diagnose-api.ps1 -ShowLogs 50
#>
param(
    [int]$ShowLogs = 30,
    [switch]$Fix
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# --- Configuration ---
$AppName       = "real-estate-star-api"
$ResourceGroup = "real-estate-star-rg"
$Registry      = "realestatestar"
$Environment   = "real-estate-star-env"
$CustomDomain  = "api.real-estate-star.com"

# --- Helpers ---
function Test-CommandExists($Name) {
    $null = Get-Command $Name -ErrorAction SilentlyContinue
    return $?
}

function Write-Ok($Msg)   { Write-Host "  [OK]   $Msg" -ForegroundColor Green }
function Write-Warn($Msg) { Write-Host "  [WARN] $Msg" -ForegroundColor Yellow }
function Write-Fail($Msg) { Write-Host "  [FAIL] $Msg" -ForegroundColor Red }
function Write-Info($Msg)  { Write-Host "  [-->]  $Msg" -ForegroundColor Cyan }

function Write-Section($Title) {
    Write-Host ""
    Write-Host "=== $Title ===" -ForegroundColor White
}

function Invoke-Az {
    param([string[]]$Arguments)
    $savedEAP = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $output = & az @Arguments 2>&1
    $code = $LASTEXITCODE
    $ErrorActionPreference = $savedEAP

    if ($code -eq 0) {
        # Filter out WARNING lines from az CLI
        $clean = ($output | Where-Object { $_ -notmatch "^WARNING" }) -join "`n"
        return $clean.Trim()
    }
    return $null
}

# --- Prerequisites ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Real Estate Star API -- Diagnostics" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$prereqFailed = $false

Write-Section "0. Prerequisites"

if (Test-CommandExists "az") {
    Write-Ok "Azure CLI found"
} else {
    Write-Fail "Azure CLI not found. Install: https://aka.ms/installazurecliwindows"
    $prereqFailed = $true
}

if ($prereqFailed) {
    Write-Host ""
    Write-Fail "Fix prerequisites above and re-run."
    exit 1
}

# --- 1. Azure Login ---
Write-Section "1. Azure CLI Login"
$account = Invoke-Az @("account", "show", "--query", "{name: name, id: id}", "--output", "json")
if ($account) {
    $acct = $account | ConvertFrom-Json
    Write-Ok "Logged in: $($acct.name) ($($acct.id))"
} else {
    Write-Fail "Not logged in -- run: az login"
    exit 1
}

# --- 2. Resource Group ---
Write-Section "2. Resource Group"
$rgState = Invoke-Az @("group", "show", "--name", $ResourceGroup, "--query", "properties.provisioningState", "--output", "tsv")
if ($rgState -eq "Succeeded") {
    Write-Ok "Resource group '$ResourceGroup' exists"
} else {
    Write-Fail "Resource group '$ResourceGroup' not found"
    exit 1
}

# --- 3. Container Registry ---
Write-Section "3. Container Registry"
$acrLogin = Invoke-Az @("acr", "show", "--name", $Registry, "--query", "loginServer", "--output", "tsv")
if ($acrLogin) {
    Write-Ok "ACR '$Registry' exists at $acrLogin"

    $tags = Invoke-Az @("acr", "repository", "show-tags", "--name", $Registry, "--repository", $AppName, "--orderby", "time_desc", "--top", "5", "--output", "tsv")
    if ($tags) {
        Write-Ok "Images found:"
        $tags -split "`n" | ForEach-Object { Write-Info "${AppName}:$_" }
    } else {
        Write-Fail "No images found in ACR for '$AppName'"
        Write-Info "Fix: docker build + docker push to $acrLogin/$AppName"
    }
} else {
    Write-Fail "ACR '$Registry' not found"
}

# --- 4. Container App ---
Write-Section "4. Container App"
$appJson = Invoke-Az @("containerapp", "show", "--name", $AppName, "--resource-group", $ResourceGroup, "--output", "json")
if (-not $appJson) {
    Write-Fail "Container App '$AppName' not found"
    exit 1
}
$app = $appJson | ConvertFrom-Json

$provState = $app.properties.provisioningState
$runState  = if ($app.properties.PSObject.Properties["runningStatus"]) { $app.properties.runningStatus } else { "unknown" }
Write-Ok "Container App exists (provisioning: $provState, running: $runState)"

$minRep = $app.properties.template.scale.minReplicas
$maxRep = $app.properties.template.scale.maxReplicas
Write-Info "Scale: min=$minRep, max=$maxRep"
if ($minRep -eq 0) {
    Write-Warn "min-replicas is 0 -- container may scale to zero and be slow to wake"
    if ($Fix) {
        Write-Info "Setting min-replicas to 1..."
        $result = Invoke-Az @("containerapp", "update", "--name", $AppName, "--resource-group", $ResourceGroup, "--min-replicas", "1", "--output", "none")
        if ($null -ne $result -or $LASTEXITCODE -eq 0) {
            Write-Ok "min-replicas set to 1"
        } else {
            Write-Fail "Failed to set min-replicas"
        }
    } else {
        Write-Info "Fix: re-run with -Fix, or: az containerapp update --name $AppName --resource-group $ResourceGroup --min-replicas 1"
    }
}

# --- 5. Ingress ---
Write-Section "5. Ingress"
$ingress = $app.properties.configuration.ingress
if ($ingress) {
    $fqdn       = $ingress.fqdn
    $targetPort = $ingress.targetPort
    $external   = $ingress.external

    Write-Ok "FQDN: $fqdn"
    Write-Info "Target port: $targetPort"
    Write-Info "External: $external"

    if ($targetPort -ne 8080) {
        Write-Fail "Target port is $targetPort, expected 8080"
        Write-Info "Fix: az containerapp ingress update --name $AppName --resource-group $ResourceGroup --target-port 8080"
    }
    if (-not $external) {
        Write-Fail "Ingress is not external -- API unreachable from internet"
        Write-Info "Fix: az containerapp ingress update --name $AppName --resource-group $ResourceGroup --type external"
    }
} else {
    Write-Fail "No ingress configured"
    Write-Info "Fix: az containerapp ingress enable --name $AppName --resource-group $ResourceGroup --target-port 8080 --type external"
    $fqdn = $null
}

# --- 6. Custom Domains ---
Write-Section "6. Custom Domains"
# Check both the app config and the hostname list (Azure stores these in different places)
$hasDomain = $false

# Method 1: Check app config
$hasCustomDomains = $app.properties.configuration.PSObject.Properties["customDomains"]
$domains = if ($hasCustomDomains) { $app.properties.configuration.customDomains } else { $null }
if ($domains -and $domains.Count -gt 0) {
    foreach ($d in $domains) {
        $binding = if ($d.PSObject.Properties["bindingType"]) { $d.bindingType } else { "none" }
        Write-Ok "$($d.name) (binding: $binding)"
        if ($d.name -eq $CustomDomain) { $hasDomain = $true }
    }
}

# Method 2: Check ingress customDomains
if (-not $hasDomain) {
    $hasIngressDomains = $app.properties.configuration.PSObject.Properties["ingress"]
    if ($hasIngressDomains -and $app.properties.configuration.ingress.PSObject.Properties["customDomains"]) {
        $ingressDomains = $app.properties.configuration.ingress.customDomains
        if ($ingressDomains -and $ingressDomains.Count -gt 0) {
            foreach ($d in $ingressDomains) {
                $binding = if ($d.PSObject.Properties["bindingType"]) { $d.bindingType } else { "none" }
                Write-Ok "$($d.name) (binding: $binding)"
                if ($d.name -eq $CustomDomain) { $hasDomain = $true }
            }
        }
    }
}

# Method 3: Use hostname list command as fallback
if (-not $hasDomain) {
    $hostnames = Invoke-Az @("containerapp", "hostname", "list",
        "--name", $AppName,
        "--resource-group", $ResourceGroup,
        "--output", "json")
    if ($hostnames -and $hostnames.Count -gt 0) {
        foreach ($h in $hostnames) {
            $hname = $h.name
            $binding = if ($h.PSObject.Properties["bindingType"]) { $h.bindingType } else { "none" }
            Write-Ok "$hname (binding: $binding)"
            if ($hname -eq $CustomDomain) { $hasDomain = $true }
        }
    }
}

if (-not $hasDomain) {
    Write-Warn "Custom domain '$CustomDomain' not configured"
    if ($Fix) {
        # Check if Cloudflare proxy is hiding the CNAME (resolves to CF IP, not Azure FQDN)
        $cnameLookup = $null
        try {
            $cnameLookup = Resolve-DnsName -Name $CustomDomain -Type CNAME -ErrorAction SilentlyContinue |
                Where-Object { $_.QueryType -eq "CNAME" } |
                Select-Object -First 1
        } catch { }

        $cfProxied = $false
        if (-not $cnameLookup -and $fqdn) {
            # No raw CNAME visible -- Cloudflare proxy is likely on
            $cfProxied = $true
            Write-Warn "Cloudflare proxy (orange cloud) detected -- Azure cannot validate CNAME"
            Write-Info "Temporarily set the 'api' DNS record to DNS-only (grey cloud) in Cloudflare"
            Write-Info "Waiting 30 seconds for DNS propagation..."

            # Prompt user to toggle, then wait
            Write-Host ""
            Write-Host "  >>> Toggle the 'api' record to DNS-only in Cloudflare now, then press Enter <<<" -ForegroundColor Yellow
            $null = Read-Host "  Press Enter when done"

            # Re-check after user toggles
            Write-Info "Verifying DNS..."
            Start-Sleep -Seconds 5
            try {
                $cnameLookup = Resolve-DnsName -Name $CustomDomain -Type CNAME -ErrorAction SilentlyContinue |
                    Where-Object { $_.QueryType -eq "CNAME" } |
                    Select-Object -First 1
            } catch { }

            if ($cnameLookup) {
                Write-Ok "CNAME now visible: $($cnameLookup.NameHost)"
            } else {
                Write-Warn "CNAME still not visible -- trying anyway (may take a minute to propagate)"
            }
        }

        Write-Info "Adding custom domain '$CustomDomain'..."
        $addResult = Invoke-Az @("containerapp", "hostname", "add",
            "--name", $AppName,
            "--resource-group", $ResourceGroup,
            "--hostname", $CustomDomain,
            "--output", "none")
        if ($null -ne $addResult -or $LASTEXITCODE -eq 0) {
            Write-Ok "Hostname added"
            Write-Info "Binding managed certificate..."
            $bindResult = Invoke-Az @("containerapp", "hostname", "bind",
                "--name", $AppName,
                "--resource-group", $ResourceGroup,
                "--hostname", $CustomDomain,
                "--environment", $Environment,
                "--validation-method", "CNAME",
                "--output", "none")
            if ($null -ne $bindResult -or $LASTEXITCODE -eq 0) {
                Write-Ok "Managed certificate bound to $CustomDomain"
                if ($cfProxied) {
                    Write-Host ""
                    Write-Info "Certificate provisioned. You can now re-enable the Cloudflare proxy (orange cloud)"
                    Write-Info "on the 'api' DNS record in Cloudflare dashboard."
                }
            } else {
                Write-Warn "Certificate binding may take a few minutes -- re-run to check"
            }
        } else {
            Write-Fail "Failed to add hostname"
            Write-Info "Make sure the 'api' CNAME points to: $fqdn"
            Write-Info "And Cloudflare proxy is OFF (grey cloud / DNS-only) during setup"
        }
    } else {
        Write-Info "Fix: re-run with -Fix, or manually:"
        Write-Info "  1. Set Cloudflare 'api' record to DNS-only (grey cloud)"
        Write-Info "  2. az containerapp hostname add --name $AppName --resource-group $ResourceGroup --hostname $CustomDomain"
        Write-Info "  3. az containerapp hostname bind --name $AppName --resource-group $ResourceGroup --hostname $CustomDomain --environment $Environment --validation-method CNAME"
        Write-Info "  4. Re-enable Cloudflare proxy (orange cloud) after cert is provisioned"
    }
}

# --- 7. Secrets ---
Write-Section "7. Secrets"
$expectedSecrets = @(
    "anthropic-api-key",
    "stripe-secret-key",
    "stripe-webhook-secret",
    "google-client-id",
    "google-client-secret",
    "cloudflare-api-token",
    "cloudflare-account-id",
    "scraper-api-key",
    "attom-api-key"
)

$secretListJson = Invoke-Az @("containerapp", "secret", "list", "--name", $AppName, "--resource-group", $ResourceGroup, "--query", "[].name", "--output", "json")
$actualSecrets = @()
if ($secretListJson) {
    $actualSecrets = $secretListJson | ConvertFrom-Json
}

$missingSecrets = 0
foreach ($secret in $expectedSecrets) {
    if ($actualSecrets -contains $secret) {
        Write-Ok $secret
    } else {
        Write-Fail "$secret -- MISSING"
        $missingSecrets++
    }
}
if ($missingSecrets -gt 0) {
    Write-Info "Fix: az containerapp secret set --name $AppName --resource-group $ResourceGroup --secrets `"secret-name=value`""
}

# --- 8. Environment Variables ---
Write-Section "8. Environment Variables"
$containers = $app.properties.template.containers
$envVars = @()
$envEntries = @()
if ($containers -and $containers.Count -gt 0) {
    $envList = $containers[0].env
    if ($envList) {
        $envVars = $envList | ForEach-Object { $_.name }
        $envEntries = $envList
    }
}

# Required env vars: name -> expected value (or $null for secret refs that just need to exist)
$requiredEnvVars = [ordered]@{
    "ASPNETCORE_ENVIRONMENT"   = "Production"
    "Anthropic__ApiKey"        = $null  # secret ref
    "Stripe__SecretKey"        = $null  # secret ref
    "Stripe__WebhookSecret"    = $null  # secret ref
    "Stripe__PriceId"          = "prod_U7k7m92bbHfqHE"
    "Google__ClientId"         = $null  # secret ref
    "Google__ClientSecret"     = $null  # secret ref
    "Google__RedirectUri"      = "https://api.real-estate-star.com/oauth/google/callback"
    "Cloudflare__ApiToken"     = $null  # secret ref
    "Cloudflare__AccountId"    = $null  # secret ref
    "ScraperApi__ApiKey"       = $null  # secret ref
    "Platform__BaseUrl"        = "https://real-estate-star.com"
}

$missingEnvVars = @{}
foreach ($kv in $requiredEnvVars.GetEnumerator()) {
    $name = $kv.Key
    $expected = $kv.Value

    if ($envVars -contains $name) {
        $entry = $envEntries | Where-Object { $_.name -eq $name }
        $val = if ($entry.PSObject.Properties["value"]) { $entry.value } elseif ($entry.PSObject.Properties["secretRef"]) { "(secret: $($entry.secretRef))" } else { "?" }
        Write-Ok "$name = $val"

        # Check value mismatch for non-secret vars
        if ($expected -and $entry.PSObject.Properties["value"] -and $entry.value -ne $expected) {
            Write-Warn "  Expected: $expected"
            $missingEnvVars[$name] = $expected
        }
    } else {
        if ($expected) {
            Write-Fail "$name -- MISSING (should be: $expected)"
            $missingEnvVars[$name] = $expected
        } else {
            Write-Fail "$name -- MISSING (should be a secret ref)"
        }
    }
}

# Show all env vars for reference
Write-Host ""
Write-Info "All env vars configured:"
foreach ($e in $envEntries) {
    $val = if ($e.PSObject.Properties["value"]) { $e.value } elseif ($e.PSObject.Properties["secretRef"]) { "(secret: $($e.secretRef))" } else { "?" }
    Write-Host "       $($e.name) = $val"
}

# --- 8b. Auto-fix missing/wrong env vars ---
if ($missingEnvVars.Count -gt 0) {
    Write-Host ""
    if ($Fix) {
        Write-Section "8b. Fixing Environment Variables"
        $setArgs = @("containerapp", "update", "--name", $AppName, "--resource-group", $ResourceGroup, "--set-env-vars")
        foreach ($kv in $missingEnvVars.GetEnumerator()) {
            $setArgs += "$($kv.Key)=$($kv.Value)"
            Write-Info "Setting $($kv.Key) = $($kv.Value)"
        }
        $result = Invoke-Az $setArgs
        if ($result) {
            Write-Ok "Environment variables updated"
        } else {
            Write-Fail "Failed to update environment variables"
        }
    } else {
        Write-Warn "$($missingEnvVars.Count) env var(s) need fixing. Re-run with -Fix to auto-set them."
    }
}

# --- 9. Active Revisions ---
Write-Section "9. Active Revisions"
$revJson = Invoke-Az @("containerapp", "revision", "list", "--name", $AppName, "--resource-group", $ResourceGroup, "--output", "json")
if ($revJson) {
    $revisions = $revJson | ConvertFrom-Json
    foreach ($rev in $revisions) {
        $active  = $rev.properties.active
        $reps    = $rev.properties.replicas
        $health  = if ($rev.properties.PSObject.Properties["healthState"]) { $rev.properties.healthState } else { "?" }
        $created = if ($rev.properties.PSObject.Properties["createdTime"]) { $rev.properties.createdTime } else { "?" }
        $status  = if ($active) { "ACTIVE" } else { "inactive" }
        $color   = if ($active) { "Green" } else { "DarkGray" }
        Write-Host "  [$status] $($rev.name) -- replicas: $reps, health: $health, created: $created" -ForegroundColor $color
    }
} else {
    Write-Warn "Could not list revisions"
}

# --- 10. Recent Logs ---
Write-Section "10. Recent Logs (last $ShowLogs lines)"
Write-Host "  (Looking for startup errors, crashes, missing config...)" -ForegroundColor DarkGray
Write-Host ""

$savedEAP = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$logOutput = & az containerapp logs show `
    --name $AppName `
    --resource-group $ResourceGroup `
    --tail $ShowLogs `
    --follow false 2>&1
$logCode = $LASTEXITCODE
$ErrorActionPreference = $savedEAP

if ($logCode -eq 0 -and $logOutput) {
    $logOutput | ForEach-Object {
        $line = "$_"
        if ($line -match "error|exception|fail|crash|fatal" ) {
            Write-Host "  $line" -ForegroundColor Red
        } elseif ($line -match "warn") {
            Write-Host "  $line" -ForegroundColor Yellow
        } else {
            Write-Host "  $line"
        }
    }
} else {
    Write-Warn "Could not retrieve logs -- container may not have started yet"
    Write-Info "Try: az containerapp logs show --name $AppName --resource-group $ResourceGroup --follow"
}

# --- 11. DNS Resolution ---
Write-Section "11. DNS Resolution"
try {
    $dnsResult = Resolve-DnsName -Name "api.real-estate-star.com" -ErrorAction Stop
    $apiIp = ($dnsResult | Where-Object { $_.QueryType -eq "A" } | Select-Object -First 1).IPAddress
    if ($apiIp) {
        Write-Ok "api.real-estate-star.com resolves to $apiIp"
    } else {
        # Might be CNAME only
        $cname = ($dnsResult | Where-Object { $_.QueryType -eq "CNAME" } | Select-Object -First 1).NameHost
        if ($cname) {
            Write-Ok "api.real-estate-star.com -> CNAME $cname"
        } else {
            Write-Warn "api.real-estate-star.com resolved but no A or CNAME record found"
        }
    }
} catch {
    Write-Fail "api.real-estate-star.com does not resolve"
    if ($fqdn) {
        Write-Info "Fix: Add CNAME record in Cloudflare: api -> $fqdn"
    }
}

# --- 12. HTTP Health Checks ---
Write-Section "12. HTTP Health Checks"

# Try Azure FQDN directly (bypasses DNS/Cloudflare issues)
if ($fqdn) {
    Write-Host "  Testing Azure FQDN directly..." -ForegroundColor DarkGray
    try {
        $azureResp = Invoke-WebRequest -Uri "https://$fqdn/health/live" -UseBasicParsing -TimeoutSec 15 -ErrorAction Stop
        Write-Ok "Azure FQDN /health/live -> $($azureResp.StatusCode)"
    } catch {
        $statusCode = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { "no response" }
        Write-Fail "Azure FQDN /health/live -> $statusCode"
        if ($statusCode -eq "no response") {
            Write-Info "Container is not responding -- check logs above for startup crash"
        }
    }
}

# Try custom domain
Write-Host "  Testing custom domain..." -ForegroundColor DarkGray
try {
    $customResp = Invoke-WebRequest -Uri "https://api.real-estate-star.com/health/live" -UseBasicParsing -TimeoutSec 15 -ErrorAction Stop
    Write-Ok "api.real-estate-star.com /health/live -> $($customResp.StatusCode)"
} catch {
    $statusCode = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { "no response" }
    Write-Fail "api.real-estate-star.com /health/live -> $statusCode"
}

try {
    $readyResp = Invoke-WebRequest -Uri "https://api.real-estate-star.com/health/ready" -UseBasicParsing -TimeoutSec 15 -ErrorAction Stop
    Write-Ok "api.real-estate-star.com /health/ready -> $($readyResp.StatusCode)"
    $readyData = $readyResp.Content | ConvertFrom-Json
    Write-Info "Status: $($readyData.status)"
    if ($readyData.checks) {
        foreach ($check in $readyData.checks) {
            $checkColor = if ($check.status -eq "Healthy") { "Green" } else { "Red" }
            Write-Host "       $($check.name): $($check.status) ($($check.duration)ms)" -ForegroundColor $checkColor
        }
    }
} catch {
    $statusCode = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { "no response" }
    Write-Fail "api.real-estate-star.com /health/ready -> $statusCode"
}

# --- 13. Security Headers ---
Write-Section "13. Security Headers"
try {
    $headerResp = Invoke-WebRequest -Uri "https://api.real-estate-star.com/health/live" -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop
    $headers = $headerResp.Headers

    $expectedHeaders = @{
        "X-Content-Type-Options" = "nosniff"
        "X-Frame-Options"       = "DENY"
        "Referrer-Policy"       = "strict-origin-when-cross-origin"
    }

    foreach ($h in $expectedHeaders.GetEnumerator()) {
        $val = $headers[$h.Key]
        if ($val -eq $h.Value) {
            Write-Ok "$($h.Key): $val"
        } elseif ($val) {
            Write-Warn "$($h.Key): $val (expected: $($h.Value))"
        } else {
            Write-Fail "$($h.Key) -- MISSING"
        }
    }

    # HSTS
    $hsts = $headers["Strict-Transport-Security"]
    if ($hsts) {
        Write-Ok "Strict-Transport-Security: $hsts"
    } else {
        Write-Warn "Strict-Transport-Security -- missing (may be added by Cloudflare)"
    }
} catch {
    Write-Warn "Could not check security headers -- API not responding"
}

# --- Summary ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Diagnostics Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Common fixes:" -ForegroundColor White
Write-Host "  Scale to zero:   az containerapp update --name $AppName --resource-group $ResourceGroup --min-replicas 1"
Write-Host "  View live logs:  az containerapp logs show --name $AppName --resource-group $ResourceGroup --follow"
Write-Host "  Restart:         az containerapp revision restart --name $AppName --resource-group $ResourceGroup"
Write-Host "  Redeploy:        az containerapp update --name $AppName --resource-group $ResourceGroup --image ${Registry}.azurecr.io/${AppName}:latest"
Write-Host ""

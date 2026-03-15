#Requires -Version 5.1
<#
.SYNOPSIS
    Interactive Go-Live Runbook for Real Estate Star

.DESCRIPTION
    Walks you through every step to take the platform from zero to production.
    Validates each step before moving to the next.
    Safe to re-run -- checks what is already done and skips completed steps.

.PARAMETER Step
    Jump to a specific step (1-8)

.PARAMETER Reset
    Clear saved progress and start fresh

.PARAMETER Nuke
    Delete ALL Azure resources (resource group, ACR, Container App) and start over

.EXAMPLE
    .\infra\go-live.ps1              # Start from the beginning
    .\infra\go-live.ps1 -Step 3      # Jump to step 3
    .\infra\go-live.ps1 -Reset       # Clear progress
    .\infra\go-live.ps1 -Nuke        # Delete everything and start over
#>

param(
    [int]$Step = 0,
    [switch]$Reset,
    [switch]$Nuke
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# --- Configuration -----------------------------------------------------------
$ResourceGroup = "real-estate-star-rg"
$AcrName       = "realestatestaracr"
$AppName       = "real-estate-star-api"
$Environment   = "real-estate-star-env"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = Split-Path -Parent $ScriptDir
$StateFile = Join-Path $RepoRoot ".go-live-state"

# --- Helpers -----------------------------------------------------------------
function Write-Banner($Text) {
    Write-Host ""
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host "    $Text" -ForegroundColor Cyan
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Write-StepBanner($StepNum, $Title) {
    $Total = 8
    Write-Host ""
    Write-Host "  ----------------------------------------------------------------" -ForegroundColor Blue
    Write-Host "    Step $StepNum of $Total`: $Title" -ForegroundColor White
    Write-Host "  ----------------------------------------------------------------" -ForegroundColor Blue
    Write-Host ""
}

function Write-Ok($Msg)   { Write-Host "    [OK]   $Msg" -ForegroundColor Green }
function Write-Warn($Msg) { Write-Host "    [WARN] $Msg" -ForegroundColor Yellow }
function Write-Fail($Msg) { Write-Host "    [FAIL] $Msg" -ForegroundColor Red }
function Write-Info($Msg)  { Write-Host "    [-->]  $Msg" -ForegroundColor Cyan }

function Write-Cmd($Msg) {
    Write-Host ""
    Write-Host "      $Msg" -ForegroundColor DarkCyan
    Write-Host ""
}

function Save-State($StepNum) {
    Set-Content -Path $StateFile -Value $StepNum -NoNewline
}

function Get-SavedState {
    if (Test-Path $StateFile) {
        return [int](Get-Content $StateFile -Raw).Trim()
    }
    return 0
}

function Wait-ForUser($CurrentStep) {
    Write-Host ""
    Write-Host "    Press Enter when ready to continue (or 'q' to quit and save progress)..." -ForegroundColor Yellow
    $response = Read-Host
    if ($response -eq 'q' -or $response -eq 'Q') {
        Save-State $CurrentStep
        Write-Host ""
        Write-Host "  Progress saved at step $CurrentStep. Re-run this script to resume." -ForegroundColor Green
        Write-Host ""
        exit 0
    }
}

function Confirm-Action($Prompt) {
    Write-Host ""
    Write-Host "    $Prompt (y/n)" -ForegroundColor Yellow
    $response = Read-Host
    return ($response -eq 'y' -or $response -eq 'Y')
}

function Test-AzLoggedIn {
    try {
        $null = az account show --output none 2>$null
        return $LASTEXITCODE -eq 0
    } catch {
        return $false
    }
}

function Invoke-SetupSh {
    # Find bash: prefer Git Bash, fall back to PATH, then WSL
    $bashCmd = $null
    if (Test-CommandExists "git") {
        $gitDir = Split-Path (Get-Command git).Source
        $gitBash = Join-Path (Split-Path $gitDir) "bin\bash.exe"
        if (Test-Path $gitBash) { $bashCmd = $gitBash }
    }
    if (-not $bashCmd -and (Test-CommandExists "bash")) { $bashCmd = "bash" }
    if (-not $bashCmd -and (Test-CommandExists "wsl")) { $bashCmd = "wsl" }

    if (-not $bashCmd) {
        Write-Fail "No bash found (Git Bash, WSL, or bash on PATH)."
        Write-Info "Install Git (includes Git Bash): winget install Git.Git"
        Write-Info "Then close and reopen PowerShell, and re-run this script."
        return $false
    }

    # Temporarily relax error handling -- bash/az send warnings to stderr
    # which PowerShell treats as terminating errors under $ErrorActionPreference = "Stop"
    $savedEAP = $ErrorActionPreference
    $ErrorActionPreference = "Continue"

    $output = & $bashCmd "$ScriptDir/azure/setup.sh" 2>&1
    $exitCode = $LASTEXITCODE

    $ErrorActionPreference = $savedEAP

    # Print the output (it was captured to prevent stderr from crashing PowerShell)
    foreach ($line in $output) {
        if ($line -is [System.Management.Automation.ErrorRecord]) {
            # stderr line -- print as warning color but don't crash
            Write-Host "    $($line.ToString())" -ForegroundColor DarkYellow
        } else {
            Write-Host "    $line"
        }
    }

    if ($exitCode -ne 0) {
        Write-Fail "setup.sh failed (exit code $exitCode). Check the output above."
        return $false
    }
    return $true
}

function Get-AppUrl {
    try {
        $fqdn = az containerapp show `
            --name $AppName `
            --resource-group $ResourceGroup `
            --query "properties.configuration.ingress.fqdn" `
            --output tsv 2>$null
        if ($LASTEXITCODE -eq 0 -and $fqdn) { return $fqdn }
    } catch {}
    return $null
}

# --- Handle Reset ------------------------------------------------------------
if ($Reset) {
    if (Test-Path $StateFile) { Remove-Item $StateFile }
    Write-Host "  Progress reset." -ForegroundColor Green
    exit 0
}

# --- Handle Nuke (delete all Azure resources and start fresh) ----------------
if ($Nuke) {
    Write-Host ""
    Write-Host "  ================================================================" -ForegroundColor Red
    Write-Host "    NUKE MODE -- Delete all Azure resources and start fresh" -ForegroundColor Red
    Write-Host "  ================================================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "    This will DELETE the entire resource group '$ResourceGroup'" -ForegroundColor Yellow
    Write-Host "    including the Container Registry, Container App, and all secrets." -ForegroundColor Yellow
    Write-Host ""
    if (Confirm-Action "Are you SURE you want to delete everything?") {
        if (Test-Path $StateFile) { Remove-Item $StateFile }

        $oldEAP = $ErrorActionPreference
        $ErrorActionPreference = "Continue"

        $null = az group show --name $ResourceGroup --output none 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Info "Deleting resource group '$ResourceGroup' (this takes 1-2 minutes)..."
            $null = az group delete --name $ResourceGroup --yes --no-wait 2>&1
            Write-Ok "Resource group deletion started (runs in background)."
            Write-Info "Wait 2-3 minutes, then re-run: .\infra\go-live.ps1"
        } else {
            Write-Ok "Resource group '$ResourceGroup' does not exist. Nothing to delete."
        }

        $ErrorActionPreference = $oldEAP
    } else {
        Write-Host "  Cancelled." -ForegroundColor DarkGray
    }
    exit 0
}

# --- Determine start step ----------------------------------------------------
$StartStep = $Step
if ($StartStep -eq 0) {
    $saved = Get-SavedState
    if ($saved -gt 0) {
        Write-Host "  Found saved progress at step $saved." -ForegroundColor Cyan
        if (Confirm-Action "Resume from step $saved`?") {
            $StartStep = $saved
        } else {
            $StartStep = 1
        }
    } else {
        $StartStep = 1
    }
}

# --- Prerequisites -----------------------------------------------------------
# Check for required tools and auto-install if possible.
# Each check: detect tool -> if missing, try winget -> if no winget, show manual URL.

function Test-CommandExists($Name) {
    $null = Get-Command $Name -ErrorAction SilentlyContinue
    return $?
}

function Refresh-PathEnv {
    # Reload PATH from registry so newly installed tools are found without restarting
    $machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
    $userPath    = [Environment]::GetEnvironmentVariable("Path", "User")
    $env:Path = "$machinePath;$userPath"
}

function Install-WithWinget($PackageId, $ToolName, $ManualUrl) {
    $hasWinget = Test-CommandExists "winget"
    if ($hasWinget) {
        Write-Info "Installing $ToolName via winget..."
        Write-Host ""
        winget install --id $PackageId --accept-package-agreements --accept-source-agreements
        if ($LASTEXITCODE -eq 0) {
            Refresh-PathEnv
            Write-Ok "$ToolName installed successfully!"
            return $true
        } else {
            Write-Warn "winget install returned exit code $LASTEXITCODE"
        }
    } else {
        Write-Warn "winget not available for auto-install."
    }

    Write-Fail "Could not auto-install $ToolName."
    Write-Host "    Install manually: $ManualUrl" -ForegroundColor Cyan
    Write-Host "    Then re-run this script." -ForegroundColor Cyan
    return $false
}

Write-Host ""
Write-Host "  Checking prerequisites..." -ForegroundColor White
Write-Host ""

$prereqFailed = $false

# 1. Azure CLI
if (Test-CommandExists "az") {
    Write-Ok "Azure CLI (az) found"
} else {
    Write-Warn "Azure CLI (az) not found -- attempting install..."
    $installed = Install-WithWinget "Microsoft.AzureCLI" "Azure CLI" "https://aka.ms/installazurecliwindows"
    if ($installed) {
        if (Test-CommandExists "az") {
            Write-Ok "Azure CLI (az) now available"
        } else {
            Write-Fail "Azure CLI installed but not on PATH. Close and reopen PowerShell, then re-run."
            $prereqFailed = $true
        }
    } else {
        $prereqFailed = $true
    }
}

# 2. Git / Git Bash (needed for setup.sh in step 1)
if (Test-CommandExists "git") {
    Write-Ok "Git found"
    # Check for bash via Git
    $gitDir = Split-Path (Get-Command git).Source
    $bashPath = Join-Path (Split-Path $gitDir) "bin\bash.exe"
    if (Test-Path $bashPath) {
        Write-Ok "Git Bash found at $bashPath"
    } elseif (Test-CommandExists "bash") {
        Write-Ok "bash found on PATH"
    } else {
        Write-Warn "Git found but bash.exe not detected. Step 1 may need Git Bash."
    }
} else {
    Write-Warn "Git not found -- attempting install..."
    $installed = Install-WithWinget "Git.Git" "Git" "https://git-scm.com/download/win"
    if ($installed) {
        Refresh-PathEnv
        if (Test-CommandExists "git") {
            Write-Ok "Git now available"
        } else {
            Write-Fail "Git installed but not on PATH. Close and reopen PowerShell, then re-run."
            $prereqFailed = $true
        }
    } else {
        $prereqFailed = $true
    }
}

# 3. Docker (needed for building and pushing container images)
if (Test-CommandExists "docker") {
    Write-Ok "Docker CLI found"
    # Also check if the Docker daemon is actually running
    $savedEAPDocker = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $null = docker info 2>&1
    $dockerRunning = ($LASTEXITCODE -eq 0)
    $ErrorActionPreference = $savedEAPDocker

    if ($dockerRunning) {
        Write-Ok "Docker daemon is running"
    } else {
        Write-Fail "Docker is installed but the daemon is not running."
        Write-Host "    Start Docker Desktop and wait for it to finish loading," -ForegroundColor Cyan
        Write-Host "    then re-run this script." -ForegroundColor Cyan
        $prereqFailed = $true
    }
} else {
    Write-Fail "Docker not found. Docker Desktop is required to build container images."
    Write-Host "    Install Docker Desktop: https://www.docker.com/products/docker-desktop/" -ForegroundColor Cyan
    Write-Host "    (Docker Desktop requires manual install -- winget cannot install it)" -ForegroundColor DarkGray
    Write-Host "    After installing, start Docker Desktop and re-run this script." -ForegroundColor Cyan
    $prereqFailed = $true
}

Write-Host ""

if ($prereqFailed) {
    Write-Host "  Some prerequisites could not be installed." -ForegroundColor Red
    Write-Host "  Install the missing tools above, then re-run this script." -ForegroundColor Red
    Write-Host ""
    exit 1
}

Write-Host "  Prerequisites look good. Continuing..." -ForegroundColor Green
Write-Host ""

# --- Intro -------------------------------------------------------------------
Write-Banner "Real Estate Star -- Go Live Runbook"
Write-Host "    This script walks you through every step to deploy Real Estate Star"
Write-Host "    to production. It validates each step and saves progress so you can"
Write-Host "    quit and resume at any time."
Write-Host ""
Write-Host "    Steps:" -ForegroundColor White
Write-Host "      1. Azure login & infrastructure setup"
Write-Host "      2. Set Azure Container App secrets"
Write-Host "      3. GitHub Actions credentials & secrets"
Write-Host "      4. Cloudflare DNS configuration"
Write-Host "      5. Grafana Cloud monitoring"
Write-Host "      6. Google OAuth production config"
Write-Host "      7. Stripe production webhook"
Write-Host "      8. Smoke tests & verification"
Write-Host ""

###############################################################################
# STEP 1: Azure Infrastructure
###############################################################################
if ($StartStep -le 1) {
    Write-StepBanner 1 "Azure Infrastructure Setup"

    if (Test-AzLoggedIn) {
        $sub = az account show --query name --output tsv
        Write-Ok "Already logged into Azure (subscription: $sub)"
    } else {
        Write-Warn "Not logged into Azure CLI."
        Write-Info "Running 'az login' -- a browser window will open for you to sign in."
        Write-Host ""
        az login
    }

    # Check that we actually have a subscription (login can succeed with zero subscriptions)
    $subs = az account list --output json 2>$null | ConvertFrom-Json
    if (-not $subs -or $subs.Count -eq 0) {
        Write-Host ""
        Write-Fail "No Azure subscriptions found for your account."
        Write-Host ""
        Write-Host "    You need an Azure subscription before we can create infrastructure." -ForegroundColor White
        Write-Host "    Azure Free gives you `$200 credit for 30 days + always-free services." -ForegroundColor White
        Write-Host ""
        Write-Info "Opening Azure free signup page..."
        Start-Process "https://azure.microsoft.com/free"
        Write-Host ""
        Write-Host "    1. Sign up with your Microsoft/GitHub/Google account" -ForegroundColor Cyan
        Write-Host "    2. Verify your phone number" -ForegroundColor Cyan
        Write-Host "    3. Add a credit card (for verification only -- won't charge you)" -ForegroundColor Cyan
        Write-Host "    4. Once your subscription is active, re-run this script" -ForegroundColor Cyan
        Write-Host ""
        Save-State 1
        exit 0
    }

    $sub = az account show --query name --output tsv
    Write-Ok "Using subscription: $sub"
    Write-Host ""

    # Register required Azure resource providers (new subscriptions need this)
    $requiredProviders = @(
        "Microsoft.ContainerRegistry",
        "Microsoft.App",
        "Microsoft.OperationalInsights"
    )

    Write-Info "Registering Azure resource providers (this can take a few minutes)..."
    $oldEAP = $ErrorActionPreference
    $ErrorActionPreference = "Continue"

    $providerFailed = $false
    foreach ($provider in $requiredProviders) {
        $state = az provider show --namespace $provider --query "registrationState" --output tsv 2>$null
        if ($state -eq "Registered") {
            Write-Ok "$provider already registered"
        } else {
            Write-Warn "$provider not registered -- registering (blocks until done)..."
            # --wait makes az block until registration completes instead of returning immediately
            $null = az provider register --namespace $provider --wait 2>&1
            if ($LASTEXITCODE -eq 0) {
                # Double-check it actually registered
                $state = az provider show --namespace $provider --query "registrationState" --output tsv 2>$null
                if ($state -eq "Registered") {
                    Write-Ok "$provider registered successfully"
                } else {
                    Write-Fail "$provider registration returned OK but state is: $state"
                    $providerFailed = $true
                }
            } else {
                Write-Fail "Failed to register $provider (exit code $LASTEXITCODE)"
                $providerFailed = $true
            }
        }
    }

    $ErrorActionPreference = $oldEAP

    if ($providerFailed) {
        Write-Host ""
        Write-Fail "Some resource providers failed to register."
        Write-Info "Try running manually: az provider register -n Microsoft.OperationalInsights --wait"
        Write-Info "Then re-run this script."
        Save-State 1
        exit 1
    }

    Write-Ok "All resource providers registered and verified"
    Write-Host ""

    # Check if infra already exists
    $rgExists = $false
    try {
        $null = az group show --name $ResourceGroup --output none 2>$null
        $rgExists = ($LASTEXITCODE -eq 0)
    } catch {}

    $appUrl = $null

    if ($rgExists) {
        Write-Ok "Resource group '$ResourceGroup' already exists"

        $appExists = $false
        try {
            $null = az containerapp show --name $AppName --resource-group $ResourceGroup --output none 2>$null
            $appExists = ($LASTEXITCODE -eq 0)
        } catch {}

        if ($appExists) {
            $appUrl = Get-AppUrl
            Write-Ok "Container App '$AppName' already exists"
            Write-Ok "App URL: https://$appUrl"
            Write-Host ""
            Write-Info "Skipping infrastructure creation (already done)."
        } else {
            Write-Warn "Resource group exists but Container App does not."
            Write-Info "Running setup.sh to create remaining infrastructure..."
            Write-Host ""
            $ran = Invoke-SetupSh
            if ($ran) {
                $appUrl = Get-AppUrl
                Write-Ok "Infrastructure created! App URL: https://$appUrl"
            } else {
                Write-Host ""
                Write-Fail "Infrastructure setup failed. Fix the error above and re-run."
                Save-State 1
                exit 1
            }
        }
    } else {
        Write-Info "Creating Azure infrastructure from scratch..."
        Write-Info "This takes 3-5 minutes. Grab a coffee."
        Write-Host ""
        $ran = Invoke-SetupSh
        if ($ran) {
            $appUrl = Get-AppUrl
            Write-Ok "Infrastructure created! App URL: https://$appUrl"
        } else {
            Write-Host ""
            Write-Fail "Infrastructure setup failed. Fix the error above and re-run."
            Save-State 1
            exit 1
        }
    }

    if (-not $appUrl) {
        Write-Warn "Could not determine App URL. It may appear after the app finishes deploying."
    } else {
        Write-Host ""
        Write-Host "    Save this URL -- you need it for DNS and GitHub secrets:" -ForegroundColor White
        Write-Host "    https://$appUrl" -ForegroundColor Green
    }

    Save-State 2
    Wait-ForUser 1
}

###############################################################################
# STEP 2: Azure Container App Secrets
###############################################################################
if ($StartStep -le 2) {
    Write-StepBanner 2 "Set Azure Container App Secrets"

    Write-Host "    This step replaces placeholder secrets with your real API keys."
    Write-Host "    You will need these values ready to paste:"
    Write-Host ""
    Write-Host "      - Anthropic API Key"
    Write-Host "      - Stripe Secret Key (live mode: sk_live_...)"
    Write-Host "      - Stripe Webhook Secret (you will get this in step 7, skip for now)"
    Write-Host "      - Google Client ID"
    Write-Host "      - Google Client Secret"
    Write-Host "      - Cloudflare API Token"
    Write-Host "      - Cloudflare Account ID"
    Write-Host "      - ScraperAPI Key"
    Write-Host "      - ATTOM API Key"
    Write-Host ""
    Write-Warn "Tip: Press Enter to skip any secret you do not have yet."
    Write-Host ""

    if (Confirm-Action "Ready to set secrets now?") {
        $secrets = @(
            @{ Name = "anthropic-api-key";    Display = "Anthropic API Key" },
            @{ Name = "stripe-secret-key";    Display = "Stripe Secret Key" },
            @{ Name = "stripe-webhook-secret"; Display = "Stripe Webhook Secret" },
            @{ Name = "google-client-id";     Display = "Google Client ID" },
            @{ Name = "google-client-secret";  Display = "Google Client Secret" },
            @{ Name = "cloudflare-api-token";  Display = "Cloudflare API Token" },
            @{ Name = "cloudflare-account-id"; Display = "Cloudflare Account ID" },
            @{ Name = "scraper-api-key";       Display = "ScraperAPI Key" },
            @{ Name = "attom-api-key";         Display = "ATTOM API Key" }
        )

        $secretArgs = @()
        $skipped = 0

        Write-Host ""
        Write-Host "    ============================================================" -ForegroundColor DarkGray
        Write-Host "      Enter secret values (input is hidden -- paste carefully)" -ForegroundColor White
        Write-Host "    ============================================================" -ForegroundColor DarkGray
        Write-Host ""

        foreach ($s in $secrets) {
            $secureVal = Read-Host -Prompt "      $($s.Display) ($($s.Name))" -AsSecureString
            $plainVal = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
                [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureVal)
            )

            if ([string]::IsNullOrWhiteSpace($plainVal)) {
                Write-Host "        -> Skipped (empty). Will keep current value." -ForegroundColor DarkGray
                $skipped++
                continue
            }

            $secretArgs += "$($s.Name)=$plainVal"
        }

        if ($secretArgs.Count -gt 0) {
            Write-Host ""
            Write-Info "Updating $($secretArgs.Count) secret(s) on '$AppName'..."

            az containerapp secret set `
                --name $AppName `
                --resource-group $ResourceGroup `
                --secrets @secretArgs `
                --output none

            if ($LASTEXITCODE -eq 0) {
                Write-Ok "Secrets updated! ($($secretArgs.Count) set, $skipped skipped)"
            } else {
                Write-Fail "Secret update failed. Check Azure CLI output above."
            }
        } else {
            Write-Warn "No secrets entered. Run this step again when you have the values."
        }
    } else {
        Write-Warn "Skipped. Re-run with -Step 2 when ready."
    }

    Save-State 3
    Wait-ForUser 2
}

###############################################################################
# STEP 3: GitHub Actions Credentials & Secrets
###############################################################################
if ($StartStep -le 3) {
    Write-StepBanner 3 "GitHub Actions Credentials & Secrets"

    Write-Host "    This step creates an Azure service principal so GitHub Actions can"
    Write-Host "    deploy to your Container App automatically."
    Write-Host ""

    if (Confirm-Action "Create the GitHub Actions service principal now?") {
        $subId = az account show --query id --output tsv

        $null = az group show --name $ResourceGroup --output none 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Fail "Resource group '$ResourceGroup' not found. Run step 1 first."
            exit 1
        }

        $rgScope = "/subscriptions/$subId/resourceGroups/$ResourceGroup"

        Write-Info "Creating service principal 'github-actions-real-estate-star'..."
        $prevPref = $ErrorActionPreference
        $ErrorActionPreference = 'SilentlyContinue'
        $spOutput = az ad sp create-for-rbac `
            --name "github-actions-real-estate-star" `
            --role Contributor `
            --scopes $rgScope 2>&1 | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] }
        $spExitCode = $LASTEXITCODE
        $ErrorActionPreference = $prevPref

        if ($spExitCode -ne 0 -or -not $spOutput) {
            Write-Fail "Service principal creation failed."
            Write-Info "You may need Owner or User Access Administrator role."
            Save-State 3
            Wait-ForUser 3
        } else {
            $sp = $spOutput | ConvertFrom-Json
            $spAppId = $sp.appId

            # Build the AZURE_CREDENTIALS JSON that GitHub Actions expects
            $azureCreds = @{
                clientId       = $sp.appId
                clientSecret   = $sp.password
                subscriptionId = $subId
                tenantId       = $sp.tenant
            } | ConvertTo-Json -Compress
            $azureCredsPretty = @{
                clientId       = $sp.appId
                clientSecret   = $sp.password
                subscriptionId = $subId
                tenantId       = $sp.tenant
            } | ConvertTo-Json

            # ACR may not exist yet if step 1 hasn't run -- assign role only if it does
            $savedEAP2 = $ErrorActionPreference
            $ErrorActionPreference = 'SilentlyContinue'
            $acrId = az acr show --name $AcrName --query id --output tsv 2>&1 | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] }
            $ErrorActionPreference = $savedEAP2

            if ($acrId) {
                Write-Info "Assigning AcrPush role on '$AcrName'..."
                $savedEAP3 = $ErrorActionPreference
                $ErrorActionPreference = 'SilentlyContinue'
                az role assignment create `
                    --assignee $spAppId `
                    --role AcrPush `
                    --scope $acrId `
                    --output none 2>&1 | Out-Null
                $ErrorActionPreference = $savedEAP3
                Write-Ok "AcrPush role assigned."
            } else {
                Write-Warn "ACR '$AcrName' not found yet. Run step 1 first to create it."
                Write-Warn "You can assign AcrPush later by re-running -Step 3."
            }

            Write-Ok "Service principal created!"
            Write-Host ""
            Write-Host "    --- BEGIN AZURE_CREDENTIALS ---" -ForegroundColor DarkYellow
            Write-Host $azureCredsPretty -ForegroundColor Gray
            Write-Host "    --- END AZURE_CREDENTIALS -----" -ForegroundColor DarkYellow
            Write-Host ""
            Write-Host "    Copy that JSON above. You need it for GitHub." -ForegroundColor White
        }
    } else {
        Write-Warn "Skipped. Re-run with -Step 3 when ready."
    }

    $appUrl = Get-AppUrl

    Write-Host ""
    Write-Host "    Now add these secrets to your GitHub repo:" -ForegroundColor White
    Write-Host ""
    Write-Host "    Go to: https://github.com/<owner>/Real-Estate-Star/settings/secrets/actions" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "    Secret Name              Value" -ForegroundColor White
    Write-Host "    ------------------------ ---------------------------------" -ForegroundColor DarkGray
    Write-Host "    AZURE_CREDENTIALS         (the JSON blob above)"
    Write-Host "    AZURE_RG_NAME             $ResourceGroup"
    Write-Host "    AZURE_ACR_NAME            $AcrName"
    Write-Host "    AZURE_APP_NAME            $AppName"
    Write-Host "    API_URL                   https://$appUrl"
    Write-Host "    STRIPE_PUBLISHABLE_KEY    pk_live_... (from Stripe dashboard)"
    Write-Host "    STRIPE_WEBHOOK_SECRET     whsec_... (from step 7)"
    Write-Host "    ATTOM_API_KEY             (from ATTOM Data dashboard)"
    Write-Host ""
    Write-Warn "You can add STRIPE_WEBHOOK_SECRET after step 7."

    Save-State 4
    Wait-ForUser 3
}

###############################################################################
# STEP 4: Cloudflare DNS
###############################################################################
if ($StartStep -le 4) {
    Write-StepBanner 4 "Cloudflare DNS Configuration"

    $appUrl = Get-AppUrl
    if (-not $appUrl) { $appUrl = "<your-app>.azurecontainerapps.io" }

    Write-Host "    Set up DNS records to point your domain to Azure and Cloudflare Pages."
    Write-Host ""
    Write-Host "    Required DNS Records (in Cloudflare dashboard):" -ForegroundColor White
    Write-Host ""
    Write-Host "    Type     | Name           | Target                                      | Proxy" -ForegroundColor DarkGray
    Write-Host "    ---------|----------------|---------------------------------------------|--------" -ForegroundColor DarkGray
    Write-Host "    CNAME    | platform       | real-estate-star-platform.pages.dev           | Proxied"
    Write-Host "    CNAME    | <handle>       | real-estate-star-agent-site.pages.dev         | Proxied"
    Write-Host "               (add one per agent via infra/cloudflare/add-agent-domain.ps1)" -ForegroundColor DarkGray
    Write-Host "    CNAME    | api            | $appUrl | Proxied"
    Write-Host "    CNAME    | www            | platform.real-estate-star.com                 | Proxied"
    Write-Host ""
    Write-Host "    SSL/TLS Settings:" -ForegroundColor White
    Write-Host "      - Encryption mode: Full (strict)"
    Write-Host "      - Always Use HTTPS: On"
    Write-Host "      - Minimum TLS: 1.2"
    Write-Host "      - HSTS: On (includeSubDomains, max-age 6 months)"
    Write-Host ""
    Write-Host "    Page Rules:" -ForegroundColor White
    Write-Host "      - api.real-estate-star.com/* -> Cache Level: Bypass"
    Write-Host "      - platform.real-estate-star.com/api/* -> Cache Level: Bypass"
    Write-Host "      - <handle>.real-estate-star.com/_next/static/* -> Cache Everything (per agent)"
    Write-Host ""
    Write-Host "    Security:" -ForegroundColor White
    Write-Host "      - Bot Fight Mode: On"
    Write-Host "      - Browser Integrity Check: On"
    Write-Host "      - WAF Managed Rules: On"
    Write-Host ""
    Write-Info "Full details: infra\cloudflare\README.md"

    Save-State 5
    Wait-ForUser 4
}

###############################################################################
# STEP 5: Grafana Cloud
###############################################################################
if ($StartStep -le 5) {
    Write-StepBanner 5 "Grafana Cloud Monitoring Setup"

    Write-Host "    Set up Grafana Cloud to receive OpenTelemetry data from your API."
    Write-Host ""
    Write-Host "    1. Create account:" -ForegroundColor White
    Write-Host "       https://grafana.com/auth/sign-up/create-user (free tier)" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "    2. Get OTLP credentials:" -ForegroundColor White
    Write-Host "       Home > Connections > Add new connection > OpenTelemetry (OTLP)"
    Write-Host "       Click Configure > Generate API token"
    Write-Host "       Note: endpoint, instance ID, and API token"
    Write-Host ""
    Write-Host "    3. Generate the Base64 auth header (run in PowerShell):" -ForegroundColor White
    Write-Cmd '[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("INSTANCE_ID:API_TOKEN"))'
    Write-Host ""
    Write-Host "    4. Set env vars on Azure Container App:" -ForegroundColor White
    Write-Host ""
    Write-Host "       az containerapp update ``" -ForegroundColor DarkCyan
    Write-Host "         --name $AppName ``" -ForegroundColor DarkCyan
    Write-Host "         --resource-group $ResourceGroup ``" -ForegroundColor DarkCyan
    Write-Host '         --set-env-vars `' -ForegroundColor DarkCyan
    Write-Host '           "Otel__Endpoint=https://otlp-gateway-<region>.grafana.net/otlp" `' -ForegroundColor DarkCyan
    Write-Host '           "OTEL_EXPORTER_OTLP_HEADERS=Authorization=Basic <base64-value>" `' -ForegroundColor DarkCyan
    Write-Host '           "OTEL_EXPORTER_OTLP_PROTOCOL=grpc"' -ForegroundColor DarkCyan
    Write-Host ""
    Write-Host "    5. Import dashboards:" -ForegroundColor White
    Write-Host "       - ASP.NET Core (ID: 19924)"
    Write-Host "       - .NET Runtime (ID: 19925)"
    Write-Host ""
    Write-Info "Full details: infra\grafana\README.md"

    Save-State 6
    Wait-ForUser 5
}

###############################################################################
# STEP 6: Google OAuth
###############################################################################
if ($StartStep -le 6) {
    Write-StepBanner 6 "Google OAuth -- Production Redirect URI"

    Write-Host "    Update your Google OAuth app to use the production callback URL."
    Write-Host ""
    Write-Host "    In Google Cloud Console:" -ForegroundColor White
    Write-Host "       https://console.cloud.google.com/apis/credentials" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "    1. Click your OAuth 2.0 Client ID"
    Write-Host "    2. Under 'Authorized redirect URIs', add:"
    Write-Host ""
    Write-Host "       https://api.real-estate-star.com/oauth/google/callback" -ForegroundColor Green
    Write-Host ""
    Write-Host "    3. Under 'Authorized JavaScript origins', add:"
    Write-Host ""
    Write-Host "       https://platform.real-estate-star.com" -ForegroundColor Green
    Write-Host ""
    Write-Host "    4. If consent screen is in 'Testing' mode:"
    Write-Host "       Go to OAuth consent screen > Publish app"
    Write-Host ""
    Write-Host "    5. Set the redirect URI on Azure:" -ForegroundColor White
    Write-Host ""
    Write-Host "       az containerapp update ``" -ForegroundColor DarkCyan
    Write-Host "         --name $AppName ``" -ForegroundColor DarkCyan
    Write-Host "         --resource-group $ResourceGroup ``" -ForegroundColor DarkCyan
    Write-Host '         --set-env-vars `' -ForegroundColor DarkCyan
    Write-Host '           "Google__RedirectUri=https://api.real-estate-star.com/oauth/google/callback"' -ForegroundColor DarkCyan

    Save-State 7
    Wait-ForUser 6
}

###############################################################################
# STEP 7: Stripe Webhook
###############################################################################
if ($StartStep -le 7) {
    Write-StepBanner 7 "Stripe -- Register Production Webhook"

    Write-Host "    Register a webhook endpoint so Stripe can notify your API about payments."
    Write-Host ""
    Write-Host "    In Stripe Dashboard (LIVE mode):" -ForegroundColor White
    Write-Host "       https://dashboard.stripe.com/webhooks" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "    1. Click '+ Add endpoint'"
    Write-Host "    2. Endpoint URL:"
    Write-Host ""
    Write-Host "       https://api.real-estate-star.com/stripe/webhook" -ForegroundColor Green
    Write-Host ""
    Write-Host "    3. Select events to listen to:"
    Write-Host "       - checkout.session.completed"
    Write-Host ""
    Write-Host "    4. Click 'Add endpoint'"
    Write-Host ""
    Write-Host "    5. Copy the 'Signing secret' (starts with whsec_...)"
    Write-Host ""
    Write-Host "    Then update the secret on Azure:" -ForegroundColor White
    Write-Host ""
    Write-Host '       az containerapp secret set `' -ForegroundColor DarkCyan
    Write-Host "         --name $AppName ``" -ForegroundColor DarkCyan
    Write-Host "         --resource-group $ResourceGroup ``" -ForegroundColor DarkCyan
    Write-Host '         --secrets stripe-webhook-secret=<your-whsec-value>' -ForegroundColor DarkCyan
    Write-Host ""
    Write-Host "    And add to GitHub secrets:" -ForegroundColor White
    Write-Host "       STRIPE_WEBHOOK_SECRET -> whsec_..."
    Write-Host ""
    Write-Warn "Make sure you are in LIVE mode, not test mode!"

    Save-State 8
    Wait-ForUser 7
}

###############################################################################
# STEP 8: Smoke Tests
###############################################################################
if ($StartStep -le 8) {
    Write-StepBanner 8 "Smoke Tests & Verification"

    Write-Host "    Running automated verification checks..."
    Write-Host ""

    $errors = 0
    $warnings = 0
    $passed = 0

    # Azure login check
    if (Test-AzLoggedIn) {
        Write-Ok "Azure CLI logged in"
        $passed++
    } else {
        Write-Fail "Azure CLI not logged in"
        $errors++
    }

    # Container App check
    $appExists = $false
    try {
        $null = az containerapp show --name $AppName --resource-group $ResourceGroup --output none 2>$null
        $appExists = ($LASTEXITCODE -eq 0)
    } catch {}

    if ($appExists) {
        Write-Ok "Container App '$AppName' exists"
        $passed++

        $appUrl = Get-AppUrl

        # Health endpoints
        Write-Host ""
        Write-Info "Checking health endpoints..."

        try {
            $liveResponse = Invoke-WebRequest -Uri "https://$appUrl/health/live" -TimeoutSec 10 -UseBasicParsing -ErrorAction SilentlyContinue
            if ($liveResponse.StatusCode -eq 200) {
                Write-Ok "Liveness check passed (https://$appUrl/health/live)"
                $passed++
            } else {
                Write-Fail "Liveness returned $($liveResponse.StatusCode)"
                $errors++
            }
        } catch {
            Write-Fail "Liveness check failed -- API may not be running"
            $errors++
        }

        try {
            $readyResponse = Invoke-WebRequest -Uri "https://$appUrl/health/ready" -TimeoutSec 10 -UseBasicParsing -ErrorAction SilentlyContinue
            if ($readyResponse.StatusCode -eq 200) {
                Write-Ok "Readiness check passed"
                $passed++
            } else {
                Write-Warn "Readiness returned $($readyResponse.StatusCode)"
                $warnings++
            }
        } catch {
            Write-Warn "Readiness check failed -- some dependencies may not be configured yet"
            $warnings++
        }

        # Security headers
        Write-Host ""
        Write-Info "Checking security headers..."

        try {
            $headResponse = Invoke-WebRequest -Uri "https://$appUrl/health/live" -Method Head -TimeoutSec 10 -UseBasicParsing -ErrorAction SilentlyContinue
            $hdrs = $headResponse.Headers

            if ($hdrs.ContainsKey("X-Content-Type-Options")) {
                Write-Ok "X-Content-Type-Options header present"; $passed++
            } else {
                Write-Warn "X-Content-Type-Options header missing"; $warnings++
            }

            if ($hdrs.ContainsKey("X-Frame-Options")) {
                Write-Ok "X-Frame-Options header present"; $passed++
            } else {
                Write-Warn "X-Frame-Options header missing"; $warnings++
            }
        } catch {
            Write-Warn "Could not check security headers"
            $warnings++
        }

    } else {
        Write-Fail "Container App '$AppName' not found"
        $errors++
    }

    # Secrets check
    Write-Host ""
    Write-Info "Checking secrets are configured..."

    if (Test-AzLoggedIn) {
        $secretsJson = az containerapp secret list `
            --name $AppName `
            --resource-group $ResourceGroup `
            --output json 2>$null

        if ($secretsJson) {
            $secretsList = $secretsJson | ConvertFrom-Json
            $secretCount = $secretsList.Count
            if ($secretCount -gt 0) {
                Write-Ok "$secretCount secrets configured on Container App"
                $passed++
            } else {
                Write-Fail "No secrets found"
                $errors++
            }
        }
    }

    # DNS check
    Write-Host ""
    Write-Info "Checking DNS (optional)..."

    foreach ($sub in @("api", "platform")) {
        try {
            $dns = Resolve-DnsName "$sub.real-estate-star.com" -Type CNAME -ErrorAction SilentlyContinue
            if ($dns) {
                Write-Ok "$sub.real-estate-star.com DNS resolves"
                $passed++
            } else {
                Write-Warn "$sub.real-estate-star.com DNS not configured yet"
                $warnings++
            }
        } catch {
            Write-Warn "$sub.real-estate-star.com DNS not configured yet"
            $warnings++
        }
    }

    # Summary
    Write-Host ""
    Write-Host "  ----------------------------------------------------------------" -ForegroundColor Blue
    if ($errors -eq 0) {
        Write-Host "    All checks passed!" -ForegroundColor Green
    } else {
        Write-Host "    $errors issue(s) found. Review the output above." -ForegroundColor Yellow
    }
    Write-Host "    $passed passed  |  $errors failed  |  $warnings warnings" -ForegroundColor DarkGray
    Write-Host "  ----------------------------------------------------------------" -ForegroundColor Blue

    # Clean up state file
    if (Test-Path $StateFile) { Remove-Item $StateFile }
    Save-State 9
}

###############################################################################
# Done!
###############################################################################
Write-Banner "Go-Live Complete!"

$appUrl = Get-AppUrl
if (-not $appUrl) { $appUrl = "<pending>" }

Write-Host "    Your Real Estate Star platform should now be live."
Write-Host ""
Write-Host "    Key URLs:" -ForegroundColor White
Write-Host "      API:      https://$appUrl"
Write-Host "      Platform: https://platform.real-estate-star.com"
Write-Host "      Agents:   https://<handle>.real-estate-star.com"
Write-Host ""
Write-Host "    Post-deploy:" -ForegroundColor White
Write-Host "      - Monitor Grafana dashboards for 30 min"
Write-Host "      - Test onboarding flow end-to-end in browser"
Write-Host "      - Test CMA submission end-to-end"
Write-Host "      - Verify Stripe webhook delivery"
Write-Host "      - Trigger a test alert to confirm notifications work"
Write-Host ""
Write-Host "    Full checklist: docs\production-checklist.md" -ForegroundColor Cyan
Write-Host ""

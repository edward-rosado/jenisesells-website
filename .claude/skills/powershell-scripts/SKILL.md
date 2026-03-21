---
name: powershell-scripts
description: >
  Windows PowerShell script creation and maintenance patterns. Use this skill whenever writing,
  reviewing, or fixing .ps1 scripts -- especially deployment scripts, automation runbooks,
  verification scripts, or any script that will run on Windows PowerShell. Trigger on mentions
  of "PowerShell", ".ps1", "Windows script", or when writing scripts that target Windows users.
  This skill encodes hard-won lessons about encoding, prerequisites, idempotency, and
  cross-platform gotchas that prevent common failures on real Windows machines.
---

# PowerShell Script Patterns

This skill captures patterns for writing PowerShell scripts that actually work on real Windows
machines. Every rule here comes from a production failure or user-reported bug.

## Encoding: ASCII Only

Windows PowerShell (5.1) and many Windows terminals mangle non-ASCII characters. This is the
single most common cause of parse errors in PowerShell scripts created by LLMs.

**Hard rule: every .ps1 file must contain ONLY ASCII characters (byte values 0-127).**

Replace these problematic characters:

| Instead of | Use |
|------------|-----|
| em dash `--` (U+2014) | `--` (two hyphens) |
| en dash `-` (U+2013) | `-` (hyphen) |
| smart quotes `""` | `""` (straight quotes) |
| box drawing `=/-` etc | `===`, `---`, `|` (pipe) |
| check marks, arrows, bullets | `[OK]`, `->`, `-` |
| any emoji | spell it out or skip it |

After writing any .ps1 file, mentally scan for non-ASCII. If in doubt, the verification
step below catches them.

## Verification Step

After creating or modifying any .ps1 file, run this check:

```bash
python3 -c "
import sys
content = open(sys.argv[1], 'rb').read()
bad = [(i+1, ch) for i, ch in enumerate(content) if ch > 127]
if bad:
    for pos, ch in bad[:10]:
        print(f'  Byte {pos}: 0x{ch:02x}')
    print(f'  Total non-ASCII bytes: {len(bad)}')
    sys.exit(1)
else:
    print('  All ASCII - clean')
" path/to/script.ps1
```

## Prerequisites & Auto-Install

Every script that depends on external tools (az CLI, git, docker, node, etc.) should check
for them upfront and attempt auto-install before doing any real work.

### Pattern: Prerequisite Check Block

Place this BEFORE any business logic, right after parameter parsing:

```powershell
function Test-CommandExists($Name) {
    $null = Get-Command $Name -ErrorAction SilentlyContinue
    return $?
}

function Refresh-PathEnv {
    # Reload PATH so newly installed tools are found without restarting
    $machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
    $userPath    = [Environment]::GetEnvironmentVariable("Path", "User")
    $env:Path = "$machinePath;$userPath"
}

function Install-WithWinget($PackageId, $ToolName, $ManualUrl) {
    $hasWinget = Test-CommandExists "winget"
    if ($hasWinget) {
        Write-Host "  Installing $ToolName via winget..." -ForegroundColor Cyan
        winget install --id $PackageId --accept-package-agreements --accept-source-agreements
        if ($LASTEXITCODE -eq 0) {
            Refresh-PathEnv
            return $true
        }
    }
    Write-Host "  Install manually: $ManualUrl" -ForegroundColor Cyan
    return $false
}
```

### Common tool checks:

| Tool | winget ID | Manual URL |
|------|-----------|------------|
| Azure CLI | `Microsoft.AzureCLI` | https://aka.ms/installazurecliwindows |
| Git | `Git.Git` | https://git-scm.com/download/win |
| Node.js | `OpenJS.NodeJS.LTS` | https://nodejs.org |
| .NET SDK | `Microsoft.DotNet.SDK.9` | https://dot.net |
| Docker | (manual only) | https://docker.com/products/docker-desktop |

Docker Desktop requires manual install -- winget cannot install it. But this does NOT mean
Docker should be a soft warning. Docker must be a HARD prerequisite that blocks execution:

1. Check `docker` command exists
2. Check Docker daemon is running (`docker info`)
3. If EITHER check fails, set `$prereqFailed = $true` and show install/start instructions
4. The script stops before doing ANY work (no Azure login, no provider registration, nothing)

**The rule: EVERY dependency check must be a hard stop.** Never let the script start real work
if a dependency is missing. Users should not waste 5 minutes on Azure setup only to fail at
Docker build. Check everything upfront, fail fast.

```powershell
# Docker check pattern (hard stop, not a warning)
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
```

### Post-install PATH gotcha

`winget install` adds tools to the system PATH, but the current PowerShell session does not
see the change. Always call `Refresh-PathEnv` after installing, and if the tool still is not
found, tell the user to close and reopen PowerShell.

## Hard Prerequisites -- No Soft Warnings

**CRITICAL**: Every tool the script depends on must be checked upfront as a HARD prerequisite.
If any dependency is missing, the script must stop BEFORE doing any real work. Never use soft
warnings like "you will need this later" -- the user will forget, waste time on earlier steps,
and then hit the error deep in the process.

Pattern:
1. Check every dependency at the top of the script (before any business logic)
2. For each missing dependency: set `$prereqFailed = $true`, show install instructions
3. After all checks: `if ($prereqFailed) { exit 1 }`
4. Dependencies include: CLI tools, running daemons, network connectivity, permissions

This applies to BOTH the PowerShell wrapper AND any bash scripts it calls. If `setup.sh`
needs Docker, then `go-live.ps1` must check for Docker BEFORE calling `setup.sh`. Don't let
the user get 5 minutes into Azure setup only to fail because Docker isn't installed.

Also check dependencies in the called scripts as a safety net (in case someone runs them
directly), but the PRIMARY check must be in the entry-point script.

## Idempotency / Re-Runnability

Every script should be safe to run multiple times. The user might hit an error halfway through,
fix the issue, and re-run. The script must not fail or duplicate work.

### Patterns:

- **Check before create**: Before creating Azure resources, check if they exist first.
  ```powershell
  $null = az group show --name $rgName --output none 2>$null
  if ($LASTEXITCODE -eq 0) {
      Write-Host "  [OK] Resource group already exists" -ForegroundColor Green
  } else {
      az group create --name $rgName --location $location
  }
  ```

- **Save progress**: For multi-step runbooks, save the current step to a state file so the
  user can resume where they left off.
  ```powershell
  $StateFile = Join-Path $PSScriptRoot ".state"
  function Save-State($StepNum) { Set-Content -Path $StateFile -Value $StepNum -NoNewline }
  ```

- **Skip-if-done**: Each step checks its own completion condition before doing work.

- **Provide -Reset flag**: Let users clear saved state and start fresh.

- **Provide -Step flag**: Let users jump to a specific step.

## Finding Bash on Windows

When a script needs to call .sh files, search for bash in this order:

```powershell
# 1. Git Bash (most common on Windows)
$bashCmd = $null
if (Test-CommandExists "git") {
    $gitDir = Split-Path (Get-Command git).Source
    $gitBash = Join-Path (Split-Path $gitDir) "bin\bash.exe"
    if (Test-Path $gitBash) { $bashCmd = $gitBash }
}
# 2. bash on PATH (WSL or other)
if (-not $bashCmd -and (Test-CommandExists "bash")) { $bashCmd = "bash" }
# 3. WSL explicitly
if (-not $bashCmd -and (Test-CommandExists "wsl")) { $bashCmd = "wsl" }
```

## Output Formatting

Use simple ASCII prefixes for status output:

```powershell
function Write-Ok($Msg)   { Write-Host "    [OK]   $Msg" -ForegroundColor Green }
function Write-Warn($Msg) { Write-Host "    [WARN] $Msg" -ForegroundColor Yellow }
function Write-Fail($Msg) { Write-Host "    [FAIL] $Msg" -ForegroundColor Red }
function Write-Info($Msg)  { Write-Host "    [-->]  $Msg" -ForegroundColor Cyan }
```

Never use box drawing characters, Unicode bullets, or fancy borders. Keep it clean.

## Script Structure Template

Every non-trivial PowerShell script should follow this order:

1. `#Requires -Version 5.1`
2. Comment-based help (`.SYNOPSIS`, `.DESCRIPTION`, `.PARAMETER`, `.EXAMPLE`)
3. `param()` block
4. `Set-StrictMode -Version Latest` + `$ErrorActionPreference = "Stop"`
5. Configuration variables
6. Helper functions
7. **Prerequisites check** (with auto-install)
8. Business logic
9. Summary / exit code

## Azure CLI stderr Warnings

Azure CLI (`az`) sends WARNING messages to stderr even on success (e.g., `az provider register`
outputs "Registering is still on-going..."). PowerShell with `$ErrorActionPreference = "Stop"`
treats ANY stderr output as a terminating error, which crashes the script.

**Fix**: Temporarily set `$ErrorActionPreference = "Continue"` around `az` calls that produce
warnings, or redirect stderr with `2>&1`:

```powershell
# Option 1: Temporarily relax error handling
$oldEAP = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$null = az provider register --namespace $provider --output none 2>&1
$ErrorActionPreference = $oldEAP

# Option 2: Redirect stderr per-command
$null = az provider register --namespace $provider --output none 2>&1
```

This applies to many `az` commands -- always test for stderr warnings.

## Azure Resource Provider Registration

New Azure subscriptions do not have resource providers registered by default. Before creating
resources like Container Registry, Container Apps, or Log Analytics, register the providers.

**Use `--wait` flag**: `az provider register` returns immediately by default -- the registration
continues in the background. If you proceed to create resources before registration finishes,
you get `MissingSubscriptionRegistration` errors. Always use `--wait` to block until done, then
double-check the state:

```powershell
$requiredProviders = @(
    "Microsoft.ContainerRegistry",
    "Microsoft.App",
    "Microsoft.OperationalInsights"
)

foreach ($provider in $requiredProviders) {
    $state = az provider show --namespace $provider --query "registrationState" --output tsv 2>$null
    if ($state -ne "Registered") {
        # --wait blocks until registration completes (can take 1-3 minutes)
        $null = az provider register --namespace $provider --wait 2>&1
        # Always verify -- don't trust exit code alone
        $state = az provider show --namespace $provider --query "registrationState" --output tsv 2>$null
        if ($state -ne "Registered") {
            Write-Fail "$provider did not register (state: $state)"
        }
    }
}
```

## Failure Must Stop Execution

When a critical step fails, the script must stop and NOT print success messages. A common
bug is when the failure path falls through to code that prints results assuming success:

```powershell
# BAD -- falls through to "Save this URL" even when $ran is $false
$ran = Invoke-SetupSh
if ($ran) {
    $appUrl = Get-AppUrl
    Write-Ok "Created! URL: https://$appUrl"
}
# This still runs on failure:
Write-Host "Save this URL: https://$appUrl"  # prints "https://"

# GOOD -- exit on failure
$ran = Invoke-SetupSh
if ($ran) {
    $appUrl = Get-AppUrl
    Write-Ok "Created! URL: https://$appUrl"
} else {
    Write-Fail "Setup failed. Fix the error above and re-run."
    Save-State $currentStep
    exit 1
}
# Only reaches here on success
Write-Host "Save this URL: https://$appUrl"
```

Every `if ($success)` block that has post-success logic MUST have an `else` that stops execution.

## Deploy Scripts: Main-Branch Guard

**CRITICAL**: Every deploy script (production or preview) MUST verify it is running from the `main`
branch before doing ANY work. This prevents accidental deployments from feature branches.

Place this guard BEFORE prerequisites, right after the banner:

```powershell
# --- Branch Guard: only deploy from main ---
$currentBranch = git -C $RepoRoot rev-parse --abbrev-ref HEAD 2>$null
if ($currentBranch -ne "main") {
    Write-Fail "You are on branch '$currentBranch' -- production deploys must come from 'main'."
    Write-Fail "Merge your changes to main first, then re-run."
    exit 1
}
Write-Ok "On branch 'main'"
```

This is a hard stop -- no `-Force` flag to bypass it. If you need to test a deploy from a branch,
use a separate preview/staging workflow, not the production deploy script.

## Common Gotchas

- **SecureString to plain text** (for secret input):
  ```powershell
  $secure = Read-Host -Prompt "Enter secret" -AsSecureString
  $plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
      [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
  )
  ```

- **`.NET Framework vs .NET Core` API differences**: Windows PowerShell 5.1 runs on .NET
  Framework, NOT .NET Core/5+. Many newer .NET APIs are unavailable. Common trap:

  ```powershell
  # BAD -- RandomNumberGenerator.Fill() is .NET Core 2.1+ only
  [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)

  # GOOD -- RNGCryptoServiceProvider works on .NET Framework 4.x
  (New-Object System.Security.Cryptography.RNGCryptoServiceProvider).GetBytes($bytes)
  ```

  Other .NET Core-only APIs that don't exist in PowerShell 5.1:
  - `System.Text.Json` (use `ConvertTo-Json` / `ConvertFrom-Json` instead)
  - `System.IO.Path.GetRelativePath()` (compute manually)
  - `HttpClient` async patterns (use `Invoke-WebRequest` instead)

- **Backtick line continuation**: PowerShell uses backtick (`` ` ``) for line continuation,
  NOT backslash. And the backtick must be the LAST character on the line -- no trailing spaces.

- **`$LASTEXITCODE` vs `$?`**: `$LASTEXITCODE` is for native commands (az, git, docker).
  `$?` is for PowerShell cmdlets. Check the right one.

- **`2>$null` redirection**: Suppresses stderr from native commands. Use it when checking
  if resources exist (e.g., `az group show ... 2>$null`).

- **String interpolation in single quotes**: Single-quoted strings are literal in PowerShell.
  Use double quotes when you need variable expansion: `"Hello $name"` vs `'Hello $name'`.

- **`$Var:` is parsed as a drive reference, not "variable then colon"**: PowerShell interprets
  `$Foo:` as a reference to a variable on the `Foo:` PSDrive, not as `$Foo` followed by a colon.
  This causes `InvalidVariableReferenceWithDrive` parse errors. Always use `${Var}:` to delimit:

  ```powershell
  # BAD -- PowerShell sees $Num: as a drive reference, script fails to parse
  Write-Host "Step $Num: $Msg"

  # GOOD -- ${} delimits the variable name from the colon
  Write-Host "Step ${Num}: ${Msg}"
  ```

  This is especially common in Write-Host helper functions that format output with colons.

- **Native commands + `$ErrorActionPreference = "Stop"` (RECURRING BUG -- READ THIS)**:
  When calling native commands that write to stderr, PowerShell with `Stop` mode treats ALL
  stderr output as a terminating error. The script throws and crashes instead of continuing.

  **This is the #1 recurring bug.** It affects EVERY native command that writes warnings or
  progress to stderr -- not just `az`. Common offenders:
  - `az` -- WARNING messages to stderr even on success
  - `npm` / `npx` -- `npm warn deprecated ...` to stderr during install
  - `docker` -- progress and layer info to stderr
  - `git` -- "Switched to branch..." to stderr
  - `dotnet` -- "Determining projects to restore..." to stderr

  **Fix**: ALWAYS wrap native command calls with `$ErrorActionPreference = "Continue"`.
  `2>&1 | Out-Null` alone is NOT enough -- PowerShell intercepts stderr BEFORE the redirect.

  ```powershell
  # BAD -- npm warn on stderr crashes the script even with 2>&1
  npm install 2>&1 | Out-Null

  # GOOD -- temporarily relax error handling around native commands
  $savedEAP = $ErrorActionPreference
  $ErrorActionPreference = "Continue"
  $null = npm install 2>&1
  $exitCode = $LASTEXITCODE
  $ErrorActionPreference = $savedEAP
  if ($exitCode -ne 0) { Write-Fail "npm install failed"; exit 1 }
  ```

  **Apply this to EVERY native command call.** When writing a new script, grep for bare calls
  to az, npm, npx, docker, git, dotnet and wrap each one.

- **ACR Tasks not available on Azure Free tier**: `az acr build` uses ACR Tasks which requires
  paid tiers. Always use local `docker build` + `az acr login` + `docker push` instead. This
  works on all subscription types and also runs faster for iterative development.

- **`curl` is aliased to `Invoke-WebRequest` in PowerShell**: Never use `curl` with Unix-style
  flags (`-sf`, `-o /dev/null`, `-w "%{http_code}"`) in .ps1 scripts. PowerShell's `curl` alias
  maps to `Invoke-WebRequest`, which has completely different parameters. Use `Invoke-WebRequest`
  directly:

  ```powershell
  # BAD -- curl flags don't work in PowerShell
  $result = curl -sf "https://example.com/health" -o /dev/null -w "%{http_code}"

  # GOOD -- use Invoke-WebRequest with try/catch for error status codes
  try {
      $resp = Invoke-WebRequest -Uri "https://example.com/health" -UseBasicParsing -TimeoutSec 10
      Write-Ok "/health -> $($resp.StatusCode)"
  } catch {
      $code = $_.Exception.Response.StatusCode.value__
      if ($code) {
          Write-Fail "/health -> $code"
      } else {
          Write-Fail "/health -> no response"
      }
  }
  ```

  Note: `Invoke-WebRequest` throws on non-2xx status codes when `$ErrorActionPreference = "Stop"`,
  so always wrap in try/catch. The status code is on the exception's Response object.

- **Docker caches layers by file checksum, not by tag**: When redeploying with the same `:latest`
  tag, Docker may reuse cached layers even if source files changed (especially if the build
  context was cached from a previous build). Azure Container Apps may also skip creating a new
  revision if the image tag hasn't changed. Two fixes:

  1. Use `--no-cache` on `docker build` to force a full rebuild
  2. Use unique version tags (e.g., timestamp-based `v20260312-183550`) instead of `:latest`
     so Azure always sees a new image and creates a fresh revision

  ```powershell
  $VersionTag = "v" + (Get-Date -Format "yyyyMMdd-HHmmss")
  docker build --no-cache -t "${ImageName}:${VersionTag}" -t "${ImageName}:latest" $DockerContext
  # Push the versioned tag -- Azure will always create a new revision
  docker push "${ImageName}:${VersionTag}"
  az containerapp update --name $AppName --resource-group $RG --image "${ImageName}:${VersionTag}"
  ```

- **Docker build context matters**: Run `docker build` from the correct directory. If the
  Dockerfile uses `COPY ["App/App.csproj", "App/"]`, the build context must be the parent
  directory containing `App/`. Running from the repo root with `-f apps/api/Dockerfile .`
  sends the ENTIRE repo (node_modules, .git, etc.) as build context -- potentially gigabytes.
  Always set the build context to the narrowest directory that contains all needed files:

  ```powershell
  # BAD -- sends entire repo as build context (2GB+)
  docker build -f apps/api/Dockerfile .

  # GOOD -- only sends apps/api/ as context
  docker build -t $ImageName apps/api
  ```

## Cross-Step Resource Dependencies

In multi-step deployment scripts, later steps often reference resources created by earlier steps
(e.g., step 3 assigns a role on an ACR created in step 1). These steps can be run out of order
or re-run after partial failures, so NEVER assume a resource exists just because an earlier step
"should have" created it.

**Hard rule: always check if a resource exists before operating on it across step boundaries.**

If the resource is missing, warn the user which step to run first -- don't crash the script.

```powershell
# BAD -- crashes if ACR doesn't exist yet
$acrId = az acr show --name $AcrName --query id --output tsv
az role assignment create --scope $acrId ...

# GOOD -- graceful degradation
$acrId = az acr show --name $AcrName --query id --output tsv 2>$null
if ($acrId) {
    az role assignment create --scope $acrId ...
    Write-Ok "Role assigned."
} else {
    Write-Warn "ACR '$AcrName' not found. Run step 1 first."
    Write-Warn "Re-run this step after to assign the role."
}
```

This applies to all cross-step dependencies: ACR, Container App, Log Analytics workspace,
managed identity, etc. Each step must be independently safe to run.

## Nuke / Clean Start Pattern

For deployment scripts, always provide a way to tear everything down and start over. Users
will hit errors, want to reset, and re-run. Add a `-Nuke` flag that:

1. Confirms with the user (double-check)
2. Deletes the resource group (which cascades to all resources inside it)
3. Clears saved progress state
4. Tells the user to wait and re-run

```powershell
param(
    [switch]$Nuke
)

if ($Nuke) {
    if (Confirm-Action "DELETE all Azure resources and start fresh?") {
        az group delete --name $rgName --yes --no-wait
        if (Test-Path $StateFile) { Remove-Item $StateFile }
        Write-Ok "Deletion started. Wait 2-3 min, then re-run."
    }
    exit 0
}
```

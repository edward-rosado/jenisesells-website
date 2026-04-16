<#
.SYNOPSIS
Re-triggers activation for an existing agent account.

.DESCRIPTION
Enqueues an activation request to Azure Queue Storage for an agent account.
Uses the same activation pipeline as platform onboarding.

.PARAMETER AccountHandle
The account handle (e.g., "jenise-buckalew", "safari-homes")

.PARAMETER ApiUrl
The API base URL (default: http://localhost:5135 for local dev)

.EXAMPLE
.\scripts\migrate-account.ps1 -AccountHandle "jenise-buckalew"
.\scripts\migrate-account.ps1 -AccountHandle "safari-homes" -ApiUrl "https://api.real-estate-star.com"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$AccountHandle,

    [Parameter(Mandatory=$false)]
    [string]$ApiUrl = "http://localhost:5135"
)

$ErrorActionPreference = "Stop"

Write-Host "Migrating account: $AccountHandle" -ForegroundColor Green

# Build the request
$body = @{
    accountHandle = $AccountHandle
} | ConvertTo-Json

Write-Host "Sending activation request to $ApiUrl/activation/trigger..."

try {
    $response = Invoke-WebRequest `
        -Uri "$ApiUrl/activation/trigger" `
        -Method Post `
        -Headers @{"Content-Type" = "application/json"} `
        -Body $body `
        -UseBasicParsing

    if ($response.StatusCode -eq 200) {
        Write-Host "✓ Activation triggered successfully" -ForegroundColor Green
        $result = $response.Content | ConvertFrom-Json
        Write-Host "Instance ID: $($result.instanceId)" -ForegroundColor Cyan
    } else {
        Write-Host "✗ Unexpected status code: $($response.StatusCode)" -ForegroundColor Red
        Write-Host $response.Content
        exit 1
    }
} catch {
    Write-Host "✗ Failed to trigger activation: $_" -ForegroundColor Red
    exit 1
}

Write-Host "`nMonitor progress at: $ApiUrl/activation/status?instanceId=$($result.instanceId)"

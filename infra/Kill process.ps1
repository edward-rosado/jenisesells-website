# Kill process by name: "jolly-relaxed-keller"
# Process ID: 3be70020-ec47-4a84-a717-79075f1a72df
 
$processName = "jolly-relaxed-keller"
$processId = "3be70020-ec47-4a84-a717-79075f1a72df"
 
Write-Host "Attempting to kill process: $processName (ID: $processId)" -ForegroundColor Yellow
 
# Try finding by name first
$proc = Get-Process -Name $processName -ErrorAction SilentlyContinue
 
if ($proc) {
    Stop-Process -Name $processName -Force
    Write-Host "Process '$processName' killed successfully." -ForegroundColor Green
} else {
    Write-Host "Process not found by name. Trying by window title..." -ForegroundColor Cyan
 
    # Try matching by window title or command line via WMI
    $wmiProc = Get-WmiObject Win32_Process | Where-Object {
        $_.Name -like "*$processName*" -or $_.CommandLine -like "*$processName*" -or $_.CommandLine -like "*$processId*"
    }
 
    if ($wmiProc) {
        $wmiProc | ForEach-Object {
            Write-Host "Found process: $($_.Name) (PID: $($_.ProcessId))" -ForegroundColor Cyan
            Stop-Process -Id $_.ProcessId -Force
            Write-Host "Killed PID $($_.ProcessId)" -ForegroundColor Green
        }
    } else {
        Write-Host "No matching process found. It may have already exited." -ForegroundColor Red
        Write-Host ""
        Write-Host "If this is a Docker container, try:" -ForegroundColor Yellow
        Write-Host "  docker stop $processName" -ForegroundColor White
        Write-Host "  docker rm $processName" -ForegroundColor White
        Write-Host ""
        Write-Host "If this is a Claude Code / MCP session, try:" -ForegroundColor Yellow
        Write-Host "  claude sessions list" -ForegroundColor White
        Write-Host "  claude sessions kill $processId" -ForegroundColor White
    }
}
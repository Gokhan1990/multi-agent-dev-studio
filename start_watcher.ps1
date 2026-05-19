param(
    [string]$ProjectDir = $PSScriptRoot,
    [string]$PowerShellExe = "powershell.exe",
    [string]$DashboardUrl = "http://localhost:3000"
)

$watchScript = Join-Path $ProjectDir "watch_commands.ps1"
$watchdogScript = Join-Path $ProjectDir "watchdog.ps1"

Write-Host "=== opencode CLI Watcher + Watchdog ===" -ForegroundColor Cyan
Write-Host "Watcher:  $watchScript"
Write-Host "Watchdog: $watchdogScript"
Write-Host ""

# Kill any existing watcher/watchdog
Get-Process -Name "powershell" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -match "watch_commands|watchdog" } |
    Stop-Process -Force
Start-Sleep 1
Write-Host "Eski process'ler temizlendi" -ForegroundColor Yellow

# Start watchdog (loop that keeps watcher alive)
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $PowerShellExe
$psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$watchdogScript`" `"$ProjectDir`""
$psi.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
$psi.UseShellExecute = $true
$wdProc = [System.Diagnostics.Process]::Start($psi)

Start-Sleep -Seconds 3

$watcher = Get-Process -Name "powershell" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -match "watch_commands" }

if ($watcher) {
    Write-Host "OK Watcher PID: $($watcher.Id)" -ForegroundColor Green
    Write-Host "OK Watchdog PID: $($wdProc.Id)" -ForegroundColor Green
    Write-Host ""
    Write-Host "Pipeline hazir! $DashboardUrl" -ForegroundColor Cyan
    Write-Host "Komut verince opencode (deepseek v4 flash free) otomatik isler." -ForegroundColor White
    Write-Host "ActivityTerminal + chat transcript'te canli goruntule." -ForegroundColor White
} else {
    Write-Host "HATA: Watcher baslatilamadi!" -ForegroundColor Red
}

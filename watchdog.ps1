param(
    [string]$ProjectDir = $PSScriptRoot,
    [int]$HeartbeatTimeoutSeconds = 30,
    [int]$CheckSeconds = 10
)

$watchScript = Join-Path $ProjectDir "watch_commands.ps1"
$heartbeatFile = Join-Path (Join-Path $ProjectDir "Commands") "watcher.heartbeat"
$watchdogLog = Join-Path (Join-Path $ProjectDir "Commands") "watchdog.log"

function Write-WatchdogLog($Text, $Color = "White") {
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $line = "[$timestamp] $Text"
    Write-Host $line -ForegroundColor $Color
    Add-Content -Path $watchdogLog -Value $line -Encoding UTF8
}

function Get-WatcherProcess {
    Get-Process -Name "powershell" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -match "watch_commands" }
}

function Start-Watcher {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "powershell.exe"
    $psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$watchScript`" `"$ProjectDir`""
    $psi.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
    $psi.UseShellExecute = $true
    $proc = [System.Diagnostics.Process]::Start($psi)
    Start-Sleep -Seconds 2
    return $proc
}

Write-WatchdogLog "=== opencode CLI Watchdog basliyor ===" "Cyan"
Write-WatchdogLog "Heartbeat: ${HeartbeatTimeoutSeconds}s  Check: ${CheckSeconds}s" "Cyan"

$lastHeartbeat = 0
$startCount = 0

while ($true) {
    try {
        $watcher = Get-WatcherProcess

        if (-not $watcher) {
            Write-WatchdogLog "Watcher bulunamadi, baslatiliyor..." "Yellow"
            $startCount++
            $proc = Start-Watcher
            if ($proc -and -not $proc.HasExited) {
                Write-WatchdogLog "Watcher baslatildi (PID: $($proc.Id), baslatma: $startCount)" "Green"
            } else {
                Write-WatchdogLog "Watcher baslatilamadi!" "Red"
            }
        }

        # Verify watcher process is actually alive (not just ghost)
        if ($watcher) {
            try {
                $alive = -not $watcher.HasExited
                if (-not $alive) {
                    Write-WatchdogLog "Watcher process exited, restarting..." "Yellow"
                    $watcher | Stop-Process -Force -ErrorAction SilentlyContinue
                    continue
                }
            } catch {
                Write-WatchdogLog "Watcher check error: $_" "Yellow"
            }
        }

    } catch {
        Write-WatchdogLog "Watchdog hatasi: $_" "Red"
    }
    Start-Sleep -Seconds $CheckSeconds
}

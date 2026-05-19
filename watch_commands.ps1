param(
    [string]$ProjectDir = $PSScriptRoot,
    [string]$Model = "opencode/deepseek-v4-flash-free",
    [int]$PollSeconds = 3,
    [int]$CmdTimeoutSeconds = 120
)

$queuePath = Join-Path $ProjectDir "Commands\queue.json"
$opencodeExe = "C:\Users\User\AppData\Roaming\npm\opencode.cmd"
$heartbeatFile = Join-Path (Join-Path $ProjectDir "Commands") "watcher.heartbeat"

function Write-Log($Text, $Color = "White") {
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Text" -ForegroundColor $Color
}

function Get-Queue {
    if (!(Test-Path $queuePath)) { return @() }
    try {
        $result = Get-Content $queuePath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($result -is [array]) { return @($result) }
        return @($result)
    } catch { return @() }
}

function Save-Queue($commands) {
    $list = @($commands)
    if ($list.Count -eq 0) { Set-Content $queuePath -Value "[]" -Encoding UTF8; return }
    $parts = foreach ($item in $list) { $item | ConvertTo-Json -Depth 10 }
    $json = "[$($parts -join ",`r`n")]"
    Set-Content $queuePath -Value $json -Encoding UTF8
}

function Add-Log($commands, $cmdId, $agentId, $text, $level) {
    $ansiEscape = [char]27
    $clean = "$text" -replace "$ansiEscape\[[0-9;]*[a-zA-Z]", '' -replace "$ansiEscape", '' -replace '[^\x20-\x7E\x80-\xFFÇçĞğİıÖöŞşÜü]', ''
    $clean = $clean.Trim()
    if ([string]::IsNullOrWhiteSpace($clean)) { return }
    $entry = @{ Text = $clean; Level = $level; AgentId = $agentId; Timestamp = (Get-Date).ToUniversalTime().ToString("o") }
    foreach ($c in $commands) { if ($c.Id -eq $cmdId) { if (-not $c.Logs) { $c.Logs = @() }; $c.Logs += $entry; break } }
}

function Process-Command($cmd) {
    $agentId = if ($cmd.ActiveAgentId) { $cmd.ActiveAgentId } else { "bora" }
    Write-Log "Komut: $($cmd.Text) [$agentId]" "Yellow"

    $commands = Get-Queue
    Add-Log $commands $cmd.Id $agentId "openCLI isleniyor..." "info"
    Save-Queue $commands

    $taskDesc = "Proje: AI Software Company OS (.NET 8 + React). Istek: $($cmd.Text). Bu degisikligi yap."

    # Run opencode in background job with timeout
    $job = Start-Job -ScriptBlock {
        param($exe, $task, $model, $dir)
        $task | & $exe run -m $model --dangerously-skip-permissions --dir $dir 2>&1
    } -ArgumentList $opencodeExe, $taskDesc, $Model, $ProjectDir

    if (Wait-Job $job -Timeout $CmdTimeoutSeconds) {
        $errLines = Receive-Job $job
    } else {
        Stop-Job $job
        $errLines = @("TIMEOUT: opencode $CmdTimeoutSeconds saniye asimi")
        Write-Log "TIMEOUT: opencode $CmdTimeoutSeconds saniyede tamamlanamadi" "Red"
    }
    Remove-Job $job -Force

    $commands = Get-Queue
    if (-not $commands -or $commands.Count -eq 0) { $commands = @($cmd) }

    $ansiEscape = [char]27
    $noisePatterns = @('^System\.Management\.Automation', '^RemoteException')
    $hasError = $false
    foreach ($line in $errLines) {
        $clean = "$line" -replace "$ansiEscape\[[0-9;]*[a-zA-Z]", '' -replace "$ansiEscape", '' -replace '[^\x20-\x7E\x80-\xFFÇçĞğİıÖöŞşÜü]', ''
        $clean = $clean.Trim()
        if ([string]::IsNullOrWhiteSpace($clean) -or $clean.Length -lt 3) { continue }
        $isNoise = $false
        foreach ($np in $noisePatterns) { if ($clean -match $np) { $isNoise = $true; break } }
        if ($isNoise) { continue }
        if ($clean -match "✗|error|fail|hata|Error|Failed|TIMEOUT") { $hasError = $true }
        $level = if ($clean -match "✓|success|başarılı|tamamlandı|oluşturuldu|güncellendi|yazıldı|Wrote|Created|Done|completed|Yapıldı") { "success" }
                 elseif ($clean -match "✗|error|fail|hata|Error|Failed|TIMEOUT") { "error" }
                 elseif ($clean -match "Read|Glob|•|okunuyor|analiz") { "info" }
                 elseif ($clean -match "\+|-|@@|Index|===|---") { "cmd" }
                 else { "info" }
        Add-Log $commands $cmd.Id $agentId $clean $level
    }

    foreach ($c in $commands) {
        if ($c.Id -eq $cmd.Id) {
            if ($hasError) {
                $c.Status = "failed"; $c.Error = "opencode error"
                Add-Log $commands $cmd.Id $agentId "openCLI hatayla tamamlandi" "error"
                Write-Log "openCLI hatayla tamamlandi" "Red"
            } else {
                $c.Status = "completed"
                Add-Log $commands $cmd.Id $agentId "openCLI basariyla tamamlandi" "success"
                Write-Log "openCLI basariyla tamamlandi" "Green"
            }
            $c.ProcessedAt = (Get-Date).ToUniversalTime().ToString("o")
            $c.ActiveAgentId = $null
        }
    }
    Save-Queue $commands

    # AgentMemory log
    $logScript = Join-Path $ProjectDir "AgentMemory\log_agent.ps1"
    if (Test-Path $logScript) {
        $agentName = if ($cmd.ActiveAgentId) { $cmd.ActiveAgentId } else { "bora" }
        $status = if ($hasError) { "failed" } else { "completed" }
        $allLogs = @($commands | Where-Object { $_.Id -eq $cmd.Id } | Select-Object -ExpandProperty Logs)
        $outputText = ($allLogs | ForEach-Object { $_.Text }) -join "`n"
        if ($outputText.Length -gt 2000) { $outputText = $outputText.Substring(0, 2000) + "..." }
        try {
            & $logScript -AgentName $agentName -Task $cmd.Text -Action "opencode run" -Output $outputText -Room "MeetingRoom" -Status $status 2>&1 | Out-Null
        } catch { Write-Log "AgentMemory log hatasi: $_" "DarkYellow" }
    }
}

Write-Log "=== opencode CLI Watcher v3 basliyor ===" "Cyan"
Write-Log "Model: $Model  Timeout: ${CmdTimeoutSeconds}s  Poll: ${PollSeconds}s" "Cyan"

$script:isProcessing = $false

while ($true) {
    try {
        # Update heartbeat
        "$($script:isProcessing)" | Set-Content $heartbeatFile -Encoding UTF8

        if ($script:isProcessing) { Start-Sleep -Seconds $PollSeconds; continue }

        $commands = Get-Queue
        if (-not $commands) { $commands = @() }

        foreach ($cmd in $commands) {
            if ($cmd.Status -eq "pending") {
                $script:isProcessing = $true
                try {
                    foreach ($c in $commands) { if ($c.Id -eq $cmd.Id) { $c.Status = "processing" } }
                    Save-Queue $commands

                    $currentCmd = Get-Queue | Where-Object { $_.Id -eq $cmd.Id }
                    if ($currentCmd) { Process-Command $currentCmd }
                } finally {
                    $script:isProcessing = $false
                }
                break
            }
        }
    } catch {
        Write-Log "Watcher hatasi: $_" "Red"
        $script:isProcessing = $false
    }
    Start-Sleep -Seconds $PollSeconds
}

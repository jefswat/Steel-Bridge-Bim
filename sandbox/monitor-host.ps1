# Host-side monitor for the Windows Sandbox VM. Nothing runs inside the sandbox: it samples the host's own view of the
# VM (vmwp.exe = Hyper-V worker, vmmemWindowsSandbox = guest memory) plus host memory / CPU / disk and the OneDrive
# client (the repo mapped into the sandbox lives in OneDrive), and the Claude session files the sandbox writes to the
# mapped cache. One CSV row per sample; when the VM worker disappears it writes a CRASH block with the last samples
# and the host events around it. 2026-10-20 21:52: crashed with OneDrive OFF and 16 GB free, right after a burst of vmwp "other" I/O ops (mapped folders; repo then in OneDrive). Watch host_commit_pct and onedrive_private_mb: on 2026-10-20 OneDrive held 31 GB private
# memory (commit 51.6 of 60.9 GB before the 24 GB sandbox started) - the likely cause of the vmwp.exe crashes.
#   powershell -NoProfile -ExecutionPolicy Bypass -File sandbox\monitor-host.ps1 [-Seconds 5]
# Output: %USERPROFILE%\SBMSandbox\monitor\monitor-<yyyyMMdd-HHmmss>.csv + events.log (CRASH / START lines).
param([int]$Seconds = 5)
$ErrorActionPreference = 'Continue'
$dir = Join-Path $env:USERPROFILE 'SBMSandbox\monitor'
New-Item -ItemType Directory -Force $dir | Out-Null
$csv = Join-Path $dir ("monitor-{0:yyyyMMdd-HHmmss}.csv" -f (Get-Date))
$events = Join-Path $dir 'events.log'
$claudeDir = Join-Path $env:USERPROFILE 'SBMSandbox\claude\projects\C--SBM'
# Appends with retries: a reader that held events.log open (`tail -F`) made Add-Content fail silently on 2026-10-20, so
# the CRASH block of the 21:52 crash was lost. Watch the file with a polling reader (Get-Content) instead.
function Append($path, $text) {
    for ($i = 0; $i -lt 20; $i++) {
        try { [IO.File]::AppendAllText($path, $text + "`r`n"); return } catch { Start-Sleep -Milliseconds 250 }
    }
}
function Say($s) { $line = "{0:yyyy-MM-dd HH:mm:ss} {1}" -f (Get-Date), $s; Append $events $line; Write-Output $line }

$cols = 'time,vmwp_pid,vmwp_cpu_pct,vmwp_ws_mb,vmwp_private_mb,vmwp_handles,vmwp_threads,vmwp_io_read_mbps,vmwp_io_write_mbps,vmwp_io_other_ops,' +
        'vmmem_ws_mb,host_avail_mb,host_commit_pct,host_cpu_pct,disk_read_mbps,disk_write_mbps,disk_queue,onedrive_cpu_pct,onedrive_private_mb,claude_files_active,claude_session_kb'
Set-Content $csv $cols
$counters = '\Memory\Available MBytes', '\Memory\% Committed Bytes In Use', '\Processor(_Total)\% Processor Time',
            '\PhysicalDisk(_Total)\Disk Read Bytes/sec', '\PhysicalDisk(_Total)\Disk Write Bytes/sec', '\PhysicalDisk(_Total)\Current Disk Queue Length'
$prev = @{}; $prevT = Get-Date; $vmPid = $null; $tail = New-Object System.Collections.Generic.Queue[string]; $tick = 0; $claudeActive = ''; $sessKb = ''
$ncpu = [Environment]::ProcessorCount
Say "monitor started, csv $csv"
while ($true) {
    $now = Get-Date; $dt = ($now - $prevT).TotalSeconds; if ($dt -le 0) { $dt = $Seconds }
    # The sandbox VM worker: the vmwp.exe that started last before / with the WindowsSandboxRemoteSession (other VMs, e.g. WSL, have their own).
    $workers = @(Get-CimInstance Win32_Process -Filter "Name='vmwp.exe'" -ErrorAction SilentlyContinue)
    $rs = Get-Process WindowsSandboxRemoteSession -ErrorAction SilentlyContinue | Sort-Object StartTime | Select-Object -Last 1
    $w = $null
    if ($workers.Count) {
        $w = if ($vmPid -and ($workers | Where-Object ProcessId -eq $vmPid)) { $workers | Where-Object ProcessId -eq $vmPid }
             else { $workers | Sort-Object CreationDate | Select-Object -Last 1 }
    }
    if ($vmPid -and (-not $w -or $w.ProcessId -ne $vmPid)) {
        Say "CRASH/EXIT: VM worker pid $vmPid is gone (remote session: $(if ($rs) { 'still there' } else { 'gone' }))"
        Append $events "  last samples:"; foreach ($l in $tail) { Append $events "    $l" }
        Start-Sleep 3
        Get-WinEvent -FilterHashtable @{ LogName = 'Application', 'System'; StartTime = $now.AddMinutes(-2) } -ErrorAction SilentlyContinue |
            Where-Object { $_.Level -le 3 } | Select-Object -First 15 |
            ForEach-Object { Append $events ("    event {0:HH:mm:ss} {1} {2}: {3}" -f $_.TimeCreated, $_.ProviderName, $_.Id, (($_.Message -split "`n")[0])) }
        $vmPid = $null; $prev = @{}
    }
    if ($w -and $w.ProcessId -ne $vmPid) { $vmPid = $w.ProcessId; $prev = @{}; Say "START: VM worker pid $vmPid (created $($w.CreationDate))" }

    $row = @{}
    if ($w) {
        $p = Get-Process -Id $w.ProcessId -ErrorAction SilentlyContinue
        $cpu = if ($p) { $p.TotalProcessorTime.TotalSeconds } else { 0 }
        $rd = [double]$w.ReadTransferCount; $wr = [double]$w.WriteTransferCount; $oo = [double]$w.OtherOperationCount
        if ($prev.Count) {
            $row.cpu = [math]::Round(100 * ($cpu - $prev.cpu) / $dt / $ncpu, 1)
            $row.rd = [math]::Round(($rd - $prev.rd) / 1MB / $dt, 2); $row.wr = [math]::Round(($wr - $prev.wr) / 1MB / $dt, 2)
            $row.oo = [math]::Round(($oo - $prev.oo) / $dt)
        }
        $prev = @{ cpu = $cpu; rd = $rd; wr = $wr; oo = $oo }
        $row.ws = [math]::Round($w.WorkingSetSize / 1MB); $row.pm = [math]::Round($w.PrivatePageCount / 1MB)
        $row.h = $w.HandleCount; $row.t = $w.ThreadCount
    }
    $vmmem = Get-Process vmmemWindowsSandbox -ErrorAction SilentlyContinue | Select-Object -First 1
    $c = (Get-Counter $counters -ErrorAction SilentlyContinue).CounterSamples
    function C($name) { ($c | Where-Object Path -like "*$name").CookedValue }
    $od = Get-Process OneDrive -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($od) { $odCpu = $od.TotalProcessorTime.TotalSeconds; $odPct = if ($prevOd) { [math]::Round(100 * ($odCpu - $prevOd) / $dt / $ncpu, 1) } else { '' }; $prevOd = $odCpu } else { $odPct = '' }
    # Session files written in the last 30 s (agents at work); every 6th sample, the listing is the heaviest part.
    if ($tick % 6 -eq 0 -and (Test-Path $claudeDir)) {
        $recent = @(Get-ChildItem $claudeDir -Recurse -Filter *.jsonl -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -gt $now.AddSeconds(-30) })
        $claudeActive = $recent.Count
        $sessKb = [math]::Round((($recent | Measure-Object Length -Sum).Sum) / 1KB)
    }
    $line = @(
        $now.ToString('HH:mm:ss'), $vmPid, $row.cpu, $row.ws, $row.pm, $row.h, $row.t, $row.rd, $row.wr, $row.oo,
        $(if ($vmmem) { [math]::Round($vmmem.WorkingSet64 / 1MB) }),
        [math]::Round((C 'Available MBytes')), [math]::Round((C '% Committed Bytes In Use'), 1), [math]::Round((C '% Processor Time'), 1),
        [math]::Round((C 'Disk Read Bytes/sec') / 1MB, 2), [math]::Round((C 'Disk Write Bytes/sec') / 1MB, 2), (C 'Current Disk Queue Length'),
        $odPct, $(if ($od) { [math]::Round($od.PrivateMemorySize64 / 1MB) }), $claudeActive, $sessKb) -join ','
    Add-Content $csv $line
    $tail.Enqueue($line); while ($tail.Count -gt 24) { [void]$tail.Dequeue() }
    $tick++; $prevT = $now
    Start-Sleep -Seconds $Seconds
}

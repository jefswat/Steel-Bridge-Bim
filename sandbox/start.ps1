# Opens a Windows Sandbox with this repo, the .NET SDK and Claude Code (no permission prompts inside).
#   .\sandbox\start.ps1              normal start
#   .\sandbox\start.ps1 -Update      re-download the .NET SDK, Git and Claude Code
#   .\sandbox\start.ps1 -NoNetwork   no internet inside (Claude cannot work; for running the app only)
#   .\sandbox\start.ps1 -NoVGpu      virtual GPU off (test switch for the 2026-10-20 host vmwp.exe crashes; the likely
#                                    cause was a hung OneDrive.exe holding 31 GB, see monitor-host.ps1)
param([switch]$Update, [switch]$NoNetwork, [switch]$NoVGpu, [int]$MemoryMB = 24576)
$ErrorActionPreference = 'Stop'

if (-not (Test-Path "$env:windir\System32\WindowsSandbox.exe")) {
    Write-Host "Windows Sandbox is not enabled. In an ADMIN PowerShell run:" -ForegroundColor Yellow
    Write-Host "  Enable-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM -All"
    Write-Host "then reboot and run this script again."
    exit 1
}
if (Get-Process WindowsSandboxRemoteSession, WindowsSandboxClient -ErrorAction SilentlyContinue) {
    Write-Host "A Windows Sandbox is already running (only one can run at a time)." -ForegroundColor Yellow
    exit 1
}

$repo   = (Resolve-Path "$PSScriptRoot\..").Path
$cache  = Join-Path $env:USERPROFILE 'SBMSandbox'    # persists between sandbox sessions: tools, NuGet, Claude login (AppData cannot be mapped)
$inside = 'C:\SBM'                                # repo path inside the sandbox (a C:\Users\<host user> path cannot be mapped)
$memory = Join-Path $env:USERPROFILE (".claude\projects\" + ($repo -replace '[^A-Za-z0-9]', '-') + "\memory")
New-Item -ItemType Directory -Force $cache, $memory | Out-Null

# Facts the sandbox needs from the host.
@{
    Repo     = $inside
    Key      = ($inside -replace '[^A-Za-z0-9]', '-')   # Claude's project folder name inside the sandbox
    GitName  = (git config user.name)
    GitEmail = (git config user.email)
    Update   = [bool]$Update
    # Claude account ids that opted in to bypass permissions in the host desktop app: the sandbox app gets the same
    # 'allow bypass permissions' opt-in (allowed, not the default mode).
    BypassAccounts = @(try {
        $p = (Get-Content "$env:APPDATA\Claude\claude_desktop_config.json" -Raw | ConvertFrom-Json).preferences.bypassPermissionsOptInByAccount
        $p.PSObject.Properties | Where-Object Value | ForEach-Object Name
    } catch { })
} | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $cache 'host.json')

function Map($hostPath, $sandboxPath, $readOnly) {
    $ro = if ($readOnly) { 'true' } else { 'false' }
    "    <MappedFolder><HostFolder>$([Security.SecurityElement]::Escape($hostPath))</HostFolder>" +
    "<SandboxFolder>$([Security.SecurityElement]::Escape($sandboxPath))</SandboxFolder><ReadOnly>$ro</ReadOnly></MappedFolder>"
}
$maps = @(
    Map $repo $inside $false
    Map $cache 'C:\sbmcache' $false
    Map $memory 'C:\sbmmemory' $false      # Claude's project memory, shared with the Windows sessions
)
if (Test-Path 'C:\SBMData') { $maps += Map 'C:\SBMData' 'C:\SBMData' $true }

$setup = Join-Path $inside 'sandbox\setup.ps1'
$wsb = @"
<Configuration>
  <Networking>$(if ($NoNetwork) { 'Disable' } else { 'Default' })</Networking>
  <vGPU>$(if ($NoVGpu) { 'Disable' } else { 'Enable' })</vGPU>
  <MemoryInMB>$MemoryMB</MemoryInMB>
  <ClipboardRedirection>Enable</ClipboardRedirection>
  <PrinterRedirection>Disable</PrinterRedirection>
  <AudioInput>Disable</AudioInput>
  <VideoInput>Disable</VideoInput>
  <MappedFolders>
$($maps -join "`n")
  </MappedFolders>
  <LogonCommand>
    <Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$([Security.SecurityElement]::Escape($setup))"</Command>
  </LogonCommand>
</Configuration>
"@
$wsbPath = Join-Path $cache 'SteelBridgeModeler.wsb'
Set-Content -Encoding utf8 $wsbPath $wsb
Write-Host "Starting Windows Sandbox (setup log inside: C:\sbmcache\setup.log)..."
Start-Process $wsbPath

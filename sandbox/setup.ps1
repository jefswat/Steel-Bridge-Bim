# Runs INSIDE Windows Sandbox at logon (started by start.ps1). Tools are installed once into C:\sbmcache (a host
# folder), so later sessions only set PATH. Then opens Claude Code in the repo with permission prompts off.
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # Invoke-WebRequest is very slow with the progress bar
$cache = 'C:\sbmcache'
Start-Transcript -Path "$cache\setup.log" -Force | Out-Null

try {
    $hostInfo = Get-Content "$cache\host.json" -Raw | ConvertFrom-Json
    $dotnet = "$cache\dotnet"; $git = "$cache\git"; $bin = "$cache\bin"; $dl = "$cache\downloads"
    New-Item -ItemType Directory -Force $bin, $dl, "$cache\claude", "$cache\nuget" | Out-Null

    # .NET SDK (includes the WPF / Windows Desktop runtime)
    if ($hostInfo.Update -or -not (Test-Path "$dotnet\dotnet.exe")) {
        Write-Host "Installing .NET 10 SDK..."
        Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile "$dl\dotnet-install.ps1" -UseBasicParsing
        & "$dl\dotnet-install.ps1" -Channel 10.0 -InstallDir $dotnet -NoPath
    }

    # Git for Windows (portable; Claude Code needs its bash)
    if ($hostInfo.Update -or -not (Test-Path "$git\bin\bash.exe")) {
        Write-Host "Installing Git for Windows (portable)..."
        $rel = Invoke-RestMethod https://api.github.com/repos/git-for-windows/git/releases/latest
        $asset = $rel.assets | Where-Object { $_.name -like 'PortableGit-*-64-bit.7z.exe' } | Select-Object -First 1
        Invoke-WebRequest $asset.browser_download_url -OutFile "$dl\PortableGit.exe" -UseBasicParsing
        if (Test-Path $git) { Remove-Item -Recurse -Force $git }
        Start-Process "$dl\PortableGit.exe" -ArgumentList "-o`"$git`"", '-y' -Wait
    }

    # Claude Code (native build; installed into the throwaway profile, then the exe is kept in the cache)
    if ($hostInfo.Update -or -not (Test-Path "$bin\claude.exe")) {
        Write-Host "Installing Claude Code..."
        Invoke-WebRequest https://claude.ai/install.ps1 -OutFile "$dl\claude-install.ps1" -UseBasicParsing
        & "$dl\claude-install.ps1"
        Copy-Item "$env:USERPROFILE\.local\bin\claude.exe" "$bin\claude.exe" -Force
    }

    # Python (same minor version as the host) installed into the cache; packages from sandbox\requirements.txt,
    # re-run when that file changes.
    $python = "$cache\python"
    if ($hostInfo.Update -or -not (Test-Path "$python\python.exe")) {
        Write-Host "Installing Python 3.12..."
        Invoke-WebRequest https://www.python.org/ftp/python/3.12.10/python-3.12.10-amd64.exe -OutFile "$dl\python.exe" -UseBasicParsing
        Start-Process "$dl\python.exe" -Wait -ArgumentList '/quiet', 'InstallAllUsers=0', "TargetDir=$python",
            'Include_launcher=0', 'Include_test=0', 'PrependPath=0', 'Shortcuts=0', 'AssociateFiles=0'
        Remove-Item "$python\.requirements.stamp" -ErrorAction SilentlyContinue
    }
    # Visual C++ runtime (PyMuPDF etc. need it; the sandbox image has none). vc_redist installs into the sandbox's
    # System32, which is wiped, and takes ~4 min - so it runs once and its DLLs are kept next to python.exe, where
    # Windows finds them for extension modules.
    $vcDlls = 'msvcp140.dll', 'msvcp140_1.dll', 'msvcp140_2.dll', 'msvcp140_atomic_wait.dll', 'msvcp140_codecvt_ids.dll',
              'concrt140.dll', 'vcomp140.dll'
    if ($hostInfo.Update -or -not (Test-Path "$python\msvcp140.dll")) {
        Write-Host "Installing Visual C++ runtime (one-time, several minutes)..."
        Invoke-WebRequest https://aka.ms/vs/17/release/vc_redist.x64.exe -OutFile "$dl\vc_redist.x64.exe" -UseBasicParsing
        Start-Process "$dl\vc_redist.x64.exe" -ArgumentList '/install', '/quiet', '/norestart' -Wait
        foreach ($d in $vcDlls) { if (Test-Path "$env:windir\System32\$d") { Copy-Item "$env:windir\System32\$d" $python -Force } }
    }
    $req = Join-Path $hostInfo.Repo 'sandbox\requirements.txt'
    $stamp = (Get-FileHash $req).Hash
    if ((Get-Content "$python\.requirements.stamp" -ErrorAction SilentlyContinue) -ne $stamp) {
        Write-Host "Installing Python packages..."
        & "$python\python.exe" -m pip install --upgrade --disable-pip-version-check -r $req
        if ($LASTEXITCODE -eq 0) { Set-Content "$python\.requirements.stamp" $stamp }
    }
    & "$python\python.exe" -c "import numpy, pymupdf, PIL, rasterio, ifcopenshell, pytest; print('python packages OK')"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Python imports failed - installing the Visual C++ runtime into this session..." -ForegroundColor Yellow
        Start-Process "$dl\vc_redist.x64.exe" -ArgumentList '/install', '/quiet', '/norestart' -Wait
    }

    # Text editor: the sandbox image has no Notepad (Store app). Portable Notepad++ plus a 'notepad' command for it.
    # PDFs open in Edge (built in).
    if ($hostInfo.Update -or -not (Test-Path "$cache\npp\notepad++.exe")) {
        Write-Host "Installing Notepad++ (portable)..."
        $rel = Invoke-RestMethod https://api.github.com/repos/notepad-plus-plus/notepad-plus-plus/releases/latest
        $asset = $rel.assets | Where-Object { $_.name -like 'npp.*.portable.x64.zip' } | Select-Object -First 1
        Invoke-WebRequest $asset.browser_download_url -OutFile "$dl\npp.zip" -UseBasicParsing
        if (Test-Path "$cache\npp") { Remove-Item -Recurse -Force "$cache\npp" }
        Expand-Archive "$dl\npp.zip" "$cache\npp"
    }
    Set-Content "$bin\notepad.cmd" "@start `"`" `"$cache\npp\notepad++.exe`" %*" -Encoding ascii

    # Environment for this session (user scope so every new window gets it).
    $vars = @{
        CLAUDE_CONFIG_DIR           = "$cache\claude"     # login, settings, history persist on the host
        CLAUDE_CODE_GIT_BASH_PATH   = "$git\bin\bash.exe"
        DISABLE_AUTOUPDATER         = '1'                 # update with start.ps1 -Update
        NUGET_PACKAGES              = "$cache\nuget"
        DOTNET_ROOT                 = $dotnet
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO               = '1'
    }
    foreach ($k in $vars.Keys) {
        [Environment]::SetEnvironmentVariable($k, $vars[$k], 'User')
        Set-Item "env:$k" $vars[$k]
    }
    $path = "$bin;$dotnet;$python;$python\Scripts;$git\cmd;$git\bin;" + [Environment]::GetEnvironmentVariable('Path', 'User')
    [Environment]::SetEnvironmentVariable('Path', $path, 'User')
    $env:Path = "$bin;$dotnet;$python;$python\Scripts;$git\cmd;$git\bin;$env:Path"

    # Git: trust the mapped repo (owned by the host account) and use the host identity.
    git config --global --add safe.directory '*'
    git config --global core.autocrlf true
    if ($hostInfo.GitName)  { git config --global user.name  $hostInfo.GitName }
    if ($hostInfo.GitEmail) { git config --global user.email $hostInfo.GitEmail }
    # GitHub login: Git Credential Manager without the helper-selector pop-up; the token is kept in the cache (plain
    # text, host folder) because the sandbox's own Windows credential store is wiped on close. Sign in once.
    git config --global credential.helper '""'              # empty entry drops PortableGit's helper-selector
    git config --global --add credential.helper manager
    git config --global credential.credentialStore plaintext
    git config --global credential.plaintextStorePath "$cache\git-credentials"

    # Claude's project memory -> the host's memory folder (mapped at C:\sbmmemory).
    $proj = "$cache\claude\projects\$($hostInfo.Key)"
    New-Item -ItemType Directory -Force $proj | Out-Null
    if (Test-Path "$proj\memory") { cmd /c rmdir "$proj\memory" | Out-Null }
    cmd /c mklink /J "$proj\memory" C:\sbmmemory | Out-Null

    # Claude desktop app (GUI). MSIX kept in the cache; installing it takes a few seconds per session. Its data folder
    # stays inside the sandbox (redirecting it to the mapped cache made the app fail to launch), so the desktop app's
    # sign-in is per session; the CLI login and Claude Code session history live in the cache and persist.
    $desktopApp = $null
    try {
        if ($hostInfo.Update -or -not (Test-Path "$dl\Claude.msix")) {
            Write-Host "Downloading Claude desktop..."
            Invoke-WebRequest https://claude.ai/api/desktop/win32/x64/msix/latest/redirect -OutFile "$dl\Claude.msix" -UseBasicParsing
        }
        Add-AppxPackage -Path "$dl\Claude.msix"
        $pkg = Get-AppxPackage -Name Claude
        # Allow (not default) bypass permissions mode for the signed-in account(s). The MSIX app's AppData is
        # virtualized into its package folder inside the sandbox.
        $dataDir = "$env:LOCALAPPDATA\Packages\$($pkg.PackageFamilyName)\LocalCache\Roaming\Claude"
        New-Item -ItemType Directory -Force $dataDir | Out-Null
        $cfgPath = "$dataDir\claude_desktop_config.json"
        $cfg = if (Test-Path $cfgPath) { Get-Content $cfgPath -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
        if (-not $cfg.preferences) { $cfg | Add-Member preferences ([pscustomobject]@{}) -Force }
        foreach ($key in 'bypassPermissionsOptInByAccount', 'bypassPermissionsGateByAccount') {
            if (-not $cfg.preferences.$key) { $cfg.preferences | Add-Member $key ([pscustomobject]@{}) -Force }
            foreach ($acct in @($hostInfo.BypassAccounts)) { if ($acct) { $cfg.preferences.$key | Add-Member $acct $true -Force } }
        }
        [IO.File]::WriteAllText($cfgPath, ($cfg | ConvertTo-Json -Depth 100), (New-Object Text.UTF8Encoding $false))
        $appId = (Get-AppxPackageManifest $pkg).Package.Applications.Application | Select-Object -First 1 -ExpandProperty Id
        $desktopApp = "shell:AppsFolder\$($pkg.PackageFamilyName)!$appId"
    }
    catch { Write-Host "Claude desktop install failed (CLI still available): $_" -ForegroundColor Yellow }

    # Desktop shortcuts for convenience.
    $ws = New-Object -ComObject WScript.Shell
    $lnk = $ws.CreateShortcut("$env:USERPROFILE\Desktop\Claude CLI (no prompts).lnk")
    $lnk.TargetPath = 'powershell.exe'
    $lnk.Arguments = '-NoExit -Command claude --allow-dangerously-skip-permissions --remote-control ''SBM sandbox'''
    $lnk.WorkingDirectory = $hostInfo.Repo
    $lnk.Save()
    $lnk = $ws.CreateShortcut("$env:USERPROFILE\Desktop\Repo shell.lnk")
    $lnk.TargetPath = 'powershell.exe'; $lnk.Arguments = '-NoExit'; $lnk.WorkingDirectory = $hostInfo.Repo
    $lnk.Save()

    Write-Host "Setup done."
    Stop-Transcript | Out-Null
    if ($desktopApp) { Start-Process $desktopApp }
    else { Start-Process powershell.exe -WorkingDirectory $hostInfo.Repo -ArgumentList '-NoExit', '-Command', 'claude --allow-dangerously-skip-permissions --remote-control ''SBM sandbox''' }
}
catch {
    Write-Host "SETUP FAILED: $_" -ForegroundColor Red
    Stop-Transcript | Out-Null
    Read-Host "Press Enter to close"
}

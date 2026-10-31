# Windows Sandbox for Claude Code

A throwaway Windows desktop where bypass permissions (no prompts) is ALLOWED but not the default - pick it per session (CLI: Shift+Tab; desktop: mode menu) and can
build, test AND run the WPF app (`--snapshot`, `sbm app ...`). Inside, Claude can only reach:
- this repo (read-write, at `C:\SBM`),
- `C:\SBMData` (read-only),
- `%USERPROFILE%\SBMSandbox` on the host (= `C:\sbmcache`: .NET SDK, Git, Python 3.12 + `sandbox/requirements.txt` packages,
  Notepad++, Claude CLI + desktop MSIX, NuGet cache, logins),
- Claude's project memory folder for this repo (shared with normal Windows sessions).

Everything else in the sandbox is discarded when you close its window.

## One-time setup
1. Admin PowerShell: `Enable-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM -All`, reboot.
2. `.\sandbox\start.ps1` - the first start downloads the .NET SDK, portable Git and Claude Code (several minutes).
3. Log in to Claude in the window that opens (the login is kept for later sessions).

## Use
- `.\sandbox\start.ps1` - opens the sandbox and the Claude desktop app (installed from the cached MSIX each time, ~10 s;
  sign in to it each session - its data cannot live in a mapped folder). Open C:\SBM in the Code tab; bypass permissions is pre-allowed.
- Desktop shortcuts inside: "Claude CLI (no prompts)" (starts with Remote Control on, name "SBM sandbox") and "Repo shell".
- Nothing important lives only in the sandbox: repo, tools, logins and Claude sessions are host folders, so a crash loses
  only what was in memory. The sandbox itself cannot be saved as an image (use a Hyper-V VM for that).
- `.\sandbox\start.ps1 -Update` - refresh .NET SDK, Git and Claude Code.
- Setup log: `%USERPROFILE%\SBMSandbox\setup.log`.

## Limits
- Only one Windows Sandbox can run at a time.
- Internet is all-or-nothing (no allow-list) - the repo is the thing to protect; commit before long unattended runs.
- The sandbox and the host share the repo's bin/obj: close the app on the host before building inside (file locks).

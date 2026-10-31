# Re-runs the Ramp G script chain on a model file (default: a test copy in temp\) and logs everything.
param([string]$Model = "temp\rampg-rebuild-test.sbm", [switch]$FromOriginal)
$ErrorActionPreference = "Continue"
Set-Location "C:\SBM"
if ($FromOriginal) { Copy-Item "samples\plans\I-80 WB and Ramp G.sbm" $Model -Force }
$cli = (Get-ChildItem src\SBM.Cli\bin\Debug -Recurse -Filter sbm.exe | Select-Object -First 1).FullName
$log = [IO.Path]::ChangeExtension($Model, ".log")
"Ramp G rebuild on $Model  $(Get-Date)" | Out-File $log -Encoding utf8
foreach ($s in "build-ramp-g", "field-splices-ramp-g", "piers-ramp-g", "bearings", "column-rebar", "cross-frames-ramp-g", "check-top-of-bearing", "check-spans-ramp-g") {
    "===== $s  $(Get-Date -Format T)" | Out-File $log -Append -Encoding utf8
    & $cli run "samples\plans\$s.csx" $Model 2>&1 | Out-File $log -Append -Encoding utf8
    "exit $LASTEXITCODE" | Out-File $log -Append -Encoding utf8
}
"done $(Get-Date)" | Out-File $log -Append -Encoding utf8

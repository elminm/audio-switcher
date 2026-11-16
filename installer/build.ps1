# Builds AudioSwitcher.exe and then the installer (dist\AudioSwitcher-Setup-<version>.exe).
# Requires Inno Setup 6: winget install JRSoftware.InnoSetup
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

& (Join-Path (Split-Path -Parent $here) 'app\build.ps1')

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup' }

& $iscc /Q (Join-Path $here 'AudioSwitcher.iss')
if ($LASTEXITCODE -ne 0) { throw 'installer build failed' }
Get-ChildItem (Join-Path (Split-Path -Parent $here) 'dist') -Filter *.exe | ForEach-Object { Write-Host "Built $($_.FullName)" }

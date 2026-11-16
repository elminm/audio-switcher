# Builds AudioSwitcher.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$out  = Join-Path (Split-Path -Parent $here) 'AudioSwitcher.exe'
$csc  = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$src  = @("$here\AudioSwitcher.cs", "$here\CoreAudio.cs", "$here\Bluetooth.cs", "$here\Strings.cs", "$here\HyperXProbe.cs")
$common = @('/nologo', '/target:winexe', '/optimize+', '/codepage:65001', '/platform:anycpu',
            '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Web.Extensions.dll')

$ico = Join-Path $here 'icon.ico'
if (-not (Test-Path $ico)) {
    $tmp = Join-Path $here 'iconmaker.exe'
    & $csc @common "/out:$tmp" @src
    if ($LASTEXITCODE -ne 0) { throw 'compile (icon pass) failed' }
    Start-Process -FilePath $tmp -ArgumentList '--make-icon', "`"$ico`"" -Wait
    Remove-Item $tmp
}

& $csc @common "/win32icon:$ico" "/out:$out" @src
if ($LASTEXITCODE -ne 0) { throw 'compile failed' }
Write-Host "Built $out"

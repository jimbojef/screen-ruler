# build.ps1: builds dist\ScreenRuler.exe with the C# compiler that ships with Windows
# (.NET Framework 4.8). Nothing needs to be installed.
# Usage:  powershell -ExecutionPolicy Bypass -File .\build.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc  = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$out  = Join-Path $root 'dist\ScreenRuler.exe'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null

if (Get-Process ScreenRuler -ErrorAction SilentlyContinue) {
    Write-Host 'ScreenRuler is still running. Please quit it first (tray icon > Beenden).' -ForegroundColor Yellow
    exit 1
}

$src = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
& $csc /nologo /target:winexe /optimize+ /platform:anycpu /utf8output `
    "/out:$out" "/win32manifest:$(Join-Path $root 'app.manifest')" `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll $src
if ($LASTEXITCODE -ne 0) { Write-Host 'Build failed.' -ForegroundColor Red; exit $LASTEXITCODE }
Write-Host "Done: $out" -ForegroundColor Green

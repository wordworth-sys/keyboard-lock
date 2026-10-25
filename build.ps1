# Builds dist\KeyboardLock.exe with the embedded icon, manifest and version info.
# Requires Windows + .NET Framework 4.x (csc.exe ships with Windows 10/11).
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw 'csc.exe not found. Install .NET Framework 4.x.'
}

$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

& $csc /nologo /target:winexe /platform:anycpu /codepage:65001 /optimize+ `
    "/win32icon:$root\assets\icon.ico" `
    "/win32manifest:$root\src\app.manifest" `
    "/out:$dist\KeyboardLock.exe" `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    "$root\src\KeyboardLock.cs" "$root\src\AssemblyInfo.cs"

if ($LASTEXITCODE -ne 0) {
    throw "csc failed with exit code $LASTEXITCODE"
}

Write-Host "Built: $dist\KeyboardLock.exe"

# Builds dist\KeyboardLock.exe — a single file with the embedded icon and manifest.
# The UI is drawn natively with GDI+, so there is no WebView2 runtime and no extra DLL to ship.
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

# 面板渲染模块放在 src\alu\ 下，其余源码在 src\ 根目录。
$sources = @(Get-ChildItem -Path (Join-Path $root 'src') -Filter '*.cs' -File | ForEach-Object { $_.FullName })
$sources += @(Get-ChildItem -Path (Join-Path $root 'src\alu') -Filter '*.cs' -File | ForEach-Object { $_.FullName })

& $csc /nologo /target:winexe /platform:x64 /codepage:65001 /optimize+ `
    "/win32icon:$root\assets\icon.ico" `
    "/win32manifest:$root\src\app.manifest" `
    "/out:$dist\KeyboardLock.exe" `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    /warn:4 `
    $sources

if ($LASTEXITCODE -ne 0) {
    throw "csc failed with exit code $LASTEXITCODE"
}

Write-Host "Built: $dist\KeyboardLock.exe"

# Builds dist\KeyboardLock.exe with the embedded icon, manifest, version info, and WebView2 runtime bindings.
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

$wvCore = Join-Path $root 'sdk\lib\Microsoft.Web.WebView2.Core.dll'
$wvWinForms = Join-Path $root 'sdk\lib\Microsoft.Web.WebView2.WinForms.dll'
$wvLoader = Join-Path $root 'sdk\lib\WebView2Loader.dll'
$uiHtml = Join-Path $root 'src\ui.html'

& $csc /nologo /target:winexe /platform:x64 /codepage:65001 /optimize+ `
    "/win32icon:$root\assets\icon.ico" `
    "/win32manifest:$root\src\app.manifest" `
    "/out:$dist\KeyboardLock.exe" `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    "/r:$wvCore" "/r:$wvWinForms" `
    "/resource:$uiHtml,KeyboardLock.ui.html" `
    "/resource:$wvCore,KeyboardLock.Microsoft.Web.WebView2.Core.dll" `
    "/resource:$wvWinForms,KeyboardLock.Microsoft.Web.WebView2.WinForms.dll" `
    "/resource:$wvLoader,KeyboardLock.WebView2Loader.dll" `
    /warn:4 `
    "$root\src\KeyFilter.cs" "$root\src\KeyboardHook.cs" "$root\src\KeyboardLock.cs" "$root\src\AssemblyInfo.cs"

if ($LASTEXITCODE -ne 0) {
    throw "csc failed with exit code $LASTEXITCODE"
}

# Clean up legacy loose files in dist (all resources are embedded into the single KeyboardLock.exe)
Remove-Item -Path "$dist\ui.html", "$dist\Microsoft.Web.WebView2.Core.dll", "$dist\Microsoft.Web.WebView2.WinForms.dll", "$dist\WebView2Loader.dll" -ErrorAction SilentlyContinue

Write-Host "Built: $dist\KeyboardLock.exe"

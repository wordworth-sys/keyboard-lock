# Compiles and runs the KeyFilter state-machine tests and AluPanelRenderer off-screen regression tests
# (no hooks are installed, no windows are opened, nothing touches the real keyboard).
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$filterOut = Join-Path $env:TEMP 'KeyboardLock.KeyFilterTests.exe'
& $csc /nologo /target:exe /codepage:65001 /warn:4 /warnaserror+ "/out:$filterOut" `
    "$root\src\KeyFilter.cs" "$root\tests\KeyFilterTests.cs"
if ($LASTEXITCODE -ne 0) { throw "csc (KeyFilterTests) failed with exit code $LASTEXITCODE" }

$aluSources = @(
    "$root\src\AluPanelRenderer.cs"
    "$root\tests\AluRenderTests.cs"
)
$aluSources += @(Get-ChildItem -Path (Join-Path $root 'src\alu') -Filter '*.cs' -File |
    Sort-Object Name | ForEach-Object { $_.FullName })

$renderOut = Join-Path $env:TEMP 'KeyboardLock.AluRenderTests.exe'
& $csc /nologo /target:exe /codepage:65001 /warn:4 /warnaserror+ "/out:$renderOut" `
    /r:System.dll /r:System.Drawing.dll `
    $aluSources
if ($LASTEXITCODE -ne 0) { throw "csc (AluRenderTests) failed with exit code $LASTEXITCODE" }

try {
    Write-Host "[1/2] KeyFilter state-machine tests"
    & $filterOut
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host ""
    Write-Host "[2/2] AluPanelRenderer off-screen rendering tests"
    & $renderOut
    exit $LASTEXITCODE
}
finally {
    Remove-Item $filterOut, $renderOut -ErrorAction SilentlyContinue
}

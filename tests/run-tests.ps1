# Compiles and runs the KeyFilter state-machine tests (no hooks are installed, nothing touches the real keyboard).
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}

$out = Join-Path $env:TEMP 'KeyboardLock.Tests.exe'
& $csc /nologo /target:exe /codepage:65001 /warn:4 "/out:$out" `
    "$root\src\KeyFilter.cs" "$root\tests\KeyFilterTests.cs"
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
& $out
$code = $LASTEXITCODE
Remove-Item $out -ErrorAction SilentlyContinue
exit $code

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$WrapperPath = Join-Path $RepositoryRoot "scripts\build-platform.ps1"
$Source = Get-Content -LiteralPath $WrapperPath -Raw

foreach ($RequiredToken in @(
        "System.Diagnostics.Process",
        "BeginOutputReadLine",
        "BeginErrorReadLine",
        "Register-ObjectEvent",
        "WaitForExit()",
        "menu.generate-game-scenes",
        "menu.attach-tilt-trial-presentation-blueprints",
        "--editor-command"
    )) {
    if (-not $Source.Contains($RequiredToken)) {
        throw "The build wrapper is missing streaming-process token '$RequiredToken'."
    }
}

if ($Source.Contains('$Platform -ieq "ps2" -or $Platform -ieq "windows"')) {
    throw "The build wrapper must regenerate shared game scenes for every target platform, not a PS2/Windows subset."
}

foreach ($ForbiddenToken in @(
        "Start-Sleep",
        "WaitForExit(100)",
        "ReadToEndAsync",
        "Task.WhenAny"
    )) {
    if ($Source.Contains($ForbiddenToken)) {
        throw "The build wrapper must not use timer-based or buffered process handling token '$ForbiddenToken'."
    }
}

$InvocationCount = ([System.Text.RegularExpressions.Regex]::Matches($Source, "Invoke-StreamingNativeProcess")).Count
if ($InvocationCount -ne 6) {
    throw "Expected one streaming runner declaration and five native invocations, found $InvocationCount."
}

Write-Output "STREAMING_TEST_PASS"

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
        "Copy-ProjectIntoIsolatedWorkspace",
        '[Guid]::NewGuid().ToString("N")',
        '$IsolatedProjectPath',
        'ps2-build*',
        'vita-build*',
        '"*.iso"',
        "--build-profile"
    )) {
    if (-not $Source.Contains($RequiredToken)) {
        throw "The build wrapper is missing streaming-process token '$RequiredToken'."
    }
}

foreach ($ForbiddenProjectCommandId in @(
        "menu.generate-game-scenes",
        "menu.regenerate-demo-disc-main-menu",
        "menu.attach-tilt-trial-presentation-blueprints",
        "--editor-command"
    )) {
    if ($Source.Contains($ForbiddenProjectCommandId)) {
        throw "The generic build wrapper must not hard-code project editor command '$ForbiddenProjectCommandId'."
    }
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
if ($InvocationCount -ne 4) {
    throw "Expected one streaming runner declaration and three native invocations, found $InvocationCount."
}

Write-Output "STREAMING_TEST_PASS"

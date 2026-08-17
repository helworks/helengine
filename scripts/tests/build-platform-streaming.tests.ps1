[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$WrapperPath = Join-Path $RepositoryRoot "scripts\build-platform.ps1"
$ProcessModulePath = Join-Path $RepositoryRoot "scripts\build-platform\BuildPlatformProcess.psm1"

if (-not (Test-Path -LiteralPath $ProcessModulePath -PathType Leaf)) {
    throw "The testable streaming helper module was not found at '$ProcessModulePath'."
}

Import-Module $ProcessModulePath -Force
$WrapperSource = Get-Content -LiteralPath $WrapperPath -Raw
$ProcessSource = Get-Content -LiteralPath $ProcessModulePath -Raw

foreach ($RequiredProcessToken in @(
        "System.Diagnostics.Process",
        "BeginOutputReadLine",
        "BeginErrorReadLine",
        "Register-ObjectEvent",
        "WaitForExit()"
    )) {
    if (-not $ProcessSource.Contains($RequiredProcessToken)) {
        throw "The streaming helper is missing process token '$RequiredProcessToken'."
    }
}

if (-not $WrapperSource.Contains("--build-profile")) {
    throw "The build wrapper is missing --build-profile forwarding."
}

foreach ($ForbiddenProjectCommandId in @(
        "menu.generate-game-scenes",
        "menu.regenerate-demo-disc-main-menu",
        "menu.attach-tilt-trial-presentation-blueprints",
        "--editor-command"
    )) {
    if ($WrapperSource.Contains($ForbiddenProjectCommandId)) {
        throw "The generic build wrapper must not hard-code project editor command '$ForbiddenProjectCommandId'."
    }
}

foreach ($ForbiddenToken in @(
        "Start-Sleep",
        "WaitForExit(100)",
        "ReadToEndAsync",
        "Task.WhenAny"
    )) {
    if ($ProcessSource.Contains($ForbiddenToken)) {
        throw "The streaming helper must not use timer-based or buffered process handling token '$ForbiddenToken'."
    }
}

$WrapperInvocationCount = ([System.Text.RegularExpressions.Regex]::Matches($WrapperSource, "Invoke-StreamingNativeProcess")).Count
if ($WrapperInvocationCount -ne 3) {
    throw "Expected three native process invocations in the wrapper, found $WrapperInvocationCount."
}

$TestRootPath = Join-Path ([System.IO.Path]::GetTempPath()) ("build-platform-streaming-" + [Guid]::NewGuid().ToString("N"))
$FakeChildPath = Join-Path $TestRootPath "fake-streaming-child.cmd"

try {
    $null = New-Item -ItemType Directory -Path $TestRootPath -Force
    Set-Content -LiteralPath $FakeChildPath -Value @'
@echo off
echo STREAM_STDOUT_MARKER:%~1
echo STREAM_STDERR_MARKER 1>&2
exit /b 23
'@ -NoNewline

    $OriginalConsoleOut = [Console]::Out
    $OriginalConsoleError = [Console]::Error
    $CapturedConsoleOut = New-Object System.IO.StringWriter
    $CapturedConsoleError = New-Object System.IO.StringWriter
    try {
        [Console]::SetOut($CapturedConsoleOut)
        [Console]::SetError($CapturedConsoleError)
        $ExitCode = Invoke-StreamingNativeProcess -FilePath $FakeChildPath -ArgumentList @("argument with spaces")
    } finally {
        [Console]::SetOut($OriginalConsoleOut)
        [Console]::SetError($OriginalConsoleError)
    }

    $CapturedStandardOutput = $CapturedConsoleOut.ToString()
    $CapturedStandardError = $CapturedConsoleError.ToString()
    if ($CapturedStandardOutput -notmatch 'STREAM_STDOUT_MARKER:argument with spaces') {
        throw "The streaming helper did not forward stdout. Captured: '$CapturedStandardOutput'."
    }
    if ($CapturedStandardError -notmatch 'STREAM_STDERR_MARKER') {
        throw "The streaming helper did not forward stderr. Captured: '$CapturedStandardError'."
    }
    if ($ExitCode -ne 23) {
        throw "The streaming helper returned exit code $ExitCode instead of 23."
    }

    Write-Output "STREAMING_TEST_PASS"
} finally {
    if (Test-Path -LiteralPath $TestRootPath) {
        Remove-Item -LiteralPath $TestRootPath -Recurse -Force
    }
}

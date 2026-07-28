[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$WrapperPath = Join-Path $RepositoryRoot "scripts\build-platform.ps1"
$Source = Get-Content -LiteralPath $WrapperPath -Raw

foreach ($RequiredToken in @(
        '[string]$BuildProfile = ""',
        '$ResolvedBuildProfile',
        '"--build-profile",',
        '$ResolvedBuildProfile'
    )) {
    if (-not $Source.Contains($RequiredToken)) {
        throw "The build wrapper is missing independent build-profile token '$RequiredToken'."
    }
}

$DirectConfigurationBindingPattern = '(?s)"--build-profile"\s*,\s*\$Configuration(?![A-Za-z0-9_])(?:\s*(?:\.[A-Za-z_][A-Za-z0-9_]*(?:\s*\([^)]*\))?|\[[^\]]+\]|\([^)]*\)))*'

if ($Source -match $DirectConfigurationBindingPattern) {
    throw "The build wrapper must not bind --build-profile directly to Configuration."
}

$ControlledDirectConfigurationBindingSources = @(
    ($Source -replace '(?s)("--build-profile"\s*,\s*)\$ResolvedBuildProfile', '${1}$Configuration'),
    ($Source -replace '(?s)("--build-profile"\s*,\s*)\$ResolvedBuildProfile', '${1}$Configuration.Trim()')
)
foreach ($ControlledDirectConfigurationBindingSource in $ControlledDirectConfigurationBindingSources) {
    if ($ControlledDirectConfigurationBindingSource -notmatch $DirectConfigurationBindingPattern) {
        throw "The profile test must reject direct Configuration bindings at the editor invocation."
    }
}

if ($Source -notmatch '(?s)"--build-profile"\s*,\s*\$ResolvedBuildProfile') {
    throw "The build wrapper must pass ResolvedBuildProfile to the editor CLI."
}

Write-Output "PROFILE_TEST_PASS"

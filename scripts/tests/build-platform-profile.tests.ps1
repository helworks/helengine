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

if ($Source -match '(?s)"--build-profile"\s*,\s*\$Configuration\.ToLowerInvariant\(\)') {
    throw "The build wrapper must not bind --build-profile to Configuration.ToLowerInvariant()."
}

if ($Source -notmatch '(?s)"--build-profile"\s*,\s*\$ResolvedBuildProfile') {
    throw "The build wrapper must pass ResolvedBuildProfile to the editor CLI."
}

Write-Output "PROFILE_TEST_PASS"

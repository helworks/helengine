[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$WrapperPath = Join-Path $RepositoryRoot "scripts\build-platform.ps1"
$Source = Get-Content -LiteralPath $WrapperPath -Raw

foreach ($RequiredToken in @(
        '[string]$WorkspaceRoot = ""',
        '$ResolvedWorkspaceRootPath',
        '"builds\helengine-builds"'
    )) {
    if (-not $Source.Contains($RequiredToken)) {
        throw "The build wrapper is missing workspace-root token '$RequiredToken'."
    }
}

if ($Source.Contains('[System.IO.Path]::GetTempPath()) ("helengine-builds')) {
    throw "The build wrapper must not create its isolated build workspace under the system temporary directory."
}

Write-Output "WORKSPACE_TEST_PASS"

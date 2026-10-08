param([Parameter(Mandatory=$true)][ValidateSet('dc','ps1','n64','ps3','x360','xbox','windows')][string]$Platform,[Parameter(Mandatory=$true)][string]$StagedProjectRoot,[switch]$ValidateOnly)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'prepare-staged-menu.ps1') -Platform $Platform -StagedProjectRoot $StagedProjectRoot -ValidateOnly:$ValidateOnly

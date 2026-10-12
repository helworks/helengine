param([Parameter(Mandatory=$true)][ValidateSet('dc','ps1','n64','ps2','ps3','psp','psvita','x360','xbox','gamecube','wii','wiiu','ds','3ds','switch','windows')][string]$Platform,[Parameter(Mandatory=$true)][string]$StagedProjectRoot,[switch]$ValidateOnly)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'prepare-staged-menu.ps1') -Platform $Platform -StagedProjectRoot $StagedProjectRoot -ValidateOnly:$ValidateOnly

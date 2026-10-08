[CmdletBinding()]
param(
    [Parameter()]
    [ValidateScript({ [IO.Path]::IsPathRooted($_) })]
    [string]$WorkspaceRoot = 'C:\dev\helworks'
)
$ErrorActionPreference = 'Stop'
$ResolvedWorkspace = [IO.Path]::GetFullPath($WorkspaceRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
if ($ResolvedWorkspace -ne 'C:\dev\helworks') {
    throw 'These reviewed staging guards target the existing C:\dev\helworks coordinated menu build paths.'
}
$TitleTarget = Join-Path $ResolvedWorkspace 'builds\builder-coordination\menu-20261007\menu-title-fit'
$AvailabilityTarget = Join-Path $ResolvedWorkspace 'builds\helengine-n64\menu-20261007\menu-availability-hidden'
foreach ($Mapping in @(
    @{Source=(Join-Path $PSScriptRoot 'title-layout');Target=$TitleTarget},
    @{Source=(Join-Path $PSScriptRoot 'availability');Target=$AvailabilityTarget}
)) {
    $null = New-Item -ItemType Directory -Path $Mapping.Target -Force
    foreach ($File in Get-ChildItem -LiteralPath $Mapping.Source -File) {
        $Target = Join-Path $Mapping.Target $File.Name
        Copy-Item -LiteralPath $File.FullName -Destination $Target -Force
        if ((Get-FileHash -LiteralPath $File.FullName).Hash -ne (Get-FileHash -LiteralPath $Target).Hash) {
            throw "Reviewed menu tool copy differs: $($File.Name)"
        }
    }
}
Write-Output 'Restored the reviewed menu source tools to the existing fixed staging paths.'

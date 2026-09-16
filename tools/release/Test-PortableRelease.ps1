# Checks upload limits, extraction layout, and exact recovery of the original Docker payload.
param(
    [Parameter(Mandatory=$true)][string]$ReleaseRoot,
    [Parameter(Mandatory=$true)][string]$OriginalTar,
    [Parameter(Mandatory=$true)][string]$ExtractionRoot
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($Name in @('helengine-editor-windows-x64.zip','helengine-toolchains-01.zip','helengine-toolchains-02.zip')) {
    $Zip = Join-Path $ReleaseRoot $Name
    if (-not (Test-Path $Zip)) { throw "FAIL: missing release archive $Name" }
    if ((Get-Item $Zip).Length -ge 2000000000) { throw "FAIL: $Name exceeds upload size limit." }
    [IO.Compression.ZipFile]::ExtractToDirectory($Zip, $ExtractionRoot)
    Write-Host "Extracted: $Name"
}
$Root = Join-Path $ExtractionRoot 'Helengine'
& (Join-Path $Root 'Join-Toolchains.ps1')
if ((Get-FileHash (Join-Path $Root 'toolchains\console-sdk-images.tar')).Hash -ne (Get-FileHash $OriginalTar).Hash) { throw 'FAIL: original payload not recovered exactly.' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $Root 'Start-Helengine.ps1') -Check
if ($LASTEXITCODE -ne 0) { throw 'FAIL: extracted editor payload check failed.' }
Write-Host 'PASS: all ZIPs under 2 GB, merged extraction, exact Docker payload, editor payload paths/SDK.'

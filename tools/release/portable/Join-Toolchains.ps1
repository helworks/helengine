# Reassembles the release payload and verifies it before Docker consumes it.
param([string]$ToolchainRoot = (Join-Path $PSScriptRoot 'toolchains'))
$ErrorActionPreference = 'Stop'
$ToolchainRoot = [IO.Path]::GetFullPath($ToolchainRoot)
$Target = Join-Path $ToolchainRoot 'console-sdk-images.tar'
$ManifestPath = Join-Path $ToolchainRoot 'console-sdk-images.json'
if (-not (Test-Path -LiteralPath $ManifestPath)) {
    if (Test-Path -LiteralPath $Target -PathType Leaf) { return }
    throw 'Missing toolchain payload. Extract both numbered toolchain ZIPs alongside the editor ZIP.'
}
$Manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
if ($Manifest.sha256 -notmatch '^[a-fA-F0-9]{64}$' -or @($Manifest.parts).Count -eq 0) { throw 'Invalid toolchain manifest.' }
if (Test-Path -LiteralPath $Target) {
    if ((Get-FileHash -LiteralPath $Target -Algorithm SHA256).Hash -ne $Manifest.sha256) { throw 'Toolchain archive checksum mismatch. Move the damaged console-sdk-images.tar aside and retry.' }
    return
}
foreach ($Part in $Manifest.parts) {
    if ($Part -notmatch '^console-sdk-images\.tar\.part[0-9]+$') { throw 'Invalid toolchain part name.' }
    if (-not (Test-Path -LiteralPath (Join-Path $ToolchainRoot $Part) -PathType Leaf)) { throw "Missing toolchain part: $Part. Extract all numbered toolchain ZIPs into the same destination." }
}
$Partial = Join-Path $ToolchainRoot ('console-sdk-images.' + [Guid]::NewGuid().ToString('N') + '.partial')
try {
    Write-Host 'Reassembling console SDK images (requires about 2.8 GB additional disk space)...'
    $Destination = [IO.File]::Open($Partial, [IO.FileMode]::CreateNew)
    try {
        foreach ($Part in $Manifest.parts) {
            $Source = [IO.File]::OpenRead((Join-Path $ToolchainRoot $Part))
            try { $Source.CopyTo($Destination) } finally { $Source.Dispose() }
        }
    } finally { $Destination.Dispose() }
    if ((Get-FileHash -LiteralPath $Partial -Algorithm SHA256).Hash -ne $Manifest.sha256) { throw 'Toolchain parts checksum mismatch. Download and extract both toolchain ZIPs again.' }
    Move-Item -LiteralPath $Partial -Destination $Target
} finally {
    # Only remove this invocation's uniquely named, incomplete output.
    if (Test-Path -LiteralPath $Partial) { Remove-Item -LiteralPath $Partial }
}

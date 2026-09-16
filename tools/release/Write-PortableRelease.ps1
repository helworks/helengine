# Creates ordinary, mergeable ZIPs below the conservative decimal 2 GB upload limit.
param(
    [Parameter(Mandatory=$true)][string]$PackageRoot,
    [Parameter(Mandatory=$true)][string]$ReleaseRoot
)
$ErrorActionPreference = 'Stop'
$PackageRoot = [IO.Path]::GetFullPath($PackageRoot).TrimEnd('\')
$ReleaseRoot = [IO.Path]::GetFullPath($ReleaseRoot).TrimEnd('\')
if ($ReleaseRoot -eq $PackageRoot -or $ReleaseRoot.StartsWith($PackageRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Release folder must be outside package.' }
if (Test-Path -LiteralPath $ReleaseRoot) { throw 'Use a new release folder; existing releases are never overwritten.' }
$TarPath = Join-Path $PackageRoot 'toolchains\console-sdk-images.tar'
$Tar = Get-Item -LiteralPath $TarPath
$ChunkSize = 1400000000L
if ($Tar.Length -le $ChunkSize -or $Tar.Length -gt (2 * $ChunkSize)) { throw 'This three-ZIP layout requires a Docker payload between 1.4 and 2.8 GB. Adjust the layout and README for other sizes.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$null = New-Item -ItemType Directory -Path $ReleaseRoot
foreach ($File in @('Start-Helengine.ps1', 'Join-Toolchains.ps1', 'README.txt')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "portable\$File") -Destination $PackageRoot -Force
}
$Parts = @('console-sdk-images.tar.part01', 'console-sdk-images.tar.part02')
@{ sha256=(Get-FileHash -LiteralPath $TarPath -Algorithm SHA256).Hash; parts=$Parts } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PackageRoot 'toolchains\console-sdk-images.json') -Encoding ASCII
$Source = [IO.File]::OpenRead($TarPath)
try {
    $Buffer = New-Object byte[] (4MB)
    for ($Index = 0; $Index -lt $Parts.Count; $Index++) {
        $Name = 'helengine-toolchains-{0:00}.zip' -f ($Index + 1)
        $Archive = [IO.Compression.ZipFile]::Open((Join-Path $ReleaseRoot $Name), [IO.Compression.ZipArchiveMode]::Create)
        try {
            $Entry = $Archive.CreateEntry("Helengine/toolchains/$($Parts[$Index])", [IO.Compression.CompressionLevel]::NoCompression)
            $Destination = $Entry.Open()
            try {
                $Remaining = [Math]::Min($ChunkSize, $Source.Length - $Source.Position)
                while ($Remaining -gt 0) {
                    $Read = $Source.Read($Buffer, 0, [int][Math]::Min($Buffer.Length, $Remaining))
                    if ($Read -eq 0) { throw 'Unexpected end of Docker payload.' }
                    $Destination.Write($Buffer, 0, $Read)
                    $Remaining -= $Read
                }
            } finally { $Destination.Dispose() }
        } finally { $Archive.Dispose() }
        Write-Host "Created $Name"
    }
} finally { $Source.Dispose() }
& (Join-Path $PSScriptRoot 'Write-PortableZip.ps1') -PackageRoot $PackageRoot -ZipPath (Join-Path $ReleaseRoot 'helengine-editor-windows-x64.zip') -WithoutToolchainTar
$Checksums = foreach ($File in Get-ChildItem -LiteralPath $ReleaseRoot -Filter '*.zip') {
    if ($File.Length -ge 2000000000) { throw "Archive exceeds upload limit: $($File.Name)" }
    '{0}  {1}' -f (Get-FileHash -LiteralPath $File.FullName -Algorithm SHA256).Hash, $File.Name
}
$Checksums | Set-Content -LiteralPath (Join-Path $ReleaseRoot 'SHA256SUMS.txt') -Encoding ASCII
Copy-Item -LiteralPath (Join-Path $PackageRoot 'README.txt') -Destination $ReleaseRoot
Get-ChildItem -LiteralPath $ReleaseRoot -Filter '*.zip' | Select-Object Name, Length

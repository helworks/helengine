param(
    [Parameter(Mandatory = $true)][string]$PackageRoot,
    [Parameter(Mandatory = $true)][string]$ZipPath,
    [switch]$WithoutToolchainTar
)
$ErrorActionPreference = 'Stop'
$PackageRoot = [IO.Path]::GetFullPath($PackageRoot).TrimEnd('\')
$ZipPath = [IO.Path]::GetFullPath($ZipPath)
if (Test-Path -LiteralPath $ZipPath) { throw "Archive already exists: $ZipPath" }
if ($ZipPath.StartsWith($PackageRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Archive must be outside its source folder.' }
foreach ($RequiredFile in @('README.txt', 'Start-Helengine.cmd', 'Setup-Toolchains.cmd', 'editor\helengine.editor.app.dll', 'tools\dotnet\dotnet.exe', 'editor\codegen\codegen.exe', 'toolchains\console-sdk-images.tar', 'user_settings\platforms.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $PackageRoot $RequiredFile) -PathType Leaf)) { throw "Package is incomplete: $RequiredFile" }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$Archive = [IO.Compression.ZipFile]::Open($ZipPath, [IO.Compression.ZipArchiveMode]::Create)
try {
    # Include the empty generated-core directory required by platform discovery.
    $null = $Archive.CreateEntry('Helengine/generated-core/')
    $Count = 0
    foreach ($File in Get-ChildItem -LiteralPath $PackageRoot -Recurse -File -Force) {
        $Relative = $File.FullName.Substring($PackageRoot.Length + 1).Replace('\', '/')
        if ($WithoutToolchainTar -and $Relative -eq 'toolchains/console-sdk-images.tar') { continue }
        if ($Relative -match '^(cache/|user_settings/dotnet/)' -or
            $Relative -match '^sources/.*?/(obj|bin)/' -or
            ($Relative -match '^platforms/[^/]+/player/(.*/)?(obj|bin)/' -and
             $Relative -notmatch '^platforms/[^/]+/player/builder/bin/Release/')) { continue }
        $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($Archive, $File.FullName, "Helengine/$Relative", [IO.Compression.CompressionLevel]::Optimal)
        $Count++
    }
    Write-Host "Archived $Count files."
} finally {
    $Archive.Dispose()
}
Get-Item -LiteralPath $ZipPath | Select-Object FullName, Length
$Hash = Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256
"$($Hash.Hash)  $([IO.Path]::GetFileName($ZipPath))" | Set-Content -LiteralPath ($ZipPath + '.sha256') -Encoding ASCII
Write-Host "SHA256: $($Hash.Hash)"

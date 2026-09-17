[CmdletBinding()]
param(
    [string]$OutputRoot = 'C:\dev\helworks\builds\helengine\portable-2026-09-11',
    [switch]$SkipPublish,
    [switch]$SkipHostPublish,
    [switch]$SkipSdk,
    [switch]$SkipImages,
    [switch]$NoZip
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$EngineRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$CodegenRoot = Join-Path $EngineRoot 'engine\vendor\csharpcodegen'
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$Package = Join-Path $OutputRoot 'Helengine'
$null = New-Item -ItemType Directory -Force -Path $Package

# Copy actual working-tree source bytes, including submodules, without developer settings or build output.
function Copy-SourceTree([string]$Source, [string]$Destination, [string[]]$Paths = @('.')) {
    $Files = @(& git -C $Source ls-files --recurse-submodules -- @Paths)
    if ($LASTEXITCODE -ne 0) { throw "Cannot enumerate source: $Source" }
    foreach ($Relative in $Files) {
        if ($Relative -match '(^|/)(bin|obj|build|builds|coverage|tmp|\.git|\.codex|\.agents|\.worktrees|user_settings)(/|$)' -or
            $Relative -match '\.(log|iso|pdb)$') { continue }
        $From = Join-Path $Source $Relative
        if (-not (Test-Path -LiteralPath $From -PathType Leaf)) { continue }
        $To = Join-Path $Destination $Relative
        $null = New-Item -ItemType Directory -Force -Path (Split-Path $To -Parent)
        Copy-Item -LiteralPath $From -Destination $To -Force
    }
}

# Robocopy preserves hidden runtime-support files and handles large SDK trees.
function Copy-Payload([string]$Source, [string]$Destination) {
    & robocopy $Source $Destination /E /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copy failed: $Source" }
}

# Each publish has its own workspace-owned log and isolated artifacts directory.
function Publish-Payload([string]$Project, [string]$Destination, [string]$Name) {
    $Log = Join-Path $OutputRoot "$Name-publish.log"
    Write-Host "Publishing $Name..."
    & dotnet publish $Project -c Release -o $Destination --artifacts-path (Join-Path $OutputRoot "artifacts\$Name") `
        '-p:UseAppHost=true' "-p:HelengineRoot=$EngineRoot" '-p:NuGetAudit=false' -m:1 *> $Log
    if ($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath $Log -Tail 35 | Out-Host
        throw "Publish failed: $Name. See $Log"
    }
}

$ManifestSource = Join-Path $EngineRoot 'user_settings\platforms.json'
$Entries = @((Get-Content $ManifestSource -Raw | ConvertFrom-Json).platforms)
$PackagedEntries = @()
$Provenance = @()
if (-not $SkipPublish -and -not $SkipHostPublish) {
    Publish-Payload (Join-Path $EngineRoot 'helengine.ui\helengine.editor.app\helengine.editor.app.csproj') (Join-Path $Package 'editor') 'editor'
    # The editor resolves AppContext.BaseDirectory\codegen\codegen.exe, so the tool ships inside the editor payload.
    Publish-Payload (Join-Path $CodegenRoot 'codegen\codegen.csproj') (Join-Path $Package 'editor\codegen') 'codegen'
}
Write-Host 'Copying engine sources and portable SDK...'
Copy-SourceTree $EngineRoot (Join-Path $Package 'sources\helengine') @('engine', 'submodules', 'Directory.Build.props', 'Directory.Packages.props', 'global.json', 'NuGet.Config', 'LICENSE')
if (-not $SkipSdk) {
$DotnetRoot = Split-Path (Get-Command dotnet).Source -Parent
$SdkRoot = Join-Path $Package 'tools\dotnet'
$null = New-Item -ItemType Directory -Force -Path $SdkRoot
Copy-Item (Join-Path $DotnetRoot 'dotnet.exe') $SdkRoot -Force
foreach ($Name in @('LICENSE.txt', 'ThirdPartyNotices.txt')) {
    Copy-Item (Join-Path $DotnetRoot $Name) $SdkRoot -Force
}
Copy-Payload (Join-Path $DotnetRoot 'sdk\9.0.308') (Join-Path $SdkRoot 'sdk\9.0.308')
Copy-Payload (Join-Path $DotnetRoot 'host\fxr\9.0.11') (Join-Path $SdkRoot 'host\fxr\9.0.11')
foreach ($Runtime in @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App', 'Microsoft.AspNetCore.App')) {
    Copy-Payload (Join-Path $DotnetRoot "shared\$Runtime\9.0.11") (Join-Path $SdkRoot "shared\$Runtime\9.0.11")
}
foreach ($PackName in @('Microsoft.NETCore.App.Ref', 'Microsoft.WindowsDesktop.App.Ref', 'Microsoft.AspNetCore.App.Ref', 'Microsoft.NETCore.App.Host.win-x64')) {
    Copy-Payload (Join-Path $DotnetRoot "packs\$PackName\9.0.11") (Join-Path $SdkRoot "packs\$PackName\9.0.11")
}
Copy-Payload (Join-Path $DotnetRoot 'packs\NETStandard.Library.Ref\2.1.0') (Join-Path $SdkRoot 'packs\NETStandard.Library.Ref\2.1.0')
Copy-Payload (Join-Path $DotnetRoot 'packs\Microsoft.NETCore.App.Ref\8.0.22') (Join-Path $SdkRoot 'packs\Microsoft.NETCore.App.Ref\8.0.22')
}

foreach ($Entry in $Entries) {
    $Id = $Entry.platformId
    $Source = if ([IO.Path]::IsPathRooted($Entry.playerSourceRootPath)) {
        [IO.Path]::GetFullPath($Entry.playerSourceRootPath)
    } else {
        [IO.Path]::GetFullPath((Join-Path (Split-Path $ManifestSource -Parent) $Entry.playerSourceRootPath))
    }
    $Player = Join-Path $Package "platforms\$Id\player"
    Write-Host "Packaging $Id..."
    Copy-SourceTree $Source $Player
    $BuilderProject = @(Get-ChildItem (Join-Path $Source 'builder') -Filter '*.csproj')[0].FullName
    $BuilderFramework = Split-Path (Split-Path $Entry.builderAssemblyPath -Parent) -Leaf
    $BuilderRelativePath = "platforms/$Id/player/builder/bin/Release/$BuilderFramework"
    if (-not $SkipPublish) { Publish-Payload $BuilderProject (Join-Path $Package $BuilderRelativePath) $Id }
    $BuilderName = [IO.Path]::GetFileName($Entry.builderAssemblyPath)
    if (-not (Test-Path (Join-Path $Package "$BuilderRelativePath/$BuilderName"))) { throw "Missing builder: $Id" }
    $Record = [ordered]@{
        engineVersion = $Entry.engineVersion
        platformId = $Id
        displayName = $Entry.displayName
        builderAssemblyPath = "../$BuilderRelativePath/$BuilderName"
        playerSourceRootPath = "../platforms/$Id/player"
        generatedCoreCppRootPath = '../generated-core'
    }
    if (Test-Path (Join-Path $Player 'platform-plugin.json')) { $Record.pluginManifestPath = "../platforms/$Id/player/platform-plugin.json" }
    $PackagedEntries += $Record
    $Provenance += [ordered]@{ repository = (Split-Path $Source -Leaf); commit = (& git -C $Source rev-parse HEAD); changes = @(& git -C $Source status --short) }
}
$null = New-Item -ItemType Directory -Force -Path (Join-Path $Package 'user_settings'), (Join-Path $Package 'generated-core'), (Join-Path $Package 'toolchains')
@{ platforms = $PackagedEntries } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $Package 'user_settings\platforms.json') -Encoding UTF8
$Provenance += [ordered]@{ repository = 'helengine'; commit = (& git -C $EngineRoot rev-parse HEAD); changes = @(& git -C $EngineRoot status --short) }
$Provenance += [ordered]@{ repository = 'csharpcodegen'; commit = (& git -C $CodegenRoot rev-parse HEAD); changes = @(& git -C $CodegenRoot status --short) }
$Provenance | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $Package 'source-revisions.json') -Encoding UTF8
foreach ($File in @('Start-Helengine.cmd', 'Start-Helengine.ps1', 'Join-Toolchains.ps1', 'Setup-Toolchains.cmd', 'README.txt', 'NuGet.Config')) {
    Copy-Item (Join-Path $PSScriptRoot "portable\$File") $Package -Force
}
$Images = @('helengine-ps2', 'helengine-psp', 'helengine-psvita', 'helengine-ds', 'helengine-3ds', 'helengine-gc', 'helengine-wii', 'helengine-wiiu', 'helengine-switch')
if (-not $SkipImages) {
    Write-Host 'Exporting console SDK images (this can take several minutes)...'
    & docker image save -o (Join-Path $Package 'toolchains\console-sdk-images.tar') @Images
    if ($LASTEXITCODE -ne 0) { throw 'Docker image export failed.' }
}
if (-not $NoZip) {
    Write-Host 'Creating GitHub-sized release ZIPs...'
    & (Join-Path $PSScriptRoot 'Write-PortableRelease.ps1') -PackageRoot $Package -ReleaseRoot (Join-Path $OutputRoot 'github-release')
}
Write-Host "Portable package: $Package"

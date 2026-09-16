param(
    [string]$Project = '',
    [switch]$Check,
    [switch]$SetupToolchains,
    [string]$Build = '',
    [string]$Output = ''
)
$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
if ($Project) { $Project = [IO.Path]::GetFullPath($Project) }
if ($Output) { $Output = [IO.Path]::GetFullPath($Output) }
$env:DOTNET_ROOT = Join-Path $Root 'tools\dotnet'
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
$env:DOTNET_CLI_HOME = Join-Path $Root 'user_settings\dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:MSBuildEnableWorkloadResolver = 'false'
$env:HELENGINE_SOURCE_ROOT = Join-Path $Root 'sources\helengine'
$env:HELENGINE_ROOT = $env:HELENGINE_SOURCE_ROOT
$env:HELENGINE_ENGINE_USER_SETTINGS_ROOT = Join-Path $Root 'user_settings'
foreach ($PlatformId in @('ps2', 'psp', 'psvita', 'ds', '3ds', 'gamecube', 'wii', 'switch')) {
    [Environment]::SetEnvironmentVariable("HELENGINE_$($PlatformId.ToUpperInvariant())_REPOSITORY_ROOT", (Join-Path $Root "platforms\$PlatformId\player"))
}
$env:HELENGINE_BUILD_CACHE_ROOT = ''
$env:HELENGINE_BUILD_WORKSPACE_ROOT = Join-Path $Root 'cache'
$env:NUGET_PACKAGES = Join-Path $Root 'tools\nuget-packages'
$env:TEMP = Join-Path $Root 'cache\temp'
$env:TMP = $env:TEMP
$null = New-Item -ItemType Directory -Force -Path $env:TEMP
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
Set-Location $Root
$Images = @('helengine-ps2', 'helengine-psp', 'helengine-psvita', 'helengine-ds', 'helengine-3ds', 'helengine-gc', 'helengine-wii', 'helengine-wiiu', 'helengine-switch')
if ($Check) {
    & "$env:DOTNET_ROOT\dotnet.exe" --list-sdks
    if ($LASTEXITCODE -ne 0) { throw 'Bundled .NET SDK could not start.' }
    $Manifest = Get-Content "$Root\user_settings\platforms.json" -Raw | ConvertFrom-Json
    foreach ($Entry in $Manifest.platforms) {
        foreach ($Property in @('builderAssemblyPath', 'playerSourceRootPath', 'generatedCoreCppRootPath', 'codegenToolPath', 'pluginManifestPath')) {
            if ($Entry.PSObject.Properties[$Property] -and -not (Test-Path (Join-Path "$Root\user_settings" $Entry.$Property))) { throw "Missing $Property for $($Entry.platformId)" }
        }
        Write-Host "$($Entry.platformId): payload paths OK"
    }
    exit 0
}
if ($SetupToolchains) {
    & docker info --format '{{.OSType}}'
    if ($LASTEXITCODE -ne 0) { throw 'Start Docker Desktop in Linux containers mode, then retry.' }
    & "$Root\Join-Toolchains.ps1"
    & docker load -i "$Root\toolchains\console-sdk-images.tar"
    if ($LASTEXITCODE -ne 0) { throw 'Console SDK image import failed.' }
    foreach ($Image in $Images) {
        & docker image inspect $Image --format '{{.Id}}'
        if ($LASTEXITCODE -ne 0) { throw "Missing console SDK image: $Image" }
    }
    Write-Host 'All console SDK images are ready.'
    exit 0
}
if ([string]::IsNullOrWhiteSpace($Project)) {
    Add-Type -AssemblyName System.Windows.Forms
    $Picker = New-Object System.Windows.Forms.OpenFileDialog
    $Picker.Title = 'Open a Helengine project'
    $Picker.Filter = 'Helengine project (*.heproj)|*.heproj'
    if ($Picker.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { exit 0 }
    $Project = $Picker.FileName
    $Picker.Dispose()
}
# Code generation opens these source projects through MSBuild. Restore recreates
# machine-specific dependency metadata after extraction or relocation.
foreach ($SourceProject in @(
    'sources\helengine\engine\helengine.core\helengine.core.csproj',
    'sources\helengine\engine\helengine.shader\helengine.shader.csproj',
    'sources\helengine\engine\helengine.input\helengine.input.csproj',
    'sources\helengine\engine\helengine.physics3d\helengine.physics3d.csproj',
    'platforms\ps2\player\managed\helengine.ps2\helengine.ps2.csproj'
)) {
    & "$env:DOTNET_ROOT\dotnet.exe" restore (Join-Path $Root $SourceProject) --configfile "$Root\NuGet.Config" --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Unable to prepare bundled source project: $SourceProject" }
}
$Arguments = @((Join-Path $Root 'editor\helengine.editor.app.dll'))
if ($Build) {
    if (-not $Output) { throw 'Use -Output with -Build.' }
    $Arguments += @('--project', $Project, '--build', $Build, '--output', $Output)
} else { $Arguments += $Project }
& "$env:DOTNET_ROOT\dotnet.exe" @Arguments
exit $LASTEXITCODE

$RepoRootPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$DotNetCliHomePath = Join-Path $RepoRootPath ".dotnet-home"
$NuGetPackagesPath = Join-Path $RepoRootPath ".nuget\packages"

if (!(Test-Path $DotNetCliHomePath)) {
    New-Item -ItemType Directory -Path $DotNetCliHomePath | Out-Null
}

if (!(Test-Path $NuGetPackagesPath)) {
    New-Item -ItemType Directory -Path $NuGetPackagesPath | Out-Null
}

$env:DOTNET_CLI_HOME = $DotNetCliHomePath
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_NOLOGO = "1"
$env:NUGET_PACKAGES = $NuGetPackagesPath

& dotnet @args
exit $LASTEXITCODE

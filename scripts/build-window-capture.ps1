[CmdletBinding()]
param(
    [ValidateSet('Build', 'NativeTests', 'ManagedTests', 'IntegrationTests', 'All')]
    [string]$Action = 'Build',
    [string]$TestFilter
)
$ErrorActionPreference = 'Stop'
$CaptureRepoRoot = Split-Path -Parent $PSScriptRoot
$CaptureOutputRoot = 'C:\dev\helworks\builds\helengine-capture\windows'
$CaptureScratchRoot = 'C:\temp\helengine-window-capture'
$CaptureVsWhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (!(Test-Path -LiteralPath $CaptureVsWhere)) { throw 'Visual Studio discovery tool is unavailable.' }
$CaptureVsRoot = & $CaptureVsWhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$CaptureVsRoot) { throw 'MSVC x64 tools are unavailable.' }
$CaptureMsBuild = Join-Path $CaptureVsRoot 'MSBuild\Current\Bin\MSBuild.exe'
if (!(Test-Path -LiteralPath $CaptureMsBuild)) { throw 'MSBuild was not found in the selected Visual Studio installation.' }
New-Item -ItemType Directory -Path $CaptureOutputRoot, $CaptureScratchRoot -Force | Out-Null
$CaptureNativeProject = Join-Path $CaptureRepoRoot 'engine\helengine.capture.windows.native\helengine.capture.windows.native.vcxproj'
$CaptureNativeTestProject = Join-Path $CaptureRepoRoot 'engine\helengine.capture.windows.tests\native\CaptureNativeTests.vcxproj'
$CaptureManagedTestProject = Join-Path $CaptureRepoRoot 'engine\helengine.capture.windows.tests\helengine.capture.windows.tests.csproj'
$CaptureManagedProject = Join-Path $CaptureRepoRoot 'engine\helengine.capture.windows\helengine.capture.windows.csproj'
$CaptureLog = Join-Path $CaptureScratchRoot 'build.log'

& $CaptureMsBuild $CaptureNativeProject /nologo /v:minimal /p:Configuration=Release /p:Platform=x64 "/p:NativeOutputRoot=$CaptureOutputRoot" *> $CaptureLog
$CaptureExit = $LASTEXITCODE
Get-Content -LiteralPath $CaptureLog -Tail 45
if ($CaptureExit -ne 0) { exit $CaptureExit }

if ($Action -in @('NativeTests', 'All')) {
    & $CaptureMsBuild $CaptureNativeTestProject /nologo /v:minimal /p:Configuration=Release /p:Platform=x64 "/p:NativeOutputRoot=$CaptureOutputRoot" *> $CaptureLog
    $CaptureExit = $LASTEXITCODE
    Get-Content -LiteralPath $CaptureLog -Tail 45
    if ($CaptureExit -ne 0) { exit $CaptureExit }
    & (Join-Path $CaptureOutputRoot 'helengine.capture.native.tests.exe')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    if ($Action -eq 'NativeTests') { exit 0 }
}
$CaptureOldCliHome = $env:DOTNET_CLI_HOME
Push-Location $CaptureRepoRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $CaptureScratchRoot 'dotnet-home'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'
    $CaptureSdkVersion = & dotnet --version
    if ($LASTEXITCODE -ne 0 -or $CaptureSdkVersion -ne '9.0.308') { throw 'The repository requires .NET SDK 9.0.308.' }
    $CaptureManagedArguments = @('-c', 'Release', '--artifacts-path', (Join-Path $CaptureOutputRoot 'managed'), '-p:UseCommonOutputDirectory=false')
    if ($Action -eq 'Build') {
        & dotnet build $CaptureManagedProject @CaptureManagedArguments *> $CaptureLog
    } else {
        $CaptureCategory = ''
        if ($Action -eq 'ManagedTests') { $CaptureCategory = 'Category=Contract' }
        if ($Action -eq 'IntegrationTests') { $CaptureCategory = 'Category=Integration' }
        if ($TestFilter) {
            if ($CaptureCategory) { $CaptureCategory = "($CaptureCategory)&($TestFilter)" }
            else { $CaptureCategory = $TestFilter }
        }
        $CaptureTestArguments = @('--results-directory', $CaptureScratchRoot)
        if ($CaptureCategory) { $CaptureTestArguments += @('--filter', $CaptureCategory) }
        & dotnet test $CaptureManagedTestProject @CaptureManagedArguments @CaptureTestArguments *> $CaptureLog
    }
    $CaptureExit = $LASTEXITCODE
    Get-Content -LiteralPath $CaptureLog -Tail 55
    exit $CaptureExit
} finally {
    Pop-Location
    $env:DOTNET_CLI_HOME = $CaptureOldCliHome
}

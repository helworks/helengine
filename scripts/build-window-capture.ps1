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

if ($Action -eq 'NativeTests') {
    & $CaptureMsBuild $CaptureNativeTestProject /nologo /v:minimal /p:Configuration=Release /p:Platform=x64 "/p:NativeOutputRoot=$CaptureOutputRoot" *> $CaptureLog
    $CaptureExit = $LASTEXITCODE
    Get-Content -LiteralPath $CaptureLog -Tail 45
    if ($CaptureExit -ne 0) { exit $CaptureExit }
    & (Join-Path $CaptureOutputRoot 'helengine.capture.native.tests.exe')
    exit $LASTEXITCODE
}
throw "Action $Action is not implemented yet; the native resource task currently provides NativeTests."

[CmdletBinding()]
param(
    [ValidateSet('Build', 'NativeTests', 'ManagedTests', 'IntegrationTests', 'All')]
    [string]$Action = 'Build',
    [string]$TestFilter,
    [string]$FFmpegSdkRoot = 'C:\env\ffmpeg-8.1.1-full_build-shared'
)
$ErrorActionPreference = 'Stop'
$RecordingRepo = Split-Path -Parent $PSScriptRoot
$RecordingOutput = 'C:\dev\helworks\builds\helengine-capture\windows'
$RecordingScratch = 'C:\temp\helengine-window-recording'
New-Item -ItemType Directory -Path $RecordingOutput, $RecordingScratch -Force | Out-Null
$RecordingLog = Join-Path $RecordingScratch 'build.log'
$RecordingVsWhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$RecordingVs = & $RecordingVsWhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$RecordingVs) { throw 'MSVC x64 tools are required.' }
$RecordingMsBuild = Join-Path $RecordingVs 'MSBuild\Current\Bin\MSBuild.exe'
if (!(Test-Path -LiteralPath (Join-Path $FFmpegSdkRoot 'include\libavcodec\avcodec.h'))) { throw 'FFmpeg SDK headers/import libraries/shared binaries are required.' }
$RecordingNative = Join-Path $RecordingRepo 'engine\helengine.video.ffmpeg\helengine.video.ffmpeg.vcxproj'
& $RecordingMsBuild $RecordingNative /nologo /v:minimal /p:Configuration=Release /p:Platform=x64 "/p:NativeOutputRoot=$RecordingOutput" "/p:FFmpegSdkRoot=$FFmpegSdkRoot" *> $RecordingLog
$RecordingExit = $LASTEXITCODE
Get-Content -LiteralPath $RecordingLog -Tail 35
if ($RecordingExit -ne 0) { exit $RecordingExit }
foreach ($RecordingDll in @('avcodec-62.dll', 'avformat-62.dll', 'avutil-60.dll', 'swscale-9.dll', 'swresample-6.dll')) {
    Copy-Item -LiteralPath (Join-Path $FFmpegSdkRoot "bin\$RecordingDll") -Destination $RecordingOutput -Force
}
if ($Action -in @('NativeTests', 'All')) {
    $RecordingNativeTests = Join-Path $RecordingRepo 'engine\helengine.video.ffmpeg.tests\native\HevcEncoderNativeTests.vcxproj'
    & $RecordingMsBuild $RecordingNativeTests /nologo /v:minimal /p:Configuration=Release /p:Platform=x64 "/p:NativeOutputRoot=$RecordingOutput" "/p:FFmpegSdkRoot=$FFmpegSdkRoot" *> $RecordingLog
    $RecordingExit = $LASTEXITCODE
    Get-Content -LiteralPath $RecordingLog -Tail 35
    if ($RecordingExit -ne 0) { exit $RecordingExit }
    & (Join-Path $RecordingOutput 'helengine.video.encoder.tests.exe')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    if ($Action -eq 'NativeTests') { exit 0 }
}
if ($Action -ne 'Build') { throw "The $Action projects have not been implemented yet." }

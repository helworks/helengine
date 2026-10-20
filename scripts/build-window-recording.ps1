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
$RecordingCaptureNative = Join-Path $RecordingRepo 'engine\helengine.capture.windows.native\helengine.capture.windows.native.vcxproj'
& $RecordingMsBuild $RecordingCaptureNative /nologo /v:minimal /p:Configuration=Release /p:Platform=x64 "/p:NativeOutputRoot=$RecordingOutput" *> $RecordingLog
$RecordingExit = $LASTEXITCODE
Get-Content -LiteralPath $RecordingLog -Tail 35
if ($RecordingExit -ne 0) { exit $RecordingExit }
$RecordingOldCliHome = $env:DOTNET_CLI_HOME
Push-Location $RecordingRepo
try {
    $env:DOTNET_CLI_HOME = Join-Path $RecordingScratch 'dotnet-home'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'
    if ((& dotnet --version) -ne '9.0.308') { throw 'The repository requires .NET SDK 9.0.308.' }
    $RecordingManagedArgs = @('-c', 'Release', '--artifacts-path', (Join-Path $RecordingOutput 'managed'), '-p:UseCommonOutputDirectory=false', "-p:RecordingNativeRoot=$RecordingOutput")
    $RecordingManagedProject = Join-Path $RecordingRepo 'engine\helengine.directx11.video\helengine.directx11.video.csproj'
    $RecordingTestProjects = @('engine\helengine.directx11.video.tests\helengine.directx11.video.tests.csproj')
    if (Test-Path -LiteralPath (Join-Path $RecordingRepo 'engine\helengine.recording.windows.tests\helengine.recording.windows.tests.csproj')) { $RecordingTestProjects += 'engine\helengine.recording.windows.tests\helengine.recording.windows.tests.csproj' }
    if (Test-Path -LiteralPath (Join-Path $RecordingRepo 'tools\window-recorder.tests\helengine.window-recorder.tests.csproj')) { $RecordingTestProjects += 'tools\window-recorder.tests\helengine.window-recorder.tests.csproj' }
    $RecordingCliProject = Join-Path $RecordingRepo 'tools\window-recorder\helengine.window-recorder.csproj'
    $ErrorActionPreference = 'Continue'
    & dotnet publish $RecordingCliProject @RecordingManagedArgs -o (Join-Path $RecordingOutput 'recorder') --no-self-contained *> $RecordingLog
    $RecordingExit = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    Get-Content -LiteralPath $RecordingLog -Tail 45
    if ($RecordingExit -ne 0) { exit $RecordingExit }
    if ($Action -eq 'Build') {
        $ErrorActionPreference = 'Continue'
        & dotnet build $RecordingManagedProject @RecordingManagedArgs *> $RecordingLog
        $RecordingExit = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        Get-Content -LiteralPath $RecordingLog -Tail 45
        if ($RecordingExit -ne 0) { exit $RecordingExit }
    } else {
        if ($Action -eq 'IntegrationTests') { throw 'Recording integration projects are not implemented yet.' }
        foreach ($RecordingTestProject in $RecordingTestProjects) {
            $RecordingTestArgs = @('--results-directory', $RecordingScratch)
            if ($TestFilter) { $RecordingTestArgs += @('--filter', $TestFilter) }
            $ErrorActionPreference = 'Continue'
            & dotnet test (Join-Path $RecordingRepo $RecordingTestProject) @RecordingManagedArgs @RecordingTestArgs *> $RecordingLog
            $RecordingExit = $LASTEXITCODE
            $ErrorActionPreference = 'Stop'
            Get-Content -LiteralPath $RecordingLog -Tail 55
            if ($RecordingExit -ne 0) { exit $RecordingExit }
        }
    }
} finally { Pop-Location; $env:DOTNET_CLI_HOME = $RecordingOldCliHome }

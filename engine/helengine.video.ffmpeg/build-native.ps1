param(
 [Parameter(Mandatory=$true)][string]$OutputDirectory,
 [string]$FFmpegSdkRoot='C:\env\ffmpeg-8.1.1-full_build-shared'
)
$ErrorActionPreference='Stop'
$buildRoot=[IO.Path]::GetFullPath('C:\dev\helworks\builds\helengine\media-composition')
$nativeOutput=[IO.Path]::GetFullPath($OutputDirectory)
if(-not $nativeOutput.StartsWith($buildRoot.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Native output must stay inside the visible media-composition build root.'}
$sdkRoot=(Resolve-Path -LiteralPath $FFmpegSdkRoot).Path
if(-not (Test-Path -LiteralPath (Join-Path $sdkRoot 'include\libavcodec\avcodec.h'))){throw 'FFmpeg development headers are required.'}
$msbuildPath='C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
New-Item -ItemType Directory -Force -Path $nativeOutput | Out-Null
& $msbuildPath (Join-Path $PSScriptRoot 'helengine.video.ffmpeg.vcxproj') /nologo /v:minimal /p:Configuration=Release /p:Platform=x64 "/p:FFmpegSdkRoot=$sdkRoot" "/p:NativeOutputRoot=$nativeOutput"
if($LASTEXITCODE -ne 0){throw 'Native video decoder build failed.'}
Get-ChildItem -LiteralPath (Join-Path $sdkRoot 'bin') -Filter '*.dll' | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $nativeOutput -Force}

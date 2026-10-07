param(
 [Parameter(Mandatory=$true)][string]$OutputDirectory,
 [string]$NativeDirectory='C:/dev/helworks/builds/helengine/media-composition/native',
 [string]$FFmpegSdkRoot='C:/env/ffmpeg-8.1.1-full_build-shared'
)
$ErrorActionPreference='Stop'
$taskBuildRoot=[IO.Path]::GetFullPath('C:/dev/helworks/builds/helengine/media-composition')
$taskOutput=[IO.Path]::GetFullPath($OutputDirectory)
if(-not $taskOutput.StartsWith($taskBuildRoot.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Composition packages must stay inside the visible media-composition build root.'}
if(Test-Path -LiteralPath $taskOutput){throw 'Packages are immutable. Choose a new output directory.'}
$taskSource=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskParent=[IO.Path]::GetDirectoryName($taskOutput)
New-Item -ItemType Directory -Force -Path $taskParent | Out-Null
$taskStage=Join-Path $taskParent ('.stage-'+[Guid]::NewGuid().ToString('N'))
if([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($taskStage)) -ne $taskParent){throw 'Package staging escaped its target parent.'}
& (Join-Path $taskSource 'engine/helengine.video.ffmpeg/build-native.ps1') -OutputDirectory $NativeDirectory -FFmpegSdkRoot $FFmpegSdkRoot
& dotnet publish (Join-Path $taskSource 'engine/helengine.vfx.cli/helengine.vfx.cli.csproj') -c Release "-p:MediaNativeDirectory=$NativeDirectory" -o $taskStage
if($LASTEXITCODE -ne 0){throw 'Could not publish the composition CLI.'}
$taskSchemas=Join-Path $taskStage 'Schemas'
New-Item -ItemType Directory -Force -Path $taskSchemas | Out-Null
Copy-Item -LiteralPath (Join-Path $taskSource 'engine/helengine.media/Schemas/helengine.media.composition.v1.schema.json') -Destination $taskSchemas
$taskCatalog=& dotnet (Join-Path $taskStage 'helengine.vfx.cli.dll') composition capabilities --json
if($LASTEXITCODE -ne 0){throw 'Published composition catalog failed.'}
[IO.File]::WriteAllText((Join-Path $taskStage 'media-capabilities.json'),($taskCatalog -join "`n"),(New-Object Text.UTF8Encoding($false)))
foreach($taskRequired in @('helengine.video.ffmpeg.dll','avcodec-62.dll','avformat-62.dll','avutil-60.dll','swresample-6.dll','media/shaders/MediaComposite.hlsl','media/shaders/MediaCrossfade.hlsl')){
 if(-not(Test-Path -LiteralPath (Join-Path $taskStage $taskRequired))){throw "Package dependency is missing: $taskRequired"}
}
$taskFiles=@(Get-ChildItem -LiteralPath $taskStage -File -Recurse | ForEach-Object {[PSCustomObject]@{path=$_.FullName.Substring($taskStage.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
$taskReceipt=[PSCustomObject]@{schema='helengine.media.package.v1';compositionSchema='helengine.media.composition.v1';nativeSdk='ffmpeg-8.1.1-shared';files=$taskFiles}
[IO.File]::WriteAllText((Join-Path $taskStage 'package-receipt.json'),($taskReceipt|ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)))
foreach($taskLicense in @('LICENSE.txt','LICENSE','COPYING.GPLv3','COPYING.LGPLv3')){if(Test-Path -LiteralPath (Join-Path $FFmpegSdkRoot $taskLicense)){Copy-Item -LiteralPath (Join-Path $FFmpegSdkRoot $taskLicense) -Destination $taskStage}}
if([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($taskStage)) -ne $taskParent -or [IO.Path]::GetDirectoryName($taskOutput) -ne $taskParent){throw 'Final package paths escaped their target parent.'}
Move-Item -LiteralPath $taskStage -Destination $taskOutput
Write-Output "Composition package: $taskOutput"

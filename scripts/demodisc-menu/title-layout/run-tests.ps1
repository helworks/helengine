$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$temp=Join-Path $root 'tmp'
New-Item -ItemType Directory -Path $temp -Force | Out-Null
$env:TEMP=$temp
$env:TMP=$temp
& (Join-Path $root 'prepare-source.ps1')
dotnet run --project (Join-Path $root 'TitleFitTests.csproj') --configuration Release --verbosity quiet
if($LASTEXITCODE -ne 0){throw "Title fitting validation failed: $LASTEXITCODE"}

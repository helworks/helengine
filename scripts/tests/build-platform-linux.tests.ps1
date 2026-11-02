[CmdletBinding()]
param([string]$WorkRoot = '/tmp/heldota-dota-resume/webgpu/platform-wrapper-fixture')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
    Write-Host 'Linux apphost fixture is not applicable on Windows.'
    exit 0
}
$EngineRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$ProjectRoot = Join-Path $WorkRoot 'project'
$OutputRoot = Join-Path $WorkRoot 'output'
$CacheRoot = Join-Path $WorkRoot 'cache'
$EditorMarker = Join-Path $OutputRoot 'editor-invoked.txt'
if (Test-Path -LiteralPath $EditorMarker) { Remove-Item -LiteralPath $EditorMarker }
$null = New-Item -ItemType Directory -Path $ProjectRoot -Force
Set-Content -LiteralPath (Join-Path $ProjectRoot 'project.heproj') -Value '{}'
$FakeDotNet = Join-Path $WorkRoot 'fake-dotnet'
@'
#!/usr/bin/env python3
import pathlib, sys
arguments = sys.argv[1:]
if arguments[0] == 'publish':
    output = pathlib.Path(arguments[arguments.index('-o') + 1])
    output.mkdir(parents=True, exist_ok=True)
    name = 'codegen' if pathlib.Path(arguments[1]).name == 'codegen.csproj' else 'helengine.editor.app.dll'
    (output / name).write_text('published fixture')
elif arguments[0] != 'restore':
    output = pathlib.Path(arguments[arguments.index('--output') + 1])
    (output / 'editor-invoked.txt').write_text('host codegen accepted')
'@ | Set-Content -LiteralPath $FakeDotNet
& chmod +x $FakeDotNet
$PreviousDotNet = $env:HELENGINE_DOTNET_EXECUTABLE_PATH
try {
    $env:HELENGINE_DOTNET_EXECUTABLE_PATH = $FakeDotNet
    & pwsh -NoLogo -NoProfile -File (Join-Path $EngineRoot 'scripts/build-platform.ps1') `
        -Project $ProjectRoot -Platform webgpu -Output $OutputRoot -CacheRoot $CacheRoot `
        -EditorProject (Join-Path $EngineRoot 'scripts/tests/fixtures/build-platform-smoke-builder/helengine.buildplatform.smokebuilder.csproj')
    if ($LASTEXITCODE -ne 0) { throw "Linux build-platform fixture failed with exit $LASTEXITCODE." }
    if (-not (Test-Path -LiteralPath (Join-Path $OutputRoot 'editor-invoked.txt'))) { throw 'Editor was not invoked after publishing the host codegen apphost.' }
    $State = Get-Content -LiteralPath (Join-Path $OutputRoot '.helengine-build-state.json') -Raw | ConvertFrom-Json
    if ($State.status -ne 'succeeded') { throw "Unexpected terminal build state '$($State.status)'." }
    Write-Host 'PASS: official wrapper accepts the bare Linux codegen apphost and invokes the editor.'
} finally {
    $env:HELENGINE_DOTNET_EXECUTABLE_PATH = $PreviousDotNet
}

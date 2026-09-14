param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
)
$ErrorActionPreference = "Stop"
$architectureProject = Join-Path $RepositoryRoot "engine/helengine.architecture.tests/helengine.architecture.tests.csproj"
if (-not (Test-Path -LiteralPath $architectureProject)) { throw "Architecture test project was not found: $architectureProject" }
dotnet test $architectureProject --no-restore -m:1 -v:minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
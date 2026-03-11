param(
    [string]$OutputDir = "helengine.ui/helengine.editor.app/content/icons/toolbar",
    [int]$Size = 64
)

$projectPath = Join-Path $PSScriptRoot "helengine.editor.iconbuilder.csproj"
if (-not (Test-Path $projectPath)) {
    throw "Icon generator project not found at $projectPath."
}

dotnet run --project $projectPath -- --output-dir $OutputDir --size $Size
if ($LASTEXITCODE -ne 0) {
    throw "Toolbar icon generation failed."
}

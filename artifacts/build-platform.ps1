[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Project,

    [Parameter(Mandatory = $true)]
    [string]$Platform,

    [Parameter(Mandatory = $true)]
    [string]$Output,

    [Parameter()]
    [string]$Configuration = "Debug",

    [Parameter()]
    [string]$EditorProject = "",

    [Parameter()]
    [string[]]$AdditionalArgs = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Project)) { [Console]::Error.WriteLine("Project is required."); exit 2 }
if ([string]::IsNullOrWhiteSpace($Platform)) { [Console]::Error.WriteLine("Platform is required."); exit 2 }
if ([string]::IsNullOrWhiteSpace($Output)) { [Console]::Error.WriteLine("Output is required."); exit 2 }
if ([string]::IsNullOrWhiteSpace($Configuration)) { [Console]::Error.WriteLine("Configuration is required."); exit 2 }

try {
    if ([string]::IsNullOrWhiteSpace($EditorProject)) {
        $EditorProject = Join-Path $PSScriptRoot "..\\helengine.ui\\helengine.editor.app\\helengine.editor.app.csproj"
    }

    $ResolvedEditorProject = [System.IO.Path]::GetFullPath($EditorProject)
    if (-not (Test-Path -LiteralPath $ResolvedEditorProject -PathType Leaf)) {
        [Console]::Error.WriteLine("Editor project was not found at '$ResolvedEditorProject'.")
        exit 3
    }

    $ResolvedProjectCandidate = [System.IO.Path]::GetFullPath($Project)
    if (Test-Path -LiteralPath $ResolvedProjectCandidate -PathType Container) {
        $ResolvedProjectCandidate = Join-Path $ResolvedProjectCandidate "project.heproj"
    }

    $ResolvedProjectPath = [System.IO.Path]::GetFullPath($ResolvedProjectCandidate)
    if (-not (Test-Path -LiteralPath $ResolvedProjectPath -PathType Leaf)) {
        [Console]::Error.WriteLine("Project file was not found at '$ResolvedProjectPath'. Pass either a project directory that contains project.heproj or an explicit .heproj path.")
        exit 4
    }

    $ResolvedOutputPath = [System.IO.Path]::GetFullPath($Output)
    if (-not (Test-Path -LiteralPath $ResolvedOutputPath -PathType Container)) {
        $null = New-Item -ItemType Directory -Path $ResolvedOutputPath -Force
    }

    $DotNetArguments = @(
        "run",
        "--project",
        $ResolvedEditorProject,
        "-c",
        $Configuration,
        "--",
        "--project",
        $ResolvedProjectPath,
        "--build",
        $Platform,
        "--output",
        $ResolvedOutputPath
    )

    if ($AdditionalArgs.Count -gt 0) {
        $DotNetArguments += $AdditionalArgs
    }

    $DisplayArguments = @("dotnet")
    foreach ($Argument in $DotNetArguments) {
        if ($Argument -match '[\s"]') {
            $DisplayArguments += '"' + $Argument.Replace('"', '\"') + '"'
        } else {
            $DisplayArguments += $Argument
        }
    }

    Write-Host ("Executing: " + ($DisplayArguments -join " "))

    & dotnet @DotNetArguments
    $DotNetExitCode = $LASTEXITCODE
    if ($DotNetExitCode -ne 0) {
        [Console]::Error.WriteLine("Editor platform build failed with exit code $DotNetExitCode.")
        exit $DotNetExitCode
    }

    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 10
}

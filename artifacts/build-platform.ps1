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

function Get-SafePathSegment {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "Path segment must be provided."
    }

    $InvalidCharacters = [System.IO.Path]::GetInvalidFileNameChars()
    $Builder = New-Object System.Text.StringBuilder
    foreach ($Character in $Value.ToCharArray()) {
        if ($InvalidCharacters -contains $Character) {
            $null = $Builder.Append('_')
        } else {
            $null = $Builder.Append($Character)
        }
    }

    return $Builder.ToString()
}

function Get-ProjectIsolationHash {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRootPath
    )

    if ([string]::IsNullOrWhiteSpace($ProjectRootPath)) {
        throw "Project root path must be provided."
    }

    $FullProjectRootPath = [System.IO.Path]::GetFullPath($ProjectRootPath)
    $ProjectRootBytes = [System.Text.Encoding]::UTF8.GetBytes($FullProjectRootPath)
    $Sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $HashBytes = $Sha256.ComputeHash($ProjectRootBytes)
    } finally {
        $Sha256.Dispose()
    }
    $Builder = New-Object System.Text.StringBuilder
    for ($Index = 0; $Index -lt 16; $Index++) {
        $null = $Builder.Append($HashBytes[$Index].ToString("x2"))
    }

    return $Builder.ToString()
}

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

    $ResolvedProjectRootPath = Split-Path -Parent $ResolvedProjectPath
    $ProjectIsolationHash = Get-ProjectIsolationHash -ProjectRootPath $ResolvedProjectRootPath
    $PlatformIsolationSegment = Get-SafePathSegment -Value $Platform
    $ResolvedHelEngineRootPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
    $EditorIsolationRootPath = Join-Path ([System.IO.Path]::GetTempPath()) ("helengine-builds\" + $ProjectIsolationHash + "\" + $PlatformIsolationSegment + "\editor-app")
    $EditorArtifactsPath = Join-Path $EditorIsolationRootPath "artifacts"

    $ResolvedOutputPath = [System.IO.Path]::GetFullPath($Output)
    if (-not (Test-Path -LiteralPath $ResolvedOutputPath -PathType Container)) {
        $null = New-Item -ItemType Directory -Path $ResolvedOutputPath -Force
    }

    $DotNetSharedPropertyArguments = @(
        "--artifacts-path",
        $EditorArtifactsPath
    )

    $DotNetRestoreArguments = @(
        "restore",
        $ResolvedEditorProject
    ) + $DotNetSharedPropertyArguments

    $DotNetArguments = @(
        "run",
        "--no-restore",
        "--project",
        $ResolvedEditorProject,
        "-c",
        $Configuration
    ) + $DotNetSharedPropertyArguments + @(
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

    $RestoreDisplayArguments = @("dotnet")
    foreach ($Argument in $DotNetRestoreArguments) {
        if ($Argument -match '[\s"]') {
            $RestoreDisplayArguments += '"' + $Argument.Replace('"', '\"') + '"'
        } else {
            $RestoreDisplayArguments += $Argument
        }
    }

    Write-Host ("Restoring: " + ($RestoreDisplayArguments -join " "))

    & dotnet @DotNetRestoreArguments
    $DotNetRestoreExitCode = $LASTEXITCODE
    if ($DotNetRestoreExitCode -ne 0) {
        [Console]::Error.WriteLine("Editor project restore failed with exit code $DotNetRestoreExitCode.")
        exit $DotNetRestoreExitCode
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

    $OriginalHelEngineSourceRootPath = $env:HELENGINE_SOURCE_ROOT
    try {
        $env:HELENGINE_SOURCE_ROOT = $ResolvedHelEngineRootPath
        & dotnet @DotNetArguments
        $DotNetExitCode = $LASTEXITCODE
        if ($DotNetExitCode -ne 0) {
            [Console]::Error.WriteLine("Editor platform build failed with exit code $DotNetExitCode.")
            exit $DotNetExitCode
        }
    } finally {
        $env:HELENGINE_SOURCE_ROOT = $OriginalHelEngineSourceRootPath
    }

    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 10
}

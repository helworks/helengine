[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepositoryRootPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$RealEditorSmokeScriptPath = Join-Path $RepositoryRootPath "scripts\tests\build-platform-real-editor-smoke.tests.ps1"
$TemporaryRootPath = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$TestBuildRootPath = Join-Path $TemporaryRootPath "helengine-build-platform-tests"
$CollisionRootPath = Join-Path $TestBuildRootPath ("real-editor-ownership-" + [Guid]::NewGuid().ToString("N"))
$HarnessScriptPath = Join-Path $TestBuildRootPath ("real-editor-ownership-harness-" + [Guid]::NewGuid().ToString("N") + ".ps1")
$TestBuildRootCreated = $false
$CollisionRootCreated = $false

function Test-StrictDescendantPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ParentPath,

        [Parameter(Mandatory = $true)]
        [string]$CandidatePath
    )

    $DirectorySeparators = [char[]]@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar
    )
    $CanonicalParentPath = [System.IO.Path]::GetFullPath($ParentPath).TrimEnd($DirectorySeparators)
    $CanonicalCandidatePath = [System.IO.Path]::GetFullPath($CandidatePath).TrimEnd($DirectorySeparators)
    $Prefix = $CanonicalParentPath + [System.IO.Path]::DirectorySeparatorChar
    return $CanonicalCandidatePath.StartsWith($Prefix, [System.StringComparison]::OrdinalIgnoreCase)
}

try {
    if (-not (Test-Path -LiteralPath $TestBuildRootPath -PathType Container)) {
        $null = New-Item -ItemType Directory -Path $TestBuildRootPath -ErrorAction Stop
        $TestBuildRootCreated = $true
    }
    if (-not (Test-StrictDescendantPath -ParentPath $TemporaryRootPath -CandidatePath $TestBuildRootPath)) {
        throw "Test build root '$TestBuildRootPath' must be a strict descendant of '$TemporaryRootPath'."
    }
    if (Test-Path -LiteralPath $CollisionRootPath) {
        throw "Collision root '$CollisionRootPath' unexpectedly already exists."
    }
    if (-not (Test-StrictDescendantPath -ParentPath $TestBuildRootPath -CandidatePath $CollisionRootPath)) {
        throw "Collision root '$CollisionRootPath' must be a strict descendant of '$TestBuildRootPath'."
    }

    $null = New-Item -ItemType Directory -Path $CollisionRootPath -ErrorAction Stop
    $CollisionRootCreated = $true

    $SmokeSource = Get-Content -LiteralPath $RealEditorSmokeScriptPath -Raw
    $GeneratedRootAssignment = '$TestRootPath = Join-Path $TestBuildRootPath ("build platform real editor smoke " + [Guid]::NewGuid().ToString("N"))'
    if ($SmokeSource.IndexOf($GeneratedRootAssignment, [System.StringComparison]::Ordinal) -lt 0) {
        throw "Real-editor smoke script no longer has the expected generated-root assignment."
    }

    $FixtureMarker = '    $FixtureRelativePaths = @('
    if ($SmokeSource.IndexOf($FixtureMarker, [System.StringComparison]::Ordinal) -lt 0) {
        throw "Real-editor smoke script no longer has the expected fixture marker."
    }

    $CollisionRootAssignment = '$TestRootPath = "' + $CollisionRootPath + '"'
    $HarnessSource = $SmokeSource.Replace($GeneratedRootAssignment, $CollisionRootAssignment)
    $HarnessSource = $HarnessSource.Replace(
        $FixtureMarker,
        '    throw "OWNERSHIP_HARNESS_REACHED_UNOWNED_WORK"' + [Environment]::NewLine + $FixtureMarker)
    [System.IO.File]::WriteAllText($HarnessScriptPath, $HarnessSource, [System.Text.UTF8Encoding]::new($false))

    $OriginalErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $InvocationOutput = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $HarnessScriptPath 2>&1)
        $InvocationExitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $OriginalErrorActionPreference
    }

    if ($InvocationExitCode -eq 0) {
        throw "Collision harness unexpectedly succeeded."
    }
    $InvocationText = $InvocationOutput -join [Environment]::NewLine
    $OwnershipErrors = New-Object System.Collections.ArrayList
    if ($InvocationText.IndexOf("unexpectedly already exists", [System.StringComparison]::Ordinal) -lt 0) {
        $null = $OwnershipErrors.Add("Collision harness failed without the expected root-collision diagnostic. $InvocationText")
    }
    if (-not (Test-Path -LiteralPath $CollisionRootPath -PathType Container)) {
        $null = $OwnershipErrors.Add("Real-editor smoke deleted the pre-existing collision root '$CollisionRootPath'.")
    }
    if ($OwnershipErrors.Count -ne 0) {
        throw ($OwnershipErrors -join " | ")
    }

    Write-Output "REAL_EDITOR_SMOKE_OWNERSHIP_TEST_PASS"
} finally {
    if (Test-Path -LiteralPath $HarnessScriptPath -PathType Leaf) {
        if (-not (Test-StrictDescendantPath -ParentPath $TestBuildRootPath -CandidatePath $HarnessScriptPath)) {
            throw "Refusing to remove collision harness '$HarnessScriptPath' outside '$TestBuildRootPath'."
        }
        Remove-Item -LiteralPath $HarnessScriptPath -Force
    }
    if ($CollisionRootCreated -and (Test-Path -LiteralPath $CollisionRootPath)) {
        if (-not (Test-StrictDescendantPath -ParentPath $TestBuildRootPath -CandidatePath $CollisionRootPath)) {
            throw "Refusing to remove collision root '$CollisionRootPath' outside '$TestBuildRootPath'."
        }
        Remove-Item -LiteralPath $CollisionRootPath -Recurse -Force
    }
    if ($TestBuildRootCreated -and (Test-Path -LiteralPath $TestBuildRootPath -PathType Container)) {
        Remove-Item -LiteralPath $TestBuildRootPath -Force
    }
}

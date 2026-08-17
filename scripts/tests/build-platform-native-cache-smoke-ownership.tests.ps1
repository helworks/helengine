[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepositoryRootPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\\.."))
$NativeSmokeScriptPath = Join-Path $RepositoryRootPath "scripts\\tests\\build-platform-native-cache-smoke.tests.ps1"
$TemporaryRootPath = [System.IO.Path]::GetFullPath("C:\\tmp")
$CollisionRootPath = Join-Path $TemporaryRootPath ("hbp-ownership-" + [Guid]::NewGuid().ToString("N"))
$HarnessScriptPath = Join-Path $TemporaryRootPath ("hbp-ownership-harness-" + [Guid]::NewGuid().ToString("N") + ".ps1")
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
    if (-not (Test-Path -LiteralPath $TemporaryRootPath -PathType Container)) {
        throw "Temporary root '$TemporaryRootPath' is required."
    }
    if (Test-Path -LiteralPath $CollisionRootPath) {
        throw "Collision root '$CollisionRootPath' unexpectedly already exists."
    }
    if (-not (Test-StrictDescendantPath -ParentPath $TemporaryRootPath -CandidatePath $CollisionRootPath)) {
        throw "Collision root '$CollisionRootPath' must be a strict descendant of '$TemporaryRootPath'."
    }

    $null = New-Item -ItemType Directory -Path $CollisionRootPath -ErrorAction Stop
    $CollisionRootCreated = $true

    $NativeSmokeSource = Get-Content -LiteralPath $NativeSmokeScriptPath -Raw
    $GeneratedRootAssignment = '$TestRootPath = Join-Path $TemporaryRootPath ("hbp-" + [Guid]::NewGuid().ToString("N"))'
    if ($NativeSmokeSource.IndexOf($GeneratedRootAssignment, [System.StringComparison]::Ordinal) -lt 0) {
        throw "Native smoke script no longer has the expected generated-root assignment."
    }

    $CollisionRootAssignment = '$TestRootPath = "' + $CollisionRootPath + '"'
    $HarnessSource = $NativeSmokeSource.Replace($GeneratedRootAssignment, $CollisionRootAssignment)
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
    if ($InvocationText.IndexOf("unexpectedly already exists", [System.StringComparison]::Ordinal) -lt 0) {
        throw "Collision harness failed without the expected root-collision diagnostic. $InvocationText"
    }
    if (-not (Test-Path -LiteralPath $CollisionRootPath -PathType Container)) {
        throw "Native smoke deleted the pre-existing collision root '$CollisionRootPath'."
    }

    Write-Output "NATIVE_CACHE_SMOKE_OWNERSHIP_TEST_PASS"
} finally {
    if (Test-Path -LiteralPath $HarnessScriptPath -PathType Leaf) {
        if (-not (Test-StrictDescendantPath -ParentPath $TemporaryRootPath -CandidatePath $HarnessScriptPath)) {
            throw "Refusing to remove collision harness '$HarnessScriptPath' outside temporary root '$TemporaryRootPath'."
        }
        Remove-Item -LiteralPath $HarnessScriptPath -Force
    }
    if ($CollisionRootCreated -and (Test-Path -LiteralPath $CollisionRootPath)) {
        if (-not (Test-StrictDescendantPath -ParentPath $TemporaryRootPath -CandidatePath $CollisionRootPath)) {
            throw "Refusing to remove collision root '$CollisionRootPath' outside temporary root '$TemporaryRootPath'."
        }
        Remove-Item -LiteralPath $CollisionRootPath -Recurse -Force
    }
}

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$WrapperPath = Join-Path $RepositoryRoot "scripts\build-platform.ps1"
$CacheModulePath = Join-Path $RepositoryRoot "scripts\build-platform\BuildPlatformCache.psm1"
$EnvironmentModulePath = Join-Path $RepositoryRoot "scripts\build-platform\BuildPlatformEnvironment.psm1"
Import-Module $CacheModulePath -Force
if (-not (Test-Path -LiteralPath $EnvironmentModulePath -PathType Leaf)) {
    throw "The explicit build environment state module was not found at '$EnvironmentModulePath'."
}
Import-Module $EnvironmentModulePath -Force
$TestBuildRootPath = Join-Path ([System.IO.Path]::GetTempPath()) "helengine-build-platform-tests"
$TestRootPath = Join-Path $TestBuildRootPath ("build-platform-workspace-" + [Guid]::NewGuid().ToString("N"))
$FakeToolsPath = Join-Path $TestRootPath "fake-tools"
$CapturePath = Join-Path $TestRootPath "dotnet-invocations.txt"
$EnvironmentCapturePath = Join-Path $TestRootPath "editor-environment.txt"
$EnvironmentEnumerationGuardPath = Join-Path $TestRootPath "reject-environment-enumeration.ps1"
$RobocopyMarkerPath = Join-Path $TestRootPath "robocopy-invoked.txt"
$ProjectPath = Join-Path $TestRootPath "authored-project\project.heproj"
$EditorProjectPath = Join-Path $TestRootPath "editor\editor.csproj"
$CacheRootPath = Join-Path $TestRootPath "cache"
$EquivalentWorkspaceRootPath = $CacheRootPath + [System.IO.Path]::DirectorySeparatorChar
$OutputPath = Join-Path $TestRootPath "output"
$OutputArgumentPath = $OutputPath + [System.IO.Path]::DirectorySeparatorChar
$DifferentWorkspaceRootPath = Join-Path $TestRootPath "different-cache"

function Invoke-ControlledWrapper {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$CachePath,

        [Parameter()]
        [string]$WorkspacePath = "",

        [Parameter()]
        [int]$PruneDays = 0
    )

    $Arguments = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $WrapperPath,
        "-Project",
        $ProjectPath,
        "-Platform",
        "windows",
        "-Output",
        $OutputArgumentPath,
        "-Configuration",
        "Release",
        "-BuildProfile",
        "profiler",
        "-EditorProject",
        $EditorProjectPath
    )
    if (-not [string]::IsNullOrWhiteSpace($CachePath)) {
        $Arguments += @("-CacheRoot", $CachePath)
    }
    if (-not [string]::IsNullOrWhiteSpace($WorkspacePath)) {
        $Arguments += @("-WorkspaceRoot", $WorkspacePath)
    }
    if ($PruneDays -ne 0) {
        $Arguments += @("-PruneCacheOlderThanDays", $PruneDays)
    }

    $OriginalErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $InvocationOutput = @(& powershell.exe @Arguments 2>&1)
        $InvocationExitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $OriginalErrorActionPreference
    }
    return [pscustomobject]@{
        ExitCode = $InvocationExitCode
        Output = $InvocationOutput
    }
}

function Get-CapturedArgumentValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Invocation,

        [Parameter(Mandatory = $true)]
        [string]$ArgumentName
    )

    $Pattern = [regex]::Escape($ArgumentName) + ' (?:(?:"([^\"]*)")|(\S+))'
    $Match = [regex]::Match($Invocation, $Pattern)
    if (-not $Match.Success) {
        throw "Invocation '$Invocation' did not contain argument '$ArgumentName'."
    }
    if ($Match.Groups[1].Success) {
        return $Match.Groups[1].Value
    }
    return $Match.Groups[2].Value
}

function Get-ExpectedProjectHash {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRootPath
    )

    $CanonicalProjectIdentityPath = [System.IO.Path]::GetFullPath($ProjectRootPath).ToLowerInvariant()
    $Bytes = [System.Text.Encoding]::UTF8.GetBytes($CanonicalProjectIdentityPath)
    $Sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $HashBytes = $Sha256.ComputeHash($Bytes)
    } finally {
        $Sha256.Dispose()
    }

    $Builder = New-Object System.Text.StringBuilder
    for ($Index = 0; $Index -lt 16; $Index++) {
        $null = $Builder.Append($HashBytes[$Index].ToString("x2"))
    }
    return $Builder.ToString()
}

function Assert-Success {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Result,

        [Parameter(Mandatory = $true)]
        [string]$CaseName
    )

    if ($Result.ExitCode -ne 0) {
        throw "$CaseName failed with exit code $($Result.ExitCode). $($Result.Output -join [Environment]::NewLine)"
    }
}

$TrackedEnvironmentVariableNames = @(
    "HELENGINE_BUILD_CACHE_ROOT",
    "HELENGINE_BUILD_CONFIGURATION",
    "HELENGINE_BUILD_PROFILE",
    "HELENGINE_SOURCE_ROOT"
)
$TestProcessEnvironmentBefore = @{}
foreach ($EnvironmentVariableName in $TrackedEnvironmentVariableNames) {
    $TestProcessEnvironmentBefore[$EnvironmentVariableName] = [Environment]::GetEnvironmentVariable(
        $EnvironmentVariableName,
        [EnvironmentVariableTarget]::Process
    )
}
try {
    [Environment]::SetEnvironmentVariable("HELENGINE_BUILD_CACHE_ROOT", "InheritedCacheRoot", [EnvironmentVariableTarget]::Process)
    [Environment]::SetEnvironmentVariable("HELENGINE_BUILD_CONFIGURATION", "InheritedConfiguration", [EnvironmentVariableTarget]::Process)
    [Environment]::SetEnvironmentVariable("HELENGINE_BUILD_PROFILE", $null, [EnvironmentVariableTarget]::Process)
    [Environment]::SetEnvironmentVariable("HELENGINE_SOURCE_ROOT", "InheritedSourceRoot", [EnvironmentVariableTarget]::Process)

    $SavedEnvironmentState = Save-BuildPlatformEnvironmentState
    foreach ($EnvironmentVariableName in $TrackedEnvironmentVariableNames) {
        [Environment]::SetEnvironmentVariable($EnvironmentVariableName, "MutatedValue", [EnvironmentVariableTarget]::Process)
    }
    Restore-BuildPlatformEnvironmentState -State $SavedEnvironmentState

    foreach ($ExpectedEnvironmentValue in @{
            HELENGINE_BUILD_CACHE_ROOT = "InheritedCacheRoot"
            HELENGINE_BUILD_CONFIGURATION = "InheritedConfiguration"
            HELENGINE_SOURCE_ROOT = "InheritedSourceRoot"
        }.GetEnumerator()) {
        $ActualEnvironmentValue = [Environment]::GetEnvironmentVariable(
            $ExpectedEnvironmentValue.Key,
            [EnvironmentVariableTarget]::Process
        )
        if ($ActualEnvironmentValue -cne $ExpectedEnvironmentValue.Value) {
            throw "Environment restoration changed $($ExpectedEnvironmentValue.Key) from '$($ExpectedEnvironmentValue.Value)' to '$ActualEnvironmentValue'."
        }
    }
    if ($null -ne [Environment]::GetEnvironmentVariable("HELENGINE_BUILD_PROFILE", [EnvironmentVariableTarget]::Process)) {
        throw "Environment restoration did not remove originally absent HELENGINE_BUILD_PROFILE."
    }
} finally {
    foreach ($EnvironmentVariableName in $TrackedEnvironmentVariableNames) {
        [Environment]::SetEnvironmentVariable(
            $EnvironmentVariableName,
            $TestProcessEnvironmentBefore[$EnvironmentVariableName],
            [EnvironmentVariableTarget]::Process
        )
    }
}

foreach ($DotSegment in @(".", "..")) {
    $SafeSegmentWasRejected = $false
    try {
        $null = Get-BuildPlatformSafeSegment -Value $DotSegment
    } catch {
        $SafeSegmentWasRejected = $true
    }
    if (-not $SafeSegmentWasRejected) {
        throw "Get-BuildPlatformSafeSegment accepted traversal segment '$DotSegment'."
    }

    foreach ($LayoutParameterName in @("Platform", "Configuration", "BuildProfile")) {
        $LayoutArguments = @{
            CacheRootPath = $CacheRootPath
            ProjectRootPath = Split-Path -Parent $ProjectPath
            Platform = "windows"
            Configuration = "Release"
            BuildProfile = "profiler"
        }
        $LayoutArguments[$LayoutParameterName] = $DotSegment
        $LayoutWasRejected = $false
        try {
            $null = Resolve-BuildPlatformCacheLayout @LayoutArguments
        } catch {
            $LayoutWasRejected = $true
        }
        if (-not $LayoutWasRejected) {
            throw "Resolve-BuildPlatformCacheLayout accepted '$DotSegment' for $LayoutParameterName."
        }
    }
}

foreach ($AliasedSegment in @(
        "profile.",
        "profile ",
        "CON",
        "con.cache",
        "PrN.json",
        "AUX",
        "nul.bin",
        "COM1",
        "com9.cache",
        "LPT1",
        "lPt9.log"
    )) {
    $AliasedSegmentWasRejected = $false
    try {
        $null = Get-BuildPlatformSafeSegment -Value $AliasedSegment
    } catch {
        $AliasedSegmentWasRejected = $true
    }
    if (-not $AliasedSegmentWasRejected) {
        throw "Get-BuildPlatformSafeSegment accepted Windows alias '$AliasedSegment'."
    }
}

$OrdinarySanitizedSegment = Get-BuildPlatformSafeSegment -Value "preview/profile"
if ($OrdinarySanitizedSegment -cne "preview_profile") {
    throw "Ordinary invalid-character sanitization changed to '$OrdinarySanitizedSegment'."
}
foreach ($OrdinarySegment in @("console", "COM10", "LPT0")) {
    if ((Get-BuildPlatformSafeSegment -Value $OrdinarySegment) -cne $OrdinarySegment) {
        throw "Ordinary segment '$OrdinarySegment' was rejected or changed."
    }
}

$MixedCaseProjectRootPath = Join-Path $TestRootPath "MixedCaseProject"
$LowerCaseProjectHash = Get-BuildPlatformProjectHash -ProjectRootPath $MixedCaseProjectRootPath.ToLowerInvariant()
$UpperCaseProjectHash = Get-BuildPlatformProjectHash -ProjectRootPath $MixedCaseProjectRootPath.ToUpperInvariant()
if ($LowerCaseProjectHash -cne $UpperCaseProjectHash) {
    throw "Windows project hash identity changed with caller casing: '$LowerCaseProjectHash' and '$UpperCaseProjectHash'."
}

try {
    $null = New-Item -ItemType Directory -Path $FakeToolsPath -Force
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $ProjectPath) -Force
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $EditorProjectPath) -Force
    Set-Content -LiteralPath $ProjectPath -Value "{}" -NoNewline
    Set-Content -LiteralPath $EditorProjectPath -Value "<Project />" -NoNewline
    Set-Content -LiteralPath $EnvironmentEnumerationGuardPath -Value @'
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$WrapperPath,
    [Parameter(Mandatory = $true)] [string]$Project,
    [Parameter(Mandatory = $true)] [string]$Platform,
    [Parameter(Mandatory = $true)] [string]$Output,
    [Parameter(Mandatory = $true)] [string]$Configuration,
    [Parameter(Mandatory = $true)] [string]$BuildProfile,
    [Parameter(Mandatory = $true)] [string]$EditorProject,
    [Parameter(Mandatory = $true)] [string]$CacheRoot
)

function Get-ChildItem {
    param([Parameter(Position = 0)] [object]$Path)

    if ([string]$Path -eq "Env:") {
        throw "ENV_PROVIDER_ENUMERATION_ATTEMPTED"
    }
    return Microsoft.PowerShell.Management\Get-ChildItem -Path $Path
}

& $WrapperPath `
    -Project $Project `
    -Platform $Platform `
    -Output $Output `
    -Configuration $Configuration `
    -BuildProfile $BuildProfile `
    -EditorProject $EditorProject `
    -CacheRoot $CacheRoot
'@ -NoNewline
    Set-Content -LiteralPath (Join-Path $FakeToolsPath "dotnet.cmd") -Value @'
@echo off
setlocal EnableExtensions
echo %*>> "%HELENGINE_WORKSPACE_CAPTURE%"
echo %* | findstr /C:"--build windows" >nul
if not errorlevel 1 (
    > "%HELENGINE_WORKSPACE_ENVIRONMENT_CAPTURE%" (
        echo cache=%HELENGINE_BUILD_CACHE_ROOT%
        echo configuration=%HELENGINE_BUILD_CONFIGURATION%
        echo profile=%HELENGINE_BUILD_PROFILE%
    )
)
set "OutputPath="
:FindOutputPath
if "%~1"=="" goto CreatePublishOutput
if /I "%~1"=="-o" set "OutputPath=%~2"
shift
goto FindOutputPath
:CreatePublishOutput
if not "%OutputPath%"=="" (
    if not exist "%OutputPath%" mkdir "%OutputPath%"
    type nul > "%OutputPath%\helengine.editor.app.dll"
)
exit /b 0
'@ -NoNewline
    Set-Content -LiteralPath (Join-Path $FakeToolsPath "robocopy.cmd") -Value @'
@echo off
echo invoked> "%HELENGINE_WORKSPACE_ROBOCOPY_MARKER%"
exit /b 8
'@ -NoNewline

    $OriginalPath = $env:PATH
    $OriginalCapturePath = $env:HELENGINE_WORKSPACE_CAPTURE
    $OriginalEnvironmentCapturePath = $env:HELENGINE_WORKSPACE_ENVIRONMENT_CAPTURE
    $OriginalRobocopyMarkerPath = $env:HELENGINE_WORKSPACE_ROBOCOPY_MARKER
    $OriginalDotNetExecutablePath = $env:HELENGINE_DOTNET_EXECUTABLE_PATH
    try {
        $env:PATH = $FakeToolsPath + ";" + $OriginalPath
        $env:HELENGINE_WORKSPACE_CAPTURE = $CapturePath
        $env:HELENGINE_WORKSPACE_ENVIRONMENT_CAPTURE = $EnvironmentCapturePath
        $env:HELENGINE_WORKSPACE_ROBOCOPY_MARKER = $RobocopyMarkerPath
        $env:HELENGINE_DOTNET_EXECUTABLE_PATH = Join-Path $FakeToolsPath "dotnet.cmd"

        $EnumerationGuardOutput = @(& powershell.exe `
            -NoProfile `
            -ExecutionPolicy Bypass `
            -File $EnvironmentEnumerationGuardPath `
            -WrapperPath $WrapperPath `
            -Project $ProjectPath `
            -Platform "windows" `
            -Output $OutputArgumentPath `
            -Configuration "Release" `
            -BuildProfile "profiler" `
            -EditorProject $EditorProjectPath `
            -CacheRoot $CacheRootPath 2>&1)
        $EnumerationGuardExitCode = $LASTEXITCODE
        if ($EnumerationGuardExitCode -ne 0) {
            throw "The wrapper failed when environment provider enumeration was unavailable (exit $EnumerationGuardExitCode): $($EnumerationGuardOutput -join [Environment]::NewLine)"
        }
        Clear-Content -LiteralPath $CapturePath

        $WorkspaceOnlyResult = Invoke-ControlledWrapper -CachePath "" -WorkspacePath $CacheRootPath
        if (Test-Path -LiteralPath $RobocopyMarkerPath) {
            throw "The wrapper invoked robocopy."
        }
        Assert-Success -Result $WorkspaceOnlyResult -CaseName "The deprecated WorkspaceRoot-only invocation"
        $DeprecationWarnings = @($WorkspaceOnlyResult.Output |
            Where-Object { $_ -match 'WorkspaceRoot is deprecated; use CacheRoot\.' })
        if ($DeprecationWarnings.Count -ne 1) {
            throw "WorkspaceRoot alone must emit one deprecation warning; captured $($DeprecationWarnings.Count)."
        }
        $WorkspaceOnlyPublishInvocation = Get-Content -LiteralPath $CapturePath |
            Where-Object { $_ -match '^publish ' } |
            Select-Object -Last 1
        Clear-Content -LiteralPath $CapturePath

        $FirstResult = Invoke-ControlledWrapper -CachePath $CacheRootPath
        Assert-Success -Result $FirstResult -CaseName "The first stable-cache invocation"
        $SecondResult = Invoke-ControlledWrapper -CachePath $CacheRootPath
        Assert-Success -Result $SecondResult -CaseName "The second stable-cache invocation"

        $CanonicalProjectPath = [System.IO.Path]::GetFullPath($ProjectPath)
        $CanonicalProjectRootPath = Split-Path -Parent $CanonicalProjectPath
        $CanonicalOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
        $CanonicalCacheRootPath = [System.IO.Path]::GetFullPath($CacheRootPath)
        $ProjectHash = Get-ExpectedProjectHash -ProjectRootPath $CanonicalProjectRootPath
        $ProjectCacheRootPath = Join-Path $CanonicalCacheRootPath ("projects\" + $ProjectHash)
        $ExpectedEditorCachePath = Join-Path $ProjectCacheRootPath "editor\release"
        $ExpectedEditorArtifactsPath = Join-Path $ExpectedEditorCachePath "artifacts"
        $ExpectedEditorPublishPath = Join-Path $ExpectedEditorCachePath "publish"
        $ExpectedPlatformCachePath = Join-Path $ProjectCacheRootPath "platforms\windows\release\profiler"
        $ExpectedLockPath = Join-Path $CanonicalCacheRootPath ("locks\" + $ProjectHash + ".lock")
        $ExpectedMetadataPath = Join-Path $ProjectCacheRootPath "cache-metadata.json"
        $ExpectedStatePath = Join-Path $CanonicalOutputPath ".helengine-build-state.json"

        $InitialInvocations = @(Get-Content -LiteralPath $CapturePath)
        $CapturedEditorEnvironment = @(Get-Content -LiteralPath $EnvironmentCapturePath)
        foreach ($ExpectedEnvironmentValue in @(
                "cache=$CanonicalCacheRootPath",
                "configuration=release",
                "profile=profiler"
            )) {
            if ($CapturedEditorEnvironment -notcontains $ExpectedEnvironmentValue) {
                throw "The fake editor environment did not contain '$ExpectedEnvironmentValue': '$($CapturedEditorEnvironment -join " | ")'."
            }
        }
        $EditorInvocations = @($InitialInvocations | Where-Object { $_ -match '--build windows' })
        if ($EditorInvocations.Count -ne 2) {
            throw "Expected two editor invocations, captured $($EditorInvocations.Count): '$($InitialInvocations -join " | ")'."
        }
        foreach ($EditorInvocation in $EditorInvocations) {
            if ($EditorInvocation -notmatch [regex]::Escape('--project ' + $CanonicalProjectPath)) {
                throw "The wrapper did not pass the authored project directly: '$EditorInvocation'."
            }
            if ((Get-CapturedArgumentValue -Invocation $EditorInvocation -ArgumentName "--output") -cne $CanonicalOutputPath) {
                throw "The wrapper did not pass the exact canonical output path: '$EditorInvocation'."
            }
        }

        $GuidLikeInvocationDirectories = @(Get-ChildItem -LiteralPath $CacheRootPath -Recurse -Directory |
            Where-Object { $_.Name -match '^[0-9a-f]{32}$' -and $_.Parent.Name -ne 'projects' })
        if ($GuidLikeInvocationDirectories.Count -ne 0) {
            throw "The cache contains a GUID-like invocation directory: '$($GuidLikeInvocationDirectories.FullName -join "', '")'."
        }

        $PublishInvocations = @($InitialInvocations | Where-Object { $_ -match '^publish ' })
        if ($PublishInvocations.Count -ne 2) {
            throw "Expected two publish invocations, captured $($PublishInvocations.Count)."
        }
        $FirstArtifactsPath = Get-CapturedArgumentValue -Invocation $PublishInvocations[0] -ArgumentName "--artifacts-path"
        $SecondArtifactsPath = Get-CapturedArgumentValue -Invocation $PublishInvocations[1] -ArgumentName "--artifacts-path"
        $FirstPublishPath = Get-CapturedArgumentValue -Invocation $PublishInvocations[0] -ArgumentName "-o"
        $SecondPublishPath = Get-CapturedArgumentValue -Invocation $PublishInvocations[1] -ArgumentName "-o"
        if ($FirstArtifactsPath -cne $SecondArtifactsPath -or $FirstArtifactsPath -cne $ExpectedEditorArtifactsPath) {
            throw "Editor artifacts paths were not stable and canonical: '$FirstArtifactsPath' and '$SecondArtifactsPath'."
        }
        if ($FirstPublishPath -cne $SecondPublishPath -or $FirstPublishPath -cne $ExpectedEditorPublishPath) {
            throw "Editor publish paths were not stable and canonical: '$FirstPublishPath' and '$SecondPublishPath'."
        }

        foreach ($ExpectedDiagnostic in @(
                "Authored project: $CanonicalProjectPath",
                "Lock: $ExpectedLockPath",
                "Editor cache: $ExpectedEditorCachePath",
                "Platform cache: $ExpectedPlatformCachePath",
                "Output: $CanonicalOutputPath",
                "State file: $ExpectedStatePath"
            )) {
            if ($FirstResult.Output -notcontains $ExpectedDiagnostic) {
                throw "Normal output did not contain '$ExpectedDiagnostic'. Output: '$($FirstResult.Output -join " | ")'."
            }
        }

        if (-not (Test-Path -LiteralPath $ExpectedMetadataPath -PathType Leaf)) {
            throw "Cache metadata was not written to '$ExpectedMetadataPath'."
        }
        $Metadata = Get-Content -LiteralPath $ExpectedMetadataPath -Raw | ConvertFrom-Json
        if ($Metadata.projectRootPath -cne $CanonicalProjectRootPath) {
            throw "Cache metadata recorded project root '$($Metadata.projectRootPath)' instead of '$CanonicalProjectRootPath'."
        }
        $null = [DateTimeOffset]::Parse($Metadata.lastUsedUtc)

        if (Test-Path -LiteralPath $ExpectedLockPath) {
            throw "The wrapper materialized the later-task lock path '$ExpectedLockPath'."
        }
        if (Test-Path -LiteralPath $ExpectedStatePath) {
            throw "The wrapper materialized the later-task state path '$ExpectedStatePath'."
        }

        if ((Get-CapturedArgumentValue -Invocation $WorkspaceOnlyPublishInvocation -ArgumentName "-o") -cne $ExpectedEditorPublishPath) {
            throw "WorkspaceRoot alone did not resolve the CacheRoot layout."
        }

        $EqualRootsResult = Invoke-ControlledWrapper -CachePath $CacheRootPath -WorkspacePath $EquivalentWorkspaceRootPath
        Assert-Success -Result $EqualRootsResult -CaseName "The canonically equal CacheRoot/WorkspaceRoot invocation"

        $InvocationCountBeforeConflict = @(Get-Content -LiteralPath $CapturePath).Count
        $ConflictingRootsResult = Invoke-ControlledWrapper -CachePath $CacheRootPath -WorkspacePath $DifferentWorkspaceRootPath
        if ($ConflictingRootsResult.ExitCode -ne 2) {
            throw "Differing CacheRoot/WorkspaceRoot values must exit 2, got $($ConflictingRootsResult.ExitCode)."
        }
        if (@(Get-Content -LiteralPath $CapturePath).Count -ne $InvocationCountBeforeConflict) {
            throw "Differing CacheRoot/WorkspaceRoot values reached dotnet."
        }

        $NegativePruneResult = Invoke-ControlledWrapper -CachePath $CacheRootPath -PruneDays -1
        if ($NegativePruneResult.ExitCode -ne 2) {
            throw "A negative PruneCacheOlderThanDays value must exit 2, got $($NegativePruneResult.ExitCode)."
        }
        if (@(Get-Content -LiteralPath $CapturePath).Count -ne $InvocationCountBeforeConflict) {
            throw "A negative PruneCacheOlderThanDays value reached dotnet."
        }
        if (Test-Path -LiteralPath $RobocopyMarkerPath) {
            throw "The wrapper invoked robocopy."
        }
        if ($InitialInvocations.Count -ne 6) {
            throw "Expected six native invocations for the repeated stable-cache cases, captured $($InitialInvocations.Count)."
        }
    } finally {
        $env:PATH = $OriginalPath
        $env:HELENGINE_WORKSPACE_CAPTURE = $OriginalCapturePath
        $env:HELENGINE_WORKSPACE_ENVIRONMENT_CAPTURE = $OriginalEnvironmentCapturePath
        $env:HELENGINE_WORKSPACE_ROBOCOPY_MARKER = $OriginalRobocopyMarkerPath
        $env:HELENGINE_DOTNET_EXECUTABLE_PATH = $OriginalDotNetExecutablePath
    }

    Write-Output "WORKSPACE_TEST_PASS"
} finally {
    if (Test-Path -LiteralPath $TestRootPath) {
        Remove-Item -LiteralPath $TestRootPath -Recurse -Force
    }
}

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
    [string]$BuildProfile = "",

    [Parameter()]
    [string]$EditorProject = "",

    [Parameter()]
    [string]$CacheRoot = "",

    [Parameter()]
    [string]$WorkspaceRoot = "",

    [Parameter()]
    [TimeSpan]$LockTimeout = [TimeSpan]::FromHours(2),

    [Parameter()]
    [switch]$Clean,

    [Parameter()]
    [int]$PruneCacheOlderThanDays = 0,

    [Parameter()]
    [string[]]$AdditionalArgs = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Import-Module (Join-Path $PSScriptRoot "build-platform\BuildPlatformCache.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "build-platform\BuildPlatformEnvironment.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "build-platform\BuildPlatformLock.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "build-platform\BuildPlatformProcess.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "build-platform\BuildPlatformState.psm1") -Force

function Get-CanonicalDirectoryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $FullPath = [System.IO.Path]::GetFullPath($Path)
    $RootPath = [System.IO.Path]::GetPathRoot($FullPath)
    if ($FullPath.Length -le $RootPath.Length) {
        return $RootPath
    }

    $DirectorySeparators = [char[]]@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar
    )
    return $FullPath.TrimEnd($DirectorySeparators)
}

function Test-BuildPlatformPathContains {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ParentPath,

        [Parameter(Mandatory = $true)]
        [string]$CandidatePath
    )

    $Parent = Get-CanonicalDirectoryPath -Path $ParentPath
    $Candidate = Get-CanonicalDirectoryPath -Path $CandidatePath
    $Prefix = $Parent
    if (-not $Prefix.EndsWith([IO.Path]::DirectorySeparatorChar)) {
        $Prefix += [IO.Path]::DirectorySeparatorChar
    }
    return $Candidate.StartsWith($Prefix, [StringComparison]::OrdinalIgnoreCase)
}

function Test-BuildPlatformPathOverlap {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FirstPath,

        [Parameter(Mandatory = $true)]
        [string]$SecondPath
    )

    $First = Get-CanonicalDirectoryPath -Path $FirstPath
    $Second = Get-CanonicalDirectoryPath -Path $SecondPath
    return $First.Equals($Second, [StringComparison]::OrdinalIgnoreCase) -or
        (Test-BuildPlatformPathContains -ParentPath $First -CandidatePath $Second) -or
        (Test-BuildPlatformPathContains -ParentPath $Second -CandidatePath $First)
}

function Assert-BuildPlatformAdditionalArguments {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]]$Arguments
    )

    $ReservedSwitches = @(
        "--project",
        "--build",
        "--build-profile",
        "--output"
    )
    foreach ($Argument in $Arguments) {
        if ($null -eq $Argument) {
            continue
        }

        foreach ($ReservedSwitch in $ReservedSwitches) {
            if ($Argument.Equals($ReservedSwitch, [StringComparison]::OrdinalIgnoreCase) -or
                $Argument.StartsWith($ReservedSwitch + "=", [StringComparison]::OrdinalIgnoreCase)) {
                throw "AdditionalArgs cannot override wrapper-owned switch '$Argument'."
            }
        }
    }
}

function Get-RemainingBuildPlatformLockTimeout {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Stopwatch]$Stopwatch,

        [Parameter(Mandatory = $true)]
        [TimeSpan]$Timeout
    )

    $RemainingTimeout = $Timeout - $Stopwatch.Elapsed
    if ($RemainingTimeout -lt [TimeSpan]::Zero) {
        return [TimeSpan]::Zero
    }
    return $RemainingTimeout
}

function Get-EditorArtifactsOutputPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$EditorArtifactsPath,

        [Parameter(Mandatory = $true)]
        [string]$Configuration
    )

    if ([string]::IsNullOrWhiteSpace($EditorArtifactsPath)) {
        throw "Editor artifacts path must be provided."
    } elseif ([string]::IsNullOrWhiteSpace($Configuration)) {
        throw "Configuration must be provided."
    }

    return Join-Path $EditorArtifactsPath ("bin\helengine.editor.app\" + $Configuration.ToLowerInvariant())
}

function Sync-EditorProjectReferenceOutputs {
    param(
        [Parameter(Mandatory = $true)]
        [string]$EditorArtifactsPath,

        [Parameter(Mandatory = $true)]
        [string]$Configuration
    )

    if ([string]::IsNullOrWhiteSpace($EditorArtifactsPath)) {
        throw "Editor artifacts path must be provided."
    } elseif ([string]::IsNullOrWhiteSpace($Configuration)) {
        throw "Configuration must be provided."
    }

    $EditorOutputPath = Get-EditorArtifactsOutputPath -EditorArtifactsPath $EditorArtifactsPath -Configuration $Configuration
    if (-not (Test-Path -LiteralPath $EditorOutputPath -PathType Container)) {
        throw "Editor app output was not found at '$EditorOutputPath'."
    }

    $ArtifactsBinRootPath = Join-Path $EditorArtifactsPath "bin"
    if (-not (Test-Path -LiteralPath $ArtifactsBinRootPath -PathType Container)) {
        throw "Editor artifacts bin root was not found at '$ArtifactsBinRootPath'."
    }

    $ProjectOutputDirectories = Get-ChildItem -LiteralPath $ArtifactsBinRootPath -Directory |
        Where-Object { $_.Name -ne "helengine.editor.app" }
    foreach ($ProjectOutputDirectory in $ProjectOutputDirectories) {
        $ProjectOutputPath = Join-Path $ProjectOutputDirectory.FullName $Configuration.ToLowerInvariant()
        if (-not (Test-Path -LiteralPath $ProjectOutputPath -PathType Container)) {
            continue
        }

        $OutputFiles = Get-ChildItem -LiteralPath $ProjectOutputPath -File
        foreach ($OutputFile in $OutputFiles) {
            Copy-Item -LiteralPath $OutputFile.FullName -Destination (Join-Path $EditorOutputPath $OutputFile.Name) -Force
        }
    }

    return $EditorOutputPath
}

if ([string]::IsNullOrWhiteSpace($Project)) { [Console]::Error.WriteLine("Project is required."); exit 2 }
if ([string]::IsNullOrWhiteSpace($Platform)) { [Console]::Error.WriteLine("Platform is required."); exit 2 }
if ([string]::IsNullOrWhiteSpace($Output)) { [Console]::Error.WriteLine("Output is required."); exit 2 }
if ([string]::IsNullOrWhiteSpace($Configuration)) { [Console]::Error.WriteLine("Configuration is required."); exit 2 }
if ($PruneCacheOlderThanDays -lt 0) { [Console]::Error.WriteLine("PruneCacheOlderThanDays must be zero or positive."); exit 2 }

if (-not [string]::IsNullOrWhiteSpace($CacheRoot) -and
    -not [string]::IsNullOrWhiteSpace($WorkspaceRoot) -and
    (Get-CanonicalDirectoryPath -Path $CacheRoot) -ine (Get-CanonicalDirectoryPath -Path $WorkspaceRoot)) {
    [Console]::Error.WriteLine("CacheRoot and deprecated WorkspaceRoot must resolve to the same path when both are supplied.")
    exit 2
}

$SelectedCacheRoot = if (-not [string]::IsNullOrWhiteSpace($CacheRoot)) {
    $CacheRoot
} elseif (-not [string]::IsNullOrWhiteSpace($WorkspaceRoot)) {
    Write-Warning "WorkspaceRoot is deprecated; use CacheRoot."
    $WorkspaceRoot
} else {
    "C:\dev\helworks\builds\helengine\cache"
}

if (-not [string]::IsNullOrWhiteSpace($BuildProfile)) {
    $ResolvedBuildProfile = $BuildProfile
} elseif ($Configuration -ieq "Debug" -or $Configuration -ieq "Release") {
    $ResolvedBuildProfile = $Configuration.ToLowerInvariant()
} else {
    [Console]::Error.WriteLine("BuildProfile is required when Configuration is not Debug or Release.")
    exit 2
}

$DotNetExecutablePath = $env:HELENGINE_DOTNET_EXECUTABLE_PATH
if ([string]::IsNullOrWhiteSpace($DotNetExecutablePath)) {
    $DotNetExecutablePath = "dotnet"
}

$InvocationBuildId = $env:HELENGINE_BUILD_INVOCATION_ID
if ([string]::IsNullOrWhiteSpace($InvocationBuildId)) {
    $InvocationBuildId = [Guid]::NewGuid().ToString("D")
} else {
    $ParsedInvocationBuildId = [Guid]::Empty
    if (-not [Guid]::TryParseExact($InvocationBuildId, "D", [ref]$ParsedInvocationBuildId)) {
        [Console]::Error.WriteLine("HELENGINE_BUILD_INVOCATION_ID must be a canonical GUID in D format.")
        exit 2
    }
    $InvocationBuildId = $ParsedInvocationBuildId.ToString("D")
}

$OriginalBuildPlatformEnvironmentState = Save-BuildPlatformEnvironmentState
$ProjectMutex = $null
$OutputMutex = $null
$ProjectLock = $null
$BuildStateStarted = $false
$BuildId = $null
$BuildStartedUtc = $null
$BuildTerminalStatus = "failed"
$BuildTerminalExitCode = 10
$StateFilePath = $null
$Layout = $null
$CacheMetadataStarted = $false

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
    $ResolvedHelEngineRootPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
    $ResolvedOutputPath = Get-CanonicalDirectoryPath -Path $Output
    $MaintenanceProtectedPaths = @($ResolvedProjectRootPath, $ResolvedOutputPath)

    $Layout = Resolve-BuildPlatformCacheLayout `
        -CacheRootPath $SelectedCacheRoot `
        -ProjectRootPath $ResolvedProjectRootPath `
        -EditorProjectPath $ResolvedEditorProject `
        -Platform $Platform `
        -Configuration $Configuration `
        -BuildProfile $ResolvedBuildProfile

    if (Test-BuildPlatformPathOverlap -FirstPath $ResolvedOutputPath -SecondPath $Layout.ProjectCacheRootPath) {
        [Console]::Error.WriteLine(
            "Output path '$ResolvedOutputPath' must not overlap project cache '$($Layout.ProjectCacheRootPath)'."
        )
        exit 2
    }
    try {
        Assert-BuildPlatformAdditionalArguments -Arguments $AdditionalArgs
    } catch {
        [Console]::Error.WriteLine($_.Exception.Message)
        exit 2
    }

    $LockMetadata = [ordered]@{
        processId = $PID
        projectPath = $ResolvedProjectPath
        platform = $Platform
        profile = $ResolvedBuildProfile
        output = $ResolvedOutputPath
        startedUtc = [DateTime]::UtcNow.ToString("o")
    }
    $LockWaitStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $ProjectMutex = Enter-BuildPlatformProjectMutex `
        -ProjectHash $Layout.ProjectHash `
        -ProjectPath $ResolvedProjectPath `
        -Timeout $LockTimeout
    $OutputHash = Get-BuildPlatformPathHash -Path $ResolvedOutputPath
    $OutputMutex = Enter-BuildPlatformOutputMutex `
        -OutputHash $OutputHash `
        -OutputPath $ResolvedOutputPath `
        -Timeout (Get-RemainingBuildPlatformLockTimeout `
            -Stopwatch $LockWaitStopwatch `
            -Timeout $LockTimeout)
    $ProjectLock = Enter-BuildPlatformProjectLock `
        -LockPath $Layout.LockPath `
        -Metadata $LockMetadata `
        -Timeout (Get-RemainingBuildPlatformLockTimeout `
            -Stopwatch $LockWaitStopwatch `
            -Timeout $LockTimeout)

    if ($Clean) {
        Remove-BuildPlatformSelectedCache `
            -Layout $Layout `
            -ProtectedPath $MaintenanceProtectedPaths
    }
    if ($PruneCacheOlderThanDays -gt 0) {
        Remove-BuildPlatformExpiredProjectCaches `
            -CacheRootPath $Layout.CacheRootPath `
            -OlderThanDays $PruneCacheOlderThanDays `
            -ProtectedPath $MaintenanceProtectedPaths
    }
    Write-BuildPlatformCacheMetadata -Layout $Layout -ProjectRootPath $ResolvedProjectRootPath
    $CacheMetadataStarted = $true

    if (-not (Test-Path -LiteralPath $ResolvedOutputPath -PathType Container)) {
        $null = New-Item -ItemType Directory -Path $ResolvedOutputPath -Force
    }
    $StateFilePath = Join-Path $ResolvedOutputPath ".helengine-build-state.json"
    $BuildId = $InvocationBuildId
    $BuildStartedUtc = [DateTime]::UtcNow.ToString("o")
    Write-BuildPlatformState `
        -StatePath $StateFilePath `
        -BuildId $BuildId `
        -ProjectPath $ResolvedProjectPath `
        -Platform $Platform `
        -BuildProfile $ResolvedBuildProfile `
        -Configuration $Configuration `
        -StartedUtc $BuildStartedUtc `
        -CompletedUtc $null `
        -Status "running" `
        -ExitCode $null
    $BuildStateStarted = $true

    $EditorArtifactsPath = $Layout.EditorArtifactsPath
    $EditorPublishPath = $Layout.EditorPublishPath
    $EditorCachePath = Split-Path -Parent $EditorArtifactsPath
    Write-Host "Authored project: $ResolvedProjectPath"
    Write-Host "Lock: $($Layout.LockPath)"
    Write-Host "Editor cache: $EditorCachePath"
    Write-Host "Platform cache: $($Layout.PlatformCacheRootPath)"
    Write-Host "Output: $ResolvedOutputPath"
    Write-Host "State file: $StateFilePath"

    [Environment]::SetEnvironmentVariable("HELENGINE_BUILD_CACHE_ROOT", $Layout.CacheRootPath, [EnvironmentVariableTarget]::Process)
    [Environment]::SetEnvironmentVariable("HELENGINE_BUILD_CONFIGURATION", $Configuration.ToLowerInvariant(), [EnvironmentVariableTarget]::Process)
    [Environment]::SetEnvironmentVariable("HELENGINE_BUILD_PROFILE", $ResolvedBuildProfile, [EnvironmentVariableTarget]::Process)

    $DotNetSharedPropertyArguments = @(
        "--artifacts-path",
        $EditorArtifactsPath
    )

    $DotNetRestoreArguments = @(
        "restore",
        $ResolvedEditorProject
    ) + $DotNetSharedPropertyArguments

    $DotNetPublishArguments = @(
        "publish",
        $ResolvedEditorProject,
        "--no-restore",
        "-c",
        $Configuration,
        "-o",
        $EditorPublishPath
    ) + $DotNetSharedPropertyArguments

    $EditorRunArguments = @(
        "--project",
        $ResolvedProjectPath,
        "--build",
        $Platform
    )

    $EditorRunArguments += @(
        "--build-profile",
        $ResolvedBuildProfile
    )

    $EditorRunArguments += @(
        "--output",
        $ResolvedOutputPath
    )

    if ($AdditionalArgs.Count -gt 0) {
        $EditorRunArguments += $AdditionalArgs
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

    $DotNetRestoreExitCode = Invoke-StreamingNativeProcess -FilePath $DotNetExecutablePath -ArgumentList $DotNetRestoreArguments
    if ($DotNetRestoreExitCode -ne 0) {
        [Console]::Error.WriteLine("Editor project restore failed with exit code $DotNetRestoreExitCode.")
        $BuildTerminalExitCode = $DotNetRestoreExitCode
        exit $DotNetRestoreExitCode
    }

    $BuildDisplayArguments = @("dotnet")
    foreach ($Argument in $DotNetPublishArguments) {
        if ($Argument -match '[\s"]') {
            $BuildDisplayArguments += '"' + $Argument.Replace('"', '\"') + '"'
        } else {
            $BuildDisplayArguments += $Argument
        }
    }

    Write-Host ("Publishing: " + ($BuildDisplayArguments -join " "))

    $DotNetBuildExitCode = Invoke-StreamingNativeProcess -FilePath $DotNetExecutablePath -ArgumentList $DotNetPublishArguments
    if ($DotNetBuildExitCode -ne 0) {
        [Console]::Error.WriteLine("Editor project publish failed with exit code $DotNetBuildExitCode.")
        $BuildTerminalExitCode = $DotNetBuildExitCode
        exit $DotNetBuildExitCode
    }

    $EditorAssemblyPath = Join-Path $EditorPublishPath "helengine.editor.app.dll"
    if (-not (Test-Path -LiteralPath $EditorAssemblyPath -PathType Leaf)) {
        [Console]::Error.WriteLine("Editor app assembly was not found at '$EditorAssemblyPath'.")
        $BuildTerminalExitCode = 5
        exit 5
    }

    $DisplayArguments = @("dotnet", $EditorAssemblyPath)
    foreach ($Argument in $EditorRunArguments) {
        if ($Argument -match '[\s"]') {
            $DisplayArguments += '"' + $Argument.Replace('"', '\"') + '"'
        } else {
            $DisplayArguments += $Argument
        }
    }

    Write-Host ("Executing: " + ($DisplayArguments -join " "))

    [Environment]::SetEnvironmentVariable("HELENGINE_SOURCE_ROOT", $ResolvedHelEngineRootPath, [EnvironmentVariableTarget]::Process)

    $DotNetExitCode = Invoke-StreamingNativeProcess -FilePath $DotNetExecutablePath -ArgumentList (@($EditorAssemblyPath) + $EditorRunArguments)
    if ($DotNetExitCode -ne 0) {
        [Console]::Error.WriteLine("Editor platform build failed with exit code $DotNetExitCode.")
        $BuildTerminalExitCode = $DotNetExitCode
        exit $DotNetExitCode
    }

    $BuildTerminalStatus = "succeeded"
    $BuildTerminalExitCode = 0
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    $BuildTerminalStatus = "failed"
    $BuildTerminalExitCode = 10
    exit 10
} finally {
    $TerminalStateWriteFailed = $false
    try {
        try {
            if ($BuildStateStarted) {
                try {
                    Write-BuildPlatformState `
                        -StatePath $StateFilePath `
                        -BuildId $BuildId `
                        -ProjectPath $ResolvedProjectPath `
                        -Platform $Platform `
                        -BuildProfile $ResolvedBuildProfile `
                        -Configuration $Configuration `
                        -StartedUtc $BuildStartedUtc `
                        -CompletedUtc ([DateTime]::UtcNow.ToString("o")) `
                        -Status $BuildTerminalStatus `
                        -ExitCode $BuildTerminalExitCode
                } catch {
                    $TerminalStateWriteFailed = $true
                    [Console]::Error.WriteLine(
                        "Failed to write terminal build state '$StateFilePath': $($_.Exception.Message)"
                    )
                    if ($BuildTerminalExitCode -eq 0) {
                        $BuildTerminalExitCode = 10
                    }
                }
            }
        } finally {
            try {
                if ($CacheMetadataStarted) {
                    try {
                        Write-BuildPlatformCacheMetadata `
                            -Layout $Layout `
                            -ProjectRootPath $ResolvedProjectRootPath
                    } catch {
                        [Console]::Error.WriteLine(
                            "Failed to write terminal cache metadata '$($Layout.MetadataPath)': $($_.Exception.Message)"
                        )
                    }
                }
            } finally {
                try {
                    if ($null -ne $ProjectLock) {
                        Exit-BuildPlatformProjectLock -LockHandle $ProjectLock
                    }
                } finally {
                    if ($null -ne $OutputMutex) {
                        Exit-BuildPlatformOutputMutex -MutexHandle $OutputMutex
                    }
                }
            }
        }
    } finally {
        try {
            if ($null -ne $ProjectMutex) {
                Exit-BuildPlatformProjectMutex -MutexHandle $ProjectMutex
            }
        } finally {
            Restore-BuildPlatformEnvironmentState -State $OriginalBuildPlatformEnvironmentState
        }
    }
    if ($TerminalStateWriteFailed) {
        exit $BuildTerminalExitCode
    }
}

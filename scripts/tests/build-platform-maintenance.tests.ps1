[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$WrapperPath = Join-Path $RepositoryRoot "scripts\build-platform.ps1"
$CacheModulePath = Join-Path $RepositoryRoot "scripts\build-platform\BuildPlatformCache.psm1"
$LockModulePath = Join-Path $RepositoryRoot "scripts\build-platform\BuildPlatformLock.psm1"
Import-Module $CacheModulePath -Force
Import-Module $LockModulePath -Force

foreach ($RequiredCommand in @(
        "Remove-BuildPlatformSelectedCache",
        "Remove-BuildPlatformExpiredProjectCaches"
    )) {
    if ($null -eq (Get-Command $RequiredCommand -ErrorAction SilentlyContinue)) {
        throw "Required exported maintenance command '$RequiredCommand' was not found."
    }
}

$TestRootPath = Join-Path ([System.IO.Path]::GetTempPath()) (
    "helengine-build-platform-maintenance-" + [Guid]::NewGuid().ToString("N")
)
$CacheRootPath = Join-Path $TestRootPath "cache"
$SourceRootPath = Join-Path $TestRootPath "sources"
$OutputRootPath = Join-Path $TestRootPath "outputs"
$FakeToolsPath = Join-Path $TestRootPath "fake-tools"
$EditorProjectPath = Join-Path $TestRootPath "editor\editor.csproj"
$ProjectAPath = Join-Path $SourceRootPath "project-a\project.heproj"
$ProjectBPath = Join-Path $SourceRootPath "project-b\project.heproj"
$ProjectARootPath = Split-Path -Parent $ProjectAPath
$ProjectBRootPath = Split-Path -Parent $ProjectBPath
$ProjectAOutputPath = Join-Path $OutputRootPath "project-a"
$MetadataRunningCapturePath = Join-Path $TestRootPath "metadata-running.json"
$HeldLock = $null
$JunctionPath = $null
$AllowedRootJunctionPath = $null
$PruneJunctionPath = $null

function Assert-PathExists {
    param([Parameter(Mandatory = $true)][string]$LiteralPath)

    if (-not (Test-Path -LiteralPath $LiteralPath)) {
        throw "Expected path '$LiteralPath' to exist."
    }
}

function Assert-PathMissing {
    param([Parameter(Mandatory = $true)][string]$LiteralPath)

    if (Test-Path -LiteralPath $LiteralPath) {
        throw "Expected path '$LiteralPath' to be absent."
    }
}

function New-Sentinel {
    param([Parameter(Mandatory = $true)][string]$LiteralPath)

    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $LiteralPath) -Force
    Set-Content -LiteralPath $LiteralPath -Value "sentinel" -NoNewline
}

function Assert-Throws {
    param(
        [Parameter(Mandatory = $true)][scriptblock]$Action,
        [Parameter(Mandatory = $true)][string]$CaseName
    )

    $Threw = $false
    try {
        & $Action
    } catch {
        $Threw = $true
    }
    if (-not $Threw) {
        throw "$CaseName did not reject the unsafe operation."
    }
}

function Write-CacheMetadata {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectCacheRootPath,
        [Parameter(Mandatory = $true)][string]$ProjectRootPath,
        [Parameter(Mandatory = $true)][DateTime]$LastUsedUtc
    )

    $null = New-Item -ItemType Directory -Path $ProjectCacheRootPath -Force
    [ordered]@{
        projectRootPath = [System.IO.Path]::GetFullPath($ProjectRootPath)
        lastUsedUtc = $LastUsedUtc.ToUniversalTime().ToString("o")
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ProjectCacheRootPath "cache-metadata.json") -Encoding UTF8
}

function Invoke-Wrapper {
    param(
        [Parameter()][switch]$Clean,
        [Parameter()][int]$PruneDays = 0
    )

    $Arguments = @(
        "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $WrapperPath,
        "-Project", $ProjectAPath,
        "-Platform", "ps2",
        "-Output", $ProjectAOutputPath,
        "-Configuration", "Debug",
        "-BuildProfile", "profiler",
        "-EditorProject", $EditorProjectPath,
        "-CacheRoot", $CacheRootPath
    )
    if ($Clean) {
        $Arguments += "-Clean"
    }
    if ($PruneDays -ne 0) {
        $Arguments += @("-PruneCacheOlderThanDays", $PruneDays)
    }

    $OriginalErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $Output = @(& powershell.exe @Arguments 2>&1)
        $ExitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $OriginalErrorActionPreference
    }
    return [pscustomobject]@{ ExitCode = $ExitCode; Output = $Output }
}

try {
    foreach ($Path in @(
            $ProjectARootPath,
            $ProjectBRootPath,
            (Split-Path -Parent $EditorProjectPath),
            $FakeToolsPath,
            $ProjectAOutputPath
        )) {
        $null = New-Item -ItemType Directory -Path $Path -Force
    }
    Set-Content -LiteralPath $ProjectAPath -Value "{}" -NoNewline
    Set-Content -LiteralPath $ProjectBPath -Value "{}" -NoNewline
    Set-Content -LiteralPath $EditorProjectPath -Value "<Project />" -NoNewline

    $SourceSentinelPath = Join-Path $ProjectARootPath "source-sentinel.txt"
    $OutputSentinelPath = Join-Path $ProjectAOutputPath "output-sentinel.txt"
    New-Sentinel -LiteralPath $SourceSentinelPath
    New-Sentinel -LiteralPath $OutputSentinelPath

    $LayoutA = Resolve-BuildPlatformCacheLayout `
        -CacheRootPath $CacheRootPath `
        -ProjectRootPath $ProjectARootPath `
        -Platform "ps2" `
        -Configuration "Debug" `
        -BuildProfile "profiler"
    $LayoutB = Resolve-BuildPlatformCacheLayout `
        -CacheRootPath $CacheRootPath `
        -ProjectRootPath $ProjectBRootPath `
        -Platform "windows" `
        -Configuration "Release" `
        -BuildProfile "default"

    $SelectedEditorSentinel = Join-Path $LayoutA.EditorArtifactsPath "selected-editor.txt"
    $SelectedPlatformSentinel = Join-Path $LayoutA.PlatformCacheRootPath "selected-platform.txt"
    $OtherConfigurationSentinel = Join-Path $LayoutA.ProjectCacheRootPath "editor\release\other-configuration.txt"
    $OtherProfileSentinel = Join-Path $LayoutA.ProjectCacheRootPath "platforms\ps2\debug\default\other-profile.txt"
    $OtherPlatformSentinel = Join-Path $LayoutA.ProjectCacheRootPath "platforms\windows\debug\profiler\other-platform.txt"
    $OtherProjectSentinel = Join-Path $LayoutB.ProjectCacheRootPath "project-b.txt"
    foreach ($SentinelPath in @(
            $SelectedEditorSentinel,
            $SelectedPlatformSentinel,
            $OtherConfigurationSentinel,
            $OtherProfileSentinel,
            $OtherPlatformSentinel,
            $OtherProjectSentinel
        )) {
        New-Sentinel -LiteralPath $SentinelPath
    }

    Remove-BuildPlatformSelectedCache -Layout $LayoutA
    Assert-PathMissing -LiteralPath $SelectedEditorSentinel
    Assert-PathMissing -LiteralPath $SelectedPlatformSentinel
    foreach ($PreservedPath in @(
            $OtherConfigurationSentinel,
            $OtherProfileSentinel,
            $OtherPlatformSentinel,
            $OtherProjectSentinel,
            $SourceSentinelPath,
            $OutputSentinelPath
        )) {
        Assert-PathExists -LiteralPath $PreservedPath
    }

    $SafeEditorTargetPath = Join-Path $LayoutA.ProjectCacheRootPath "editor\debug"
    $SafePlatformTargetPath = Join-Path $LayoutA.ProjectCacheRootPath "platforms\ps2\debug\profiler"
    foreach ($UnsafeTarget in @(
            $LayoutA.ProjectCacheRootPath,
            (Join-Path $LayoutA.ProjectCacheRootPath "..\outside-via-dotdot"),
            (Join-Path $TestRootPath "outside-rooted")
        )) {
        $UnsafeLayout = [pscustomobject]@{
            ProjectCacheRootPath = $LayoutA.ProjectCacheRootPath
            EditorConfigurationRootPath = $UnsafeTarget
            PlatformCacheRootPath = $SafePlatformTargetPath
        }
        Assert-Throws -CaseName "Unsafe selected-cache target '$UnsafeTarget'" -Action {
            Remove-BuildPlatformSelectedCache -Layout $UnsafeLayout
        }
    }

    $JunctionBackingPath = Join-Path $TestRootPath "junction-backing"
    $JunctionPath = Join-Path $LayoutA.ProjectCacheRootPath "editor\debug"
    $null = New-Item -ItemType Directory -Path $JunctionBackingPath -Force
    New-Sentinel -LiteralPath (Join-Path $JunctionBackingPath "junction-sentinel.txt")
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $JunctionPath) -Force
    $JunctionOutput = & cmd.exe /d /c mklink /J $JunctionPath $JunctionBackingPath 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create disposable junction. $($JunctionOutput -join ' ')"
    }
    $JunctionLayout = [pscustomobject]@{
        ProjectCacheRootPath = $LayoutA.ProjectCacheRootPath
        EditorConfigurationRootPath = $JunctionPath
        PlatformCacheRootPath = $SafePlatformTargetPath
    }
    Assert-Throws -CaseName "Reparse-point selected cache" -Action {
        Remove-BuildPlatformSelectedCache -Layout $JunctionLayout
    }
    Assert-PathExists -LiteralPath (Join-Path $JunctionBackingPath "junction-sentinel.txt")
    [System.IO.Directory]::Delete($JunctionPath, $false)
    $JunctionPath = $null

    $AllowedRootBackingPath = Join-Path $TestRootPath "allowed-root-backing"
    $AllowedRootJunctionPath = Join-Path $TestRootPath "allowed-root-junction"
    $AllowedRootTargetPath = Join-Path $AllowedRootJunctionPath "editor\debug"
    New-Sentinel -LiteralPath (Join-Path $AllowedRootBackingPath "editor\debug\allowed-root-sentinel.txt")
    $JunctionOutput = & cmd.exe /d /c mklink /J $AllowedRootJunctionPath $AllowedRootBackingPath 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create disposable allowed-root junction. $($JunctionOutput -join ' ')"
    }
    $AllowedRootJunctionLayout = [pscustomobject]@{
        ProjectCacheRootPath = $AllowedRootJunctionPath
        EditorConfigurationRootPath = $AllowedRootTargetPath
        PlatformCacheRootPath = (Join-Path $AllowedRootJunctionPath "platforms\ps2\debug\profiler")
    }
    Assert-Throws -CaseName "Reparse-point allowed root" -Action {
        Remove-BuildPlatformSelectedCache -Layout $AllowedRootJunctionLayout
    }
    Assert-PathExists -LiteralPath (Join-Path $AllowedRootBackingPath "editor\debug\allowed-root-sentinel.txt")
    [System.IO.Directory]::Delete($AllowedRootJunctionPath, $false)
    $AllowedRootJunctionPath = $null

    $PruneRootPath = Join-Path $TestRootPath "prune-cache"
    $NowUtc = [DateTime]::SpecifyKind([DateTime]::Parse("2026-08-15T12:00:00"), [DateTimeKind]::Utc)
    $ExpiredRoot = Join-Path $SourceRootPath "expired"
    $FreshRoot = Join-Path $SourceRootPath "fresh"
    $HeldRoot = Join-Path $SourceRootPath "held"
    $MismatchRoot = Join-Path $SourceRootPath "mismatch"
    foreach ($Path in @($ExpiredRoot, $FreshRoot, $HeldRoot, $MismatchRoot)) {
        $null = New-Item -ItemType Directory -Path $Path -Force
    }
    $ExpiredHash = Get-BuildPlatformProjectHash -ProjectRootPath $ExpiredRoot
    $FreshHash = Get-BuildPlatformProjectHash -ProjectRootPath $FreshRoot
    $HeldHash = Get-BuildPlatformProjectHash -ProjectRootPath $HeldRoot
    $MismatchHash = Get-BuildPlatformProjectHash -ProjectRootPath $MismatchRoot
    $ProjectsRootPath = Join-Path $PruneRootPath "projects"
    Write-CacheMetadata -ProjectCacheRootPath (Join-Path $ProjectsRootPath $ExpiredHash) -ProjectRootPath $ExpiredRoot -LastUsedUtc $NowUtc.AddDays(-31)
    Write-CacheMetadata -ProjectCacheRootPath (Join-Path $ProjectsRootPath $FreshHash) -ProjectRootPath $FreshRoot -LastUsedUtc $NowUtc.AddDays(-29)
    Write-CacheMetadata -ProjectCacheRootPath (Join-Path $ProjectsRootPath $HeldHash) -ProjectRootPath $HeldRoot -LastUsedUtc $NowUtc.AddDays(-31)
    Write-CacheMetadata -ProjectCacheRootPath (Join-Path $ProjectsRootPath $MismatchHash) -ProjectRootPath $ExpiredRoot -LastUsedUtc $NowUtc.AddDays(-31)
    $MalformedPath = Join-Path $ProjectsRootPath "11111111111111111111111111111111"
    $MissingPath = Join-Path $ProjectsRootPath "22222222222222222222222222222222"
    $InvalidDirectoryPath = Join-Path $ProjectsRootPath "not-a-project-hash"
    $BlankRootPath = Join-Path $ProjectsRootPath "33333333333333333333333333333333"
    $InvalidDatePath = Join-Path $ProjectsRootPath "44444444444444444444444444444444"
    $null = New-Item -ItemType Directory -Path $MalformedPath -Force
    $null = New-Item -ItemType Directory -Path $MissingPath -Force
    $null = New-Item -ItemType Directory -Path $InvalidDirectoryPath -Force
    $null = New-Item -ItemType Directory -Path $BlankRootPath -Force
    $null = New-Item -ItemType Directory -Path $InvalidDatePath -Force
    Set-Content -LiteralPath (Join-Path $MalformedPath "cache-metadata.json") -Value "not json" -NoNewline
    [ordered]@{ projectRootPath = " "; lastUsedUtc = $NowUtc.AddDays(-31).ToString("o") } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $BlankRootPath "cache-metadata.json") -Encoding UTF8
    [ordered]@{ projectRootPath = $ExpiredRoot; lastUsedUtc = "not-a-date" } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $InvalidDatePath "cache-metadata.json") -Encoding UTF8
    foreach ($Path in @($MalformedPath, $MissingPath, $InvalidDirectoryPath, $BlankRootPath, $InvalidDatePath)) {
        New-Sentinel -LiteralPath (Join-Path $Path "preserve.txt")
    }

    $PruneJunctionRoot = Join-Path $SourceRootPath "prune-junction"
    $PruneJunctionHash = Get-BuildPlatformProjectHash -ProjectRootPath $PruneJunctionRoot
    $PruneJunctionBackingPath = Join-Path $TestRootPath "prune-junction-backing"
    $PruneJunctionPath = Join-Path $ProjectsRootPath $PruneJunctionHash
    Write-CacheMetadata `
        -ProjectCacheRootPath $PruneJunctionBackingPath `
        -ProjectRootPath $PruneJunctionRoot `
        -LastUsedUtc $NowUtc.AddDays(-31)
    $JunctionOutput = & cmd.exe /d /c mklink /J $PruneJunctionPath $PruneJunctionBackingPath 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create disposable prune junction. $($JunctionOutput -join ' ')"
    }

    $HeldLockPath = Join-Path (Join-Path $PruneRootPath "locks") ($HeldHash + ".lock")
    $HeldLock = Enter-BuildPlatformProjectLock `
        -LockPath $HeldLockPath `
        -Metadata ([ordered]@{ projectPath = $HeldRoot }) `
        -Timeout ([TimeSpan]::Zero)

    Remove-BuildPlatformExpiredProjectCaches -CacheRootPath $PruneRootPath -OlderThanDays 0 -NowUtc $NowUtc
    Assert-PathExists -LiteralPath (Join-Path $ProjectsRootPath $ExpiredHash)
    Remove-BuildPlatformExpiredProjectCaches -CacheRootPath $PruneRootPath -OlderThanDays 30 -NowUtc $NowUtc
    Assert-PathMissing -LiteralPath (Join-Path $ProjectsRootPath $ExpiredHash)
    foreach ($PreservedPath in @(
            (Join-Path $ProjectsRootPath $FreshHash),
            (Join-Path $ProjectsRootPath $HeldHash),
            (Join-Path $ProjectsRootPath $MismatchHash),
            $MalformedPath,
            $MissingPath,
            $InvalidDirectoryPath,
            $BlankRootPath,
            $InvalidDatePath,
            $PruneJunctionPath,
            (Join-Path $PruneJunctionBackingPath "cache-metadata.json")
        )) {
        Assert-PathExists -LiteralPath $PreservedPath
    }
    [System.IO.Directory]::Delete($PruneJunctionPath, $false)
    $PruneJunctionPath = $null
    Exit-BuildPlatformProjectLock -LockHandle $HeldLock
    $HeldLock = $null

    Set-Content -LiteralPath (Join-Path $FakeToolsPath "dotnet.cmd") -Value @'
@echo off
setlocal EnableExtensions
set "PublishOutputPath="
set "IsEditorInvocation="
echo %~1| findstr /I /R "\.dll$" >nul && set "IsEditorInvocation=1"
:parse
if "%~1"=="" goto parsed
if /I "%~1"=="-o" (
  set "PublishOutputPath=%~2"
  shift
)
shift
goto parse
:parsed
if not "%PublishOutputPath%"=="" (
  if not exist "%PublishOutputPath%" mkdir "%PublishOutputPath%"
  echo fake assembly> "%PublishOutputPath%\helengine.editor.app.dll"
)
if not "%HELENGINE_MAINTENANCE_METADATA_PATH%"=="" if exist "%HELENGINE_MAINTENANCE_METADATA_PATH%" if not exist "%HELENGINE_MAINTENANCE_METADATA_CAPTURE%" copy /Y "%HELENGINE_MAINTENANCE_METADATA_PATH%" "%HELENGINE_MAINTENANCE_METADATA_CAPTURE%" >nul
if /I "%HELENGINE_MAINTENANCE_SABOTAGE_METADATA%"=="1" if "%IsEditorInvocation%"=="1" (
  del /F /Q "%HELENGINE_MAINTENANCE_METADATA_PATH%" >nul 2>&1
  mkdir "%HELENGINE_MAINTENANCE_METADATA_PATH%"
  if not "%HELENGINE_MAINTENANCE_EDITOR_EXIT_CODE%"=="" exit /b %HELENGINE_MAINTENANCE_EDITOR_EXIT_CODE%
)
exit /b 0
'@ -NoNewline

    $OriginalDotNetExecutablePath = $env:HELENGINE_DOTNET_EXECUTABLE_PATH
    $OriginalMetadataPath = $env:HELENGINE_MAINTENANCE_METADATA_PATH
    $OriginalMetadataCapture = $env:HELENGINE_MAINTENANCE_METADATA_CAPTURE
    $OriginalSabotageMetadata = $env:HELENGINE_MAINTENANCE_SABOTAGE_METADATA
    $OriginalEditorExitCode = $env:HELENGINE_MAINTENANCE_EDITOR_EXIT_CODE
    try {
        $env:HELENGINE_DOTNET_EXECUTABLE_PATH = Join-Path $FakeToolsPath "dotnet.cmd"
        $env:HELENGINE_MAINTENANCE_METADATA_PATH = $LayoutA.MetadataPath
        $env:HELENGINE_MAINTENANCE_METADATA_CAPTURE = $MetadataRunningCapturePath

        $OrdinaryEditorSentinel = Join-Path $LayoutA.EditorArtifactsPath "ordinary-editor.txt"
        $OrdinaryPlatformSentinel = Join-Path $LayoutA.PlatformCacheRootPath "ordinary-platform.txt"
        New-Sentinel -LiteralPath $OrdinaryEditorSentinel
        New-Sentinel -LiteralPath $OrdinaryPlatformSentinel
        $OrdinaryResult = Invoke-Wrapper
        if ($OrdinaryResult.ExitCode -ne 0) {
            throw "Ordinary wrapper build failed with exit code $($OrdinaryResult.ExitCode). $($OrdinaryResult.Output -join [Environment]::NewLine)"
        }
        Assert-PathExists -LiteralPath $OrdinaryEditorSentinel
        Assert-PathExists -LiteralPath $OrdinaryPlatformSentinel

        $CleanEditorSentinel = Join-Path $LayoutA.EditorArtifactsPath "clean-editor.txt"
        $CleanPlatformSentinel = Join-Path $LayoutA.PlatformCacheRootPath "clean-platform.txt"
        New-Sentinel -LiteralPath $CleanEditorSentinel
        New-Sentinel -LiteralPath $CleanPlatformSentinel
        $CleanResult = Invoke-Wrapper -Clean
        if ($CleanResult.ExitCode -ne 0) {
            throw "Clean wrapper build failed with exit code $($CleanResult.ExitCode). $($CleanResult.Output -join [Environment]::NewLine)"
        }
        Assert-PathMissing -LiteralPath $CleanEditorSentinel
        Assert-PathMissing -LiteralPath $CleanPlatformSentinel

        $WrapperExpiredRoot = Join-Path $SourceRootPath "wrapper-expired"
        $null = New-Item -ItemType Directory -Path $WrapperExpiredRoot -Force
        $WrapperExpiredHash = Get-BuildPlatformProjectHash -ProjectRootPath $WrapperExpiredRoot
        $WrapperExpiredCachePath = Join-Path (Join-Path $CacheRootPath "projects") $WrapperExpiredHash
        Write-CacheMetadata `
            -ProjectCacheRootPath $WrapperExpiredCachePath `
            -ProjectRootPath $WrapperExpiredRoot `
            -LastUsedUtc ([DateTime]::UtcNow.AddDays(-31))
        $PruneResult = Invoke-Wrapper -PruneDays 30
        if ($PruneResult.ExitCode -ne 0) {
            throw "Prune wrapper build failed with exit code $($PruneResult.ExitCode). $($PruneResult.Output -join [Environment]::NewLine)"
        }
        Assert-PathMissing -LiteralPath $WrapperExpiredCachePath
        foreach ($PreservedPath in @($SourceSentinelPath, $OutputSentinelPath)) {
            Assert-PathExists -LiteralPath $PreservedPath
        }

        Assert-PathExists -LiteralPath $MetadataRunningCapturePath
        $RunningMetadata = Get-Content -LiteralPath $MetadataRunningCapturePath -Raw | ConvertFrom-Json
        $TerminalMetadata = Get-Content -LiteralPath $LayoutA.MetadataPath -Raw | ConvertFrom-Json
        if ([DateTime]::Parse($TerminalMetadata.lastUsedUtc).ToUniversalTime() -lt [DateTime]::Parse($RunningMetadata.lastUsedUtc).ToUniversalTime()) {
            throw "Terminal metadata update predates the metadata observed while the build was running."
        }

        $env:HELENGINE_MAINTENANCE_SABOTAGE_METADATA = "1"
        foreach ($MetadataFailureCase in @(
                [pscustomobject]@{ NativeExitCode = 0; ExpectedStatus = "succeeded" },
                [pscustomobject]@{ NativeExitCode = 37; ExpectedStatus = "failed" }
            )) {
            if (Test-Path -LiteralPath $LayoutA.MetadataPath) {
                Remove-Item -LiteralPath $LayoutA.MetadataPath -Recurse -Force
            }
            if (Test-Path -LiteralPath $MetadataRunningCapturePath) {
                Remove-Item -LiteralPath $MetadataRunningCapturePath -Force
            }
            if ($MetadataFailureCase.NativeExitCode -eq 0) {
                $env:HELENGINE_MAINTENANCE_EDITOR_EXIT_CODE = $null
            } else {
                $env:HELENGINE_MAINTENANCE_EDITOR_EXIT_CODE = [string]$MetadataFailureCase.NativeExitCode
            }

            $MetadataFailureResult = Invoke-Wrapper
            if ($MetadataFailureResult.ExitCode -ne $MetadataFailureCase.NativeExitCode) {
                throw "Terminal metadata failure changed native exit code $($MetadataFailureCase.NativeExitCode) to $($MetadataFailureResult.ExitCode). $($MetadataFailureResult.Output -join [Environment]::NewLine)"
            }
            $StatePath = Join-Path $ProjectAOutputPath ".helengine-build-state.json"
            $State = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
            if ($State.status -cne $MetadataFailureCase.ExpectedStatus -or
                [int]$State.exitCode -ne $MetadataFailureCase.NativeExitCode) {
                throw "Terminal metadata failure did not preserve '$($MetadataFailureCase.ExpectedStatus)' state and native exit code $($MetadataFailureCase.NativeExitCode)."
            }
            if (Test-BuildPlatformProjectLockHeld -LockPath $LayoutA.LockPath) {
                throw "Terminal metadata failure retained the current project lock."
            }
            foreach ($PreservedPath in @($SourceSentinelPath, $OutputSentinelPath)) {
                Assert-PathExists -LiteralPath $PreservedPath
            }
        }
    } finally {
        $env:HELENGINE_DOTNET_EXECUTABLE_PATH = $OriginalDotNetExecutablePath
        $env:HELENGINE_MAINTENANCE_METADATA_PATH = $OriginalMetadataPath
        $env:HELENGINE_MAINTENANCE_METADATA_CAPTURE = $OriginalMetadataCapture
        $env:HELENGINE_MAINTENANCE_SABOTAGE_METADATA = $OriginalSabotageMetadata
        $env:HELENGINE_MAINTENANCE_EDITOR_EXIT_CODE = $OriginalEditorExitCode
    }

    $OriginalErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $NegativeOutput = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $WrapperPath `
            -Project $ProjectAPath -Platform ps2 -Output $ProjectAOutputPath `
            -EditorProject $EditorProjectPath -CacheRoot $CacheRootPath `
            -PruneCacheOlderThanDays -1 2>&1)
        $NegativeExitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $OriginalErrorActionPreference
    }
    if ($NegativeExitCode -ne 2) {
        throw "Negative prune argument exited $NegativeExitCode instead of 2. $($NegativeOutput -join [Environment]::NewLine)"
    }

    $WrapperText = Get-Content -LiteralPath $WrapperPath -Raw
    if ([regex]::Matches($WrapperText, 'Write-BuildPlatformCacheMetadata\s').Count -ne 2) {
        throw "The wrapper must contain exactly two cache metadata write sites."
    }
    if ([regex]::Matches($WrapperText, 'Write-BuildPlatformState\s').Count -ne 2) {
        throw "The wrapper must retain exactly two build-state write sites."
    }
    $MaintenanceIndex = $WrapperText.IndexOf("Remove-BuildPlatformSelectedCache", [System.StringComparison]::Ordinal)
    $RunningStateIndex = $WrapperText.IndexOf('-Status "running"', [System.StringComparison]::Ordinal)
    if ($MaintenanceIndex -lt 0 -or $MaintenanceIndex -gt $RunningStateIndex) {
        throw "Selected maintenance is not wired before running-state creation."
    }
    $TerminalMetadataIndex = $WrapperText.LastIndexOf("Write-BuildPlatformCacheMetadata", [System.StringComparison]::Ordinal)
    $LockReleaseIndex = $WrapperText.LastIndexOf("Exit-BuildPlatformProjectLock", [System.StringComparison]::Ordinal)
    if ($TerminalMetadataIndex -lt 0 -or $TerminalMetadataIndex -gt $LockReleaseIndex) {
        throw "Terminal cache metadata is not updated before project-lock release."
    }

    Write-Output "MAINTENANCE_TEST_PASS"
} finally {
    if ($null -ne $HeldLock) {
        Exit-BuildPlatformProjectLock -LockHandle $HeldLock
    }
    if ($null -ne $JunctionPath -and (Test-Path -LiteralPath $JunctionPath)) {
        [System.IO.Directory]::Delete($JunctionPath, $false)
    }
    if ($null -ne $AllowedRootJunctionPath -and (Test-Path -LiteralPath $AllowedRootJunctionPath)) {
        [System.IO.Directory]::Delete($AllowedRootJunctionPath, $false)
    }
    if ($null -ne $PruneJunctionPath -and (Test-Path -LiteralPath $PruneJunctionPath)) {
        [System.IO.Directory]::Delete($PruneJunctionPath, $false)
    }
    if (Test-Path -LiteralPath $TestRootPath) {
        Remove-Item -LiteralPath $TestRootPath -Recurse -Force
    }
}

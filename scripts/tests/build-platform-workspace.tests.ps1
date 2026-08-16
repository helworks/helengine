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
$RunningStateCapturePath = Join-Path $TestRootPath "running-state.json"
$EnvironmentEnumerationGuardPath = Join-Path $TestRootPath "reject-environment-enumeration.ps1"
$EnvironmentCleanupGuardPath = Join-Path $TestRootPath "capture-environment-cleanup.ps1"
$AdditionalArgumentsLauncherPath = Join-Path $TestRootPath "invoke-with-additional-arguments.ps1"
$RobocopyMarkerPath = Join-Path $TestRootPath "robocopy-invoked.txt"
$ProjectPath = Join-Path $TestRootPath "authored-project\project.heproj"
$EditorProjectPath = Join-Path $TestRootPath "editor\editor.csproj"
$EditorProjectBPath = Join-Path $TestRootPath "editor-b\editor.csproj"
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
        [int]$PruneDays = 0,

        [Parameter()]
        [string]$CleanupCapturePath = "",

        [Parameter()]
        [int]$LockTimeoutMilliseconds = 0,

        [Parameter()]
        [string]$InvocationOutputPath = $OutputArgumentPath,

        [Parameter()]
        [string[]]$AdditionalArguments = @()
    )

    $InvocationScriptPath = $WrapperPath
    $Arguments = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $InvocationScriptPath,
        "-Project",
        $ProjectPath,
        "-Platform",
        "windows",
        "-Output",
        $InvocationOutputPath,
        "-Configuration",
        "Release",
        "-BuildProfile",
        "profiler",
        "-EditorProject",
        $EditorProjectPath
    )
    if (-not [string]::IsNullOrWhiteSpace($CleanupCapturePath)) {
        $InvocationScriptPath = $EnvironmentCleanupGuardPath
        $Arguments[4] = $InvocationScriptPath
        $Arguments += @(
            "-WrapperPath",
            $WrapperPath,
            "-CleanupCapturePath",
            $CleanupCapturePath
        )
    }
    if (-not [string]::IsNullOrWhiteSpace($CachePath)) {
        $Arguments += @("-CacheRoot", $CachePath)
    }
    if (-not [string]::IsNullOrWhiteSpace($WorkspacePath)) {
        $Arguments += @("-WorkspaceRoot", $WorkspacePath)
    }
    if ($PruneDays -ne 0) {
        $Arguments += @("-PruneCacheOlderThanDays", $PruneDays)
    }
    if ($LockTimeoutMilliseconds -gt 0) {
        $Arguments += @("-LockTimeout", ([TimeSpan]::FromMilliseconds($LockTimeoutMilliseconds).ToString("c")))
    }
    if ($AdditionalArguments.Count -gt 0) {
        $InvocationScriptPath = $AdditionalArgumentsLauncherPath
        $Arguments[4] = $InvocationScriptPath
        $Arguments += @(
            "-WrapperPath",
            $WrapperPath,
            "-AdditionalArgumentsJsonBase64",
            [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(
                (ConvertTo-Json -InputObject ([string[]]$AdditionalArguments) -Compress)
            ))
        )
    }

    $OriginalErrorActionPreference = $ErrorActionPreference
    $OriginalExpectedStatePath = $env:HELENGINE_WORKSPACE_EXPECTED_STATE_PATH
    try {
        $ErrorActionPreference = "Continue"
        $env:HELENGINE_WORKSPACE_EXPECTED_STATE_PATH = Join-Path $InvocationOutputPath ".helengine-build-state.json"
        $InvocationOutput = @(& powershell.exe @Arguments 2>&1)
        $InvocationExitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $OriginalErrorActionPreference
        $env:HELENGINE_WORKSPACE_EXPECTED_STATE_PATH = $OriginalExpectedStatePath
    }
    return [pscustomobject]@{
        ExitCode = $InvocationExitCode
        Output = $InvocationOutput
    }
}

function Assert-EnvironmentCleanupCapture {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CapturePath
    )

    if (-not (Test-Path -LiteralPath $CapturePath -PathType Leaf)) {
        throw "The wrapper invocation did not execute its caller environment-cleanup checkpoint '$CapturePath'."
    }

    $CapturedEnvironment = Get-Content -LiteralPath $CapturePath -Raw | ConvertFrom-Json
    foreach ($EnvironmentField in @{
            cacheRoot = "HELENGINE_BUILD_CACHE_ROOT"
            configuration = "HELENGINE_BUILD_CONFIGURATION"
            profile = "HELENGINE_BUILD_PROFILE"
            sourceRoot = "HELENGINE_SOURCE_ROOT"
        }.GetEnumerator()) {
        $ExpectedValue = [Environment]::GetEnvironmentVariable(
            $EnvironmentField.Value,
            [EnvironmentVariableTarget]::Process
        )
        $ActualValue = $CapturedEnvironment.($EnvironmentField.Key)
        if ($null -eq $ExpectedValue) {
            if ($null -ne $ActualValue) {
                throw "Environment cleanup retained $($EnvironmentField.Value)='$ActualValue' instead of removing it."
            }
        } elseif ($ActualValue -cne $ExpectedValue) {
            throw "Environment cleanup restored $($EnvironmentField.Value)='$ActualValue' instead of '$ExpectedValue'."
        }
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

function Get-BuildPlatformDirectorySnapshot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $CanonicalPath = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $CanonicalPath -PathType Container)) {
        return @("<missing>")
    }

    $Entries = New-Object System.Collections.Generic.List[string]
    foreach ($Item in @(Get-ChildItem -LiteralPath $CanonicalPath -Force -Recurse | Sort-Object FullName)) {
        $RelativePath = $Item.FullName.Substring($CanonicalPath.Length).TrimStart([char[]]@('\', '/'))
        if ($Item.PSIsContainer) {
            $null = $Entries.Add(("D|{0}|{1}" -f $RelativePath, $Item.Attributes))
        } else {
            $FileHash = (Get-FileHash -LiteralPath $Item.FullName -Algorithm SHA256).Hash
            $null = $Entries.Add(("F|{0}|{1}|{2}|{3}|{4}" -f `
                $RelativePath, $Item.Length, $Item.Attributes, $Item.LastWriteTimeUtc.Ticks, $FileHash))
        }
    }
    return $Entries.ToArray()
}

function Get-BuildPlatformFileSnapshot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return "<missing>"
    }

    $Item = Get-Item -LiteralPath $Path -Force
    $FileHash = (Get-FileHash -LiteralPath $Item.FullName -Algorithm SHA256).Hash
    return "F|$($Item.Length)|$($Item.Attributes)|$($Item.LastWriteTimeUtc.Ticks)|$FileHash"
}

function Assert-BuildPlatformSnapshotUnchanged {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Description,

        [Parameter(Mandatory = $true)]
        [string[]]$ExpectedSnapshot,

        [Parameter(Mandatory = $true)]
        [string[]]$ActualSnapshot
    )

    $Differences = @(Compare-Object -ReferenceObject $ExpectedSnapshot -DifferenceObject $ActualSnapshot)
    if ($Differences.Count -ne 0) {
        throw "$Description changed: $($Differences | Out-String)"
    }
}

function Assert-RejectedBeforeBuildPlatformMutation {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Result,

        [Parameter(Mandatory = $true)]
        [string]$CaseName,

        [Parameter(Mandatory = $true)]
        [int]$InvocationCountBefore,

        [Parameter(Mandatory = $true)]
        [string]$OutputPath,

        [Parameter(Mandatory = $true)]
        [string]$ProjectCacheRootPath,

        [Parameter(Mandatory = $true)]
        [string]$SentinelPath,

        [Parameter()]
        [string]$ExpectedDiagnostic = "",

        [Parameter()]
        [switch]$AssertProjectCacheAbsent,

        [Parameter()]
        [switch]$AssertOutputAbsent,

        [Parameter()]
        [AllowEmptyCollection()]
        [string[]]$CacheRootSnapshot = @(),

        [Parameter()]
        [AllowEmptyCollection()]
        [string[]]$OutputSnapshot = @(),

        [Parameter()]
        [AllowEmptyCollection()]
        [string[]]$ProjectCacheSnapshot = @(),

        [Parameter()]
        [string]$MetadataSnapshot = "",

        [Parameter()]
        [string]$LockSnapshot = "",

        [Parameter()]
        [string]$SentinelSnapshot = "",

        [Parameter()]
        [string]$CacheRootPath = "",

        [Parameter()]
        [string]$MetadataPath = "",

        [Parameter()]
        [string]$LockPath = ""
    )

    if ($Result.ExitCode -ne 2) {
        throw "$CaseName must exit 2, got $($Result.ExitCode). $($Result.Output -join [Environment]::NewLine)"
    }
    if (@(Get-Content -LiteralPath $CapturePath).Count -ne $InvocationCountBefore) {
        throw "$CaseName reached the editor."
    }
    if (Test-Path -LiteralPath (Join-Path $OutputPath ".helengine-build-state.json")) {
        throw "$CaseName wrote build state."
    }
    if ($AssertOutputAbsent -and (Test-Path -LiteralPath $OutputPath)) {
        throw "$CaseName created output '$OutputPath'."
    }
    if ($AssertProjectCacheAbsent -and (Test-Path -LiteralPath $ProjectCacheRootPath)) {
        throw "$CaseName created project cache '$ProjectCacheRootPath'."
    }
    if (-not (Test-Path -LiteralPath $SentinelPath -PathType Leaf)) {
        throw "$CaseName removed sentinel '$SentinelPath'."
    }
    if ($CacheRootSnapshot.Count -gt 0) {
        Assert-BuildPlatformSnapshotUnchanged `
            -Description "$CaseName cache root" `
            -ExpectedSnapshot $CacheRootSnapshot `
            -ActualSnapshot (Get-BuildPlatformDirectorySnapshot -Path $CacheRootPath)
    }
    if ($OutputSnapshot.Count -gt 0) {
        Assert-BuildPlatformSnapshotUnchanged `
            -Description "$CaseName output tree" `
            -ExpectedSnapshot $OutputSnapshot `
            -ActualSnapshot (Get-BuildPlatformDirectorySnapshot -Path $OutputPath)
    }
    if ($ProjectCacheSnapshot.Count -gt 0) {
        Assert-BuildPlatformSnapshotUnchanged `
            -Description "$CaseName project cache" `
            -ExpectedSnapshot $ProjectCacheSnapshot `
            -ActualSnapshot (Get-BuildPlatformDirectorySnapshot -Path $ProjectCacheRootPath)
    }
    if (-not [string]::IsNullOrWhiteSpace($MetadataSnapshot) -and
        $MetadataSnapshot -cne (Get-BuildPlatformFileSnapshot -Path $MetadataPath)) {
        throw "$CaseName changed cache metadata '$MetadataPath'."
    }
    if (-not [string]::IsNullOrWhiteSpace($LockSnapshot) -and
        $LockSnapshot -cne (Get-BuildPlatformFileSnapshot -Path $LockPath)) {
        throw "$CaseName changed lock metadata '$LockPath'."
    }
    if (-not [string]::IsNullOrWhiteSpace($SentinelSnapshot) -and
        $SentinelSnapshot -cne (Get-BuildPlatformFileSnapshot -Path $SentinelPath)) {
        throw "$CaseName changed sentinel '$SentinelPath'."
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedDiagnostic) -and
        ($Result.Output -join [Environment]::NewLine) -notmatch [regex]::Escape($ExpectedDiagnostic)) {
        throw "$CaseName did not mention '$ExpectedDiagnostic'. $($Result.Output -join [Environment]::NewLine)"
    }
}

function Assert-BuildStateDocument {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$State,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedStatus,

        [Parameter()]
        [AllowNull()]
        [object]$ExpectedExitCode,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedProjectPath
    )

    $ExpectedPropertyNames = @(
        "buildId",
        "projectPath",
        "platform",
        "buildProfile",
        "configuration",
        "startedUtc",
        "completedUtc",
        "status",
        "exitCode"
    )
    $ActualPropertyNames = @($State.PSObject.Properties.Name)
    if ($ActualPropertyNames.Count -ne $ExpectedPropertyNames.Count -or
        @(Compare-Object -ReferenceObject $ExpectedPropertyNames -DifferenceObject $ActualPropertyNames).Count -ne 0) {
        throw "Build state fields were '$($ActualPropertyNames -join ', ')' instead of '$($ExpectedPropertyNames -join ', ')'."
    }

    $ParsedBuildId = [Guid]::Empty
    if (-not [Guid]::TryParse([string]$State.buildId, [ref]$ParsedBuildId) -or $ParsedBuildId -eq [Guid]::Empty) {
        throw "Build state id '$($State.buildId)' was not a non-empty GUID."
    }
    if ($State.projectPath -cne $ExpectedProjectPath -or -not $State.projectPath.EndsWith(".heproj", [StringComparison]::OrdinalIgnoreCase)) {
        throw "Build state project path '$($State.projectPath)' was not canonical project '$ExpectedProjectPath'."
    }
    if ($State.platform -cne "windows" -or $State.buildProfile -cne "profiler" -or $State.configuration -cne "Release") {
        throw "Build state identity was platform='$($State.platform)', profile='$($State.buildProfile)', configuration='$($State.configuration)'."
    }
    if ($State.status -cne $ExpectedStatus) {
        throw "Build state status '$($State.status)' was not '$ExpectedStatus'."
    }
    if ($null -eq $ExpectedExitCode) {
        if ($null -ne $State.exitCode -or $null -ne $State.completedUtc) {
            throw "Running build state must preserve JSON null for completedUtc and exitCode."
        }
    } else {
        if ([int]$State.exitCode -ne [int]$ExpectedExitCode) {
            throw "Build state exit code '$($State.exitCode)' was not '$ExpectedExitCode'."
        }
        if ([string]::IsNullOrWhiteSpace([string]$State.completedUtc)) {
            throw "Terminal build state did not contain completedUtc."
        }
    }

    $StartedUtc = [DateTimeOffset]::Parse([string]$State.startedUtc)
    if ($StartedUtc.Offset -ne [TimeSpan]::Zero) {
        throw "Build state startedUtc '$($State.startedUtc)' was not UTC."
    }
    if ($null -ne $State.completedUtc) {
        $CompletedUtc = [DateTimeOffset]::Parse([string]$State.completedUtc)
        if ($CompletedUtc.Offset -ne [TimeSpan]::Zero) {
            throw "Build state completedUtc '$($State.completedUtc)' was not UTC."
        }
        if ($CompletedUtc -lt $StartedUtc) {
            throw "Build state completedUtc '$CompletedUtc' preceded startedUtc '$StartedUtc'."
        }
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
            EditorProjectPath = $EditorProjectPath
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
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $EditorProjectBPath) -Force
    Set-Content -LiteralPath $ProjectPath -Value "{}" -NoNewline
    Set-Content -LiteralPath $EditorProjectPath -Value "<Project />" -NoNewline
    Set-Content -LiteralPath $EditorProjectBPath -Value "<Project />" -NoNewline
    $ProjectRootPath = Split-Path -Parent $ProjectPath
    $LayoutA = Resolve-BuildPlatformCacheLayout `
        -CacheRootPath $CacheRootPath `
        -ProjectRootPath $ProjectRootPath `
        -EditorProjectPath $EditorProjectPath `
        -Platform windows `
        -Configuration Release `
        -BuildProfile profiler
    $LayoutARepeat = Resolve-BuildPlatformCacheLayout `
        -CacheRootPath $CacheRootPath `
        -ProjectRootPath $ProjectRootPath `
        -EditorProjectPath $EditorProjectPath `
        -Platform windows `
        -Configuration Release `
        -BuildProfile profiler
    $LayoutB = Resolve-BuildPlatformCacheLayout `
        -CacheRootPath $CacheRootPath `
        -ProjectRootPath $ProjectRootPath `
        -EditorProjectPath $EditorProjectBPath `
        -Platform windows `
        -Configuration Release `
        -BuildProfile profiler
    $LegacyVerbosePath = Join-Path $CacheRootPath ("projects\" + $LayoutA.ProjectHash + "\platforms\windows\release\profiler")
    if ($LayoutA.EditorArtifactsPath -cne $LayoutARepeat.EditorArtifactsPath) {
        throw "Editor cache was not stable."
    }
    if ($LayoutA.EditorArtifactsPath -ceq $LayoutB.EditorArtifactsPath) {
        throw "Different editor checkouts shared artifacts."
    }
    if ($LayoutA.PlatformCacheRootPath -cne $LayoutB.PlatformCacheRootPath) {
        throw "Editor identity leaked into platform cache identity."
    }
    if ($LayoutA.PlatformCacheRootPath.Length -ge $LegacyVerbosePath.Length) {
        throw "The v2 platform path was not compacted."
    }
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
    Set-Content -LiteralPath $EnvironmentCleanupGuardPath -Value @'
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$WrapperPath,
    [Parameter(Mandatory = $true)] [string]$CleanupCapturePath,
    [Parameter(Mandatory = $true)] [string]$Project,
    [Parameter(Mandatory = $true)] [string]$Platform,
    [Parameter(Mandatory = $true)] [string]$Output,
    [Parameter(Mandatory = $true)] [string]$Configuration,
    [Parameter(Mandatory = $true)] [string]$BuildProfile,
    [Parameter(Mandatory = $true)] [string]$EditorProject,
    [Parameter(Mandatory = $true)] [string]$CacheRoot,
    [Parameter()] [TimeSpan]$LockTimeout = [TimeSpan]::FromHours(2)
)

$WrapperExitCode = 0
try {
    & $WrapperPath `
        -Project $Project `
        -Platform $Platform `
        -Output $Output `
        -Configuration $Configuration `
        -BuildProfile $BuildProfile `
        -EditorProject $EditorProject `
        -CacheRoot $CacheRoot `
        -LockTimeout $LockTimeout
    $WrapperExitCode = $LASTEXITCODE
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    $WrapperExitCode = 1
} finally {
    [ordered]@{
        cacheRoot = [Environment]::GetEnvironmentVariable("HELENGINE_BUILD_CACHE_ROOT", [EnvironmentVariableTarget]::Process)
        configuration = [Environment]::GetEnvironmentVariable("HELENGINE_BUILD_CONFIGURATION", [EnvironmentVariableTarget]::Process)
        profile = [Environment]::GetEnvironmentVariable("HELENGINE_BUILD_PROFILE", [EnvironmentVariableTarget]::Process)
        sourceRoot = [Environment]::GetEnvironmentVariable("HELENGINE_SOURCE_ROOT", [EnvironmentVariableTarget]::Process)
    } | ConvertTo-Json | Set-Content -LiteralPath $CleanupCapturePath -Encoding UTF8
}
exit $WrapperExitCode
'@ -NoNewline
    Set-Content -LiteralPath $AdditionalArgumentsLauncherPath -Value @'
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$WrapperPath,
    [Parameter(Mandatory = $true)] [string]$Project,
    [Parameter(Mandatory = $true)] [string]$Platform,
    [Parameter(Mandatory = $true)] [string]$Output,
    [Parameter(Mandatory = $true)] [string]$Configuration,
    [Parameter(Mandatory = $true)] [string]$BuildProfile,
    [Parameter(Mandatory = $true)] [string]$EditorProject,
    [Parameter()] [string]$CacheRoot = "",
    [Parameter()] [string]$WorkspaceRoot = "",
    [Parameter()] [int]$PruneCacheOlderThanDays = 0,
    [Parameter()] [TimeSpan]$LockTimeout = [TimeSpan]::FromHours(2),
    [Parameter(Mandatory = $true)] [string]$AdditionalArgumentsJsonBase64
)

$AdditionalArgumentsJson = [Text.Encoding]::UTF8.GetString(
    [Convert]::FromBase64String($AdditionalArgumentsJsonBase64)
)
$AdditionalArguments = ConvertFrom-Json -InputObject $AdditionalArgumentsJson
$WrapperArguments = @{
    Project = $Project
    Platform = $Platform
    Output = $Output
    Configuration = $Configuration
    BuildProfile = $BuildProfile
    EditorProject = $EditorProject
    AdditionalArgs = [string[]]$AdditionalArguments
}
if (-not [string]::IsNullOrWhiteSpace($CacheRoot)) {
    $WrapperArguments.CacheRoot = $CacheRoot
}
if (-not [string]::IsNullOrWhiteSpace($WorkspaceRoot)) {
    $WrapperArguments.WorkspaceRoot = $WorkspaceRoot
}
if ($PruneCacheOlderThanDays -ne 0) {
    $WrapperArguments.PruneCacheOlderThanDays = $PruneCacheOlderThanDays
}
if ($LockTimeout -ne [TimeSpan]::FromHours(2)) {
    $WrapperArguments.LockTimeout = $LockTimeout
}

& $WrapperPath @WrapperArguments
exit $LASTEXITCODE
'@ -NoNewline
    Set-Content -LiteralPath (Join-Path $FakeToolsPath "dotnet.cmd") -Value @'
@echo off
setlocal EnableExtensions
echo %*>> "%HELENGINE_WORKSPACE_CAPTURE%"
if exist "%HELENGINE_WORKSPACE_EXPECTED_STATE_PATH%" (
    if not exist "%HELENGINE_WORKSPACE_RUNNING_STATE_CAPTURE%" copy /Y "%HELENGINE_WORKSPACE_EXPECTED_STATE_PATH%" "%HELENGINE_WORKSPACE_RUNNING_STATE_CAPTURE%" >nul
    findstr /C:"running" "%HELENGINE_WORKSPACE_EXPECTED_STATE_PATH%" >nul
    if errorlevel 1 exit /b 92
) else (
    if not exist "%HELENGINE_WORKSPACE_RUNNING_STATE_CAPTURE%" echo missing> "%HELENGINE_WORKSPACE_RUNNING_STATE_CAPTURE%"
)
set "PublishOutputPath="
set "BuildOutputPath="
set "ProjectFilePath="
set "IsEditorBuild="
:FindArguments
if "%~1"=="" goto RunInvocation
if /I "%~1"=="-o" set "PublishOutputPath=%~2"
if /I "%~1"=="--output" set "BuildOutputPath=%~2"
if /I "%~1"=="--project" set "ProjectFilePath=%~2"
if /I "%~1"=="--build" set "IsEditorBuild=1"
shift
goto FindArguments
:RunInvocation
if not "%PublishOutputPath%"=="" (
    if not exist "%PublishOutputPath%" mkdir "%PublishOutputPath%"
    type nul > "%PublishOutputPath%\helengine.editor.app.dll"
)
if defined IsEditorBuild (
    > "%HELENGINE_WORKSPACE_ENVIRONMENT_CAPTURE%" (
        echo cache=%HELENGINE_BUILD_CACHE_ROOT%
        echo configuration=%HELENGINE_BUILD_CONFIGURATION%
        echo profile=%HELENGINE_BUILD_PROFILE%
    )
    if /I "%HELENGINE_WORKSPACE_SABOTAGE_STATE%"=="1" (
        for %%I in ("%ProjectFilePath%") do echo authored mutation> "%%~dpI\fake-editor-mutation.txt"
        if not exist "%BuildOutputPath%" mkdir "%BuildOutputPath%"
        echo partial output> "%BuildOutputPath%\partial-output.txt"
        del /F /Q "%HELENGINE_WORKSPACE_EXPECTED_STATE_PATH%" >nul 2>&1
        mkdir "%HELENGINE_WORKSPACE_EXPECTED_STATE_PATH%"
    )
    if not "%HELENGINE_WORKSPACE_EDITOR_EXIT_CODE%"=="" (
        for %%I in ("%ProjectFilePath%") do echo authored mutation> "%%~dpI\fake-editor-mutation.txt"
        if not exist "%BuildOutputPath%" mkdir "%BuildOutputPath%"
        echo partial output> "%BuildOutputPath%\partial-output.txt"
        exit /b %HELENGINE_WORKSPACE_EDITOR_EXIT_CODE%
    )
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
    $OriginalExpectedStatePath = $env:HELENGINE_WORKSPACE_EXPECTED_STATE_PATH
    $OriginalRunningStateCapturePath = $env:HELENGINE_WORKSPACE_RUNNING_STATE_CAPTURE
    $OriginalEditorExitCode = $env:HELENGINE_WORKSPACE_EDITOR_EXIT_CODE
    $OriginalSabotageState = $env:HELENGINE_WORKSPACE_SABOTAGE_STATE
    $OriginalRobocopyMarkerPath = $env:HELENGINE_WORKSPACE_ROBOCOPY_MARKER
    $OriginalDotNetExecutablePath = $env:HELENGINE_DOTNET_EXECUTABLE_PATH
    try {
        $env:PATH = $FakeToolsPath + ";" + $OriginalPath
        $env:HELENGINE_WORKSPACE_CAPTURE = $CapturePath
        $env:HELENGINE_WORKSPACE_ENVIRONMENT_CAPTURE = $EnvironmentCapturePath
        $env:HELENGINE_WORKSPACE_EXPECTED_STATE_PATH = Join-Path $OutputPath ".helengine-build-state.json"
        $env:HELENGINE_WORKSPACE_RUNNING_STATE_CAPTURE = $RunningStateCapturePath
        $env:HELENGINE_WORKSPACE_EDITOR_EXIT_CODE = $null
        $env:HELENGINE_WORKSPACE_SABOTAGE_STATE = $null
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

        $OverlapCacheRootPath = Join-Path $TestRootPath "unsafe-overlap-cache"
        $OverlapLayout = Resolve-BuildPlatformCacheLayout `
            -CacheRootPath $OverlapCacheRootPath `
            -ProjectRootPath $ProjectRootPath `
            -EditorProjectPath $EditorProjectPath `
            -Platform windows `
            -Configuration Release `
            -BuildProfile profiler
        $OutputContainingProjectCacheRootPath = Join-Path $TestRootPath "unsafe-output-containing-project-cache"
        $OutputContainingProjectCacheLayout = Resolve-BuildPlatformCacheLayout `
            -CacheRootPath $OutputContainingProjectCacheRootPath `
            -ProjectRootPath $ProjectRootPath `
            -EditorProjectPath $EditorProjectPath `
            -Platform windows `
            -Configuration Release `
            -BuildProfile profiler
        $OverlapCases = @(
            [pscustomobject]@{
                Name = "Output equal to project cache"
                CacheRootPath = $OverlapCacheRootPath
                OutputPath = $OverlapLayout.ProjectCacheRootPath
                ProjectCacheRootPath = $OverlapLayout.ProjectCacheRootPath
                MetadataPath = $OverlapLayout.MetadataPath
                LockPath = $OverlapLayout.LockPath
            },
            [pscustomobject]@{
                Name = "Output beneath project cache"
                CacheRootPath = $OverlapCacheRootPath
                OutputPath = Join-Path $OverlapLayout.ProjectCacheRootPath "nested-output"
                ProjectCacheRootPath = $OverlapLayout.ProjectCacheRootPath
                MetadataPath = $OverlapLayout.MetadataPath
                LockPath = $OverlapLayout.LockPath
            },
            [pscustomobject]@{
                Name = "Project cache beneath output"
                CacheRootPath = $OutputContainingProjectCacheRootPath
                OutputPath = $OutputContainingProjectCacheRootPath
                ProjectCacheRootPath = $OutputContainingProjectCacheLayout.ProjectCacheRootPath
                MetadataPath = $OutputContainingProjectCacheLayout.MetadataPath
                LockPath = $OutputContainingProjectCacheLayout.LockPath
            }
        )
        foreach ($OverlapCase in $OverlapCases) {
            $SentinelPath = (Join-Path (Split-Path -Parent $OverlapCase.OutputPath) "overlap-sentinel.txt")
            $OutputSentinelPath = Join-Path $OverlapCase.OutputPath "output-sentinel.txt"
            $null = New-Item -ItemType Directory -Path $OverlapCase.CacheRootPath -Force
            $null = New-Item -ItemType Directory -Path $OverlapCase.ProjectCacheRootPath -Force
            $null = New-Item -ItemType Directory -Path $OverlapCase.OutputPath -Force
            $null = New-Item -ItemType Directory -Path (Split-Path -Parent $OverlapCase.MetadataPath) -Force
            $null = New-Item -ItemType Directory -Path (Split-Path -Parent $OverlapCase.LockPath) -Force
            $null = New-Item -ItemType Directory -Path (Split-Path -Parent $SentinelPath) -Force
            Set-Content -LiteralPath $OverlapCase.MetadataPath -Value ("metadata " + $OverlapCase.Name) -NoNewline
            Set-Content -LiteralPath $OverlapCase.LockPath -Value ("lock " + $OverlapCase.Name) -NoNewline
            Set-Content -LiteralPath $SentinelPath -Value $OverlapCase.Name -NoNewline
            Set-Content -LiteralPath $OutputSentinelPath -Value $OverlapCase.Name -NoNewline
            $CacheRootSnapshot = Get-BuildPlatformDirectorySnapshot -Path $OverlapCase.CacheRootPath
            $OutputSnapshot = Get-BuildPlatformDirectorySnapshot -Path $OverlapCase.OutputPath
            $ProjectCacheSnapshot = Get-BuildPlatformDirectorySnapshot -Path $OverlapCase.ProjectCacheRootPath
            $MetadataSnapshot = Get-BuildPlatformFileSnapshot -Path $OverlapCase.MetadataPath
            $LockSnapshot = Get-BuildPlatformFileSnapshot -Path $OverlapCase.LockPath
            $SentinelSnapshot = Get-BuildPlatformFileSnapshot -Path $SentinelPath
            $InvocationCountBefore = @(Get-Content -LiteralPath $CapturePath).Count
            $OverlapResult = Invoke-ControlledWrapper `
                -CachePath $OverlapCase.CacheRootPath `
                -InvocationOutputPath $OverlapCase.OutputPath
            Assert-RejectedBeforeBuildPlatformMutation `
                -Result $OverlapResult `
                -CaseName $OverlapCase.Name `
                -InvocationCountBefore $InvocationCountBefore `
                -OutputPath $OverlapCase.OutputPath `
                -ProjectCacheRootPath $OverlapCase.ProjectCacheRootPath `
                -SentinelPath $SentinelPath `
                -CacheRootSnapshot $CacheRootSnapshot `
                -OutputSnapshot $OutputSnapshot `
                -ProjectCacheSnapshot $ProjectCacheSnapshot `
                -MetadataSnapshot $MetadataSnapshot `
                -LockSnapshot $LockSnapshot `
                -SentinelSnapshot $SentinelSnapshot `
                -CacheRootPath $OverlapCase.CacheRootPath `
                -MetadataPath $OverlapCase.MetadataPath `
                -LockPath $OverlapCase.LockPath
        }

        $ReservedCases = @(
            @('--project', 'C:\decoy\project.heproj'),
            @('--PROJECT=C:\decoy\project.heproj'),
            @('--build', 'ps2'),
            @('--build=ps2'),
            @('--build-profile', 'release'),
            @('--build-profile=release'),
            @('--output', 'C:\decoy\output'),
            @('--output=C:\decoy\output')
        )
        foreach ($ReservedCase in $ReservedCases) {
            $ReservedArguments = @($ReservedCase)
            $RejectedSwitch = ([string]$ReservedArguments[0]).Split("=", 2)[0]
            $CaseIdentifier = [Guid]::NewGuid().ToString("N")
            $ReservedCacheRootPath = Join-Path $TestRootPath ("unsafe-arguments-cache-" + $CaseIdentifier)
            $ReservedOutputPath = Join-Path $TestRootPath ("unsafe-arguments-output-" + $CaseIdentifier)
            $ReservedLayout = Resolve-BuildPlatformCacheLayout `
                -CacheRootPath $ReservedCacheRootPath `
                -ProjectRootPath $ProjectRootPath `
                -EditorProjectPath $EditorProjectPath `
                -Platform windows `
                -Configuration Release `
                -BuildProfile profiler
            $SentinelPath = (Join-Path (Split-Path -Parent $ReservedOutputPath) ("arguments-sentinel-" + $CaseIdentifier + ".txt"))
            Set-Content -LiteralPath $SentinelPath -Value $RejectedSwitch -NoNewline
            $InvocationCountBefore = @(Get-Content -LiteralPath $CapturePath).Count
            $ReservedResult = Invoke-ControlledWrapper `
                -CachePath $ReservedCacheRootPath `
                -InvocationOutputPath $ReservedOutputPath `
                -AdditionalArguments (@("--custom-argument", "pass-through") + $ReservedArguments)
            Assert-RejectedBeforeBuildPlatformMutation `
                -Result $ReservedResult `
                -CaseName "Reserved additional argument '$($ReservedArguments[0])'" `
                -InvocationCountBefore $InvocationCountBefore `
                -OutputPath $ReservedOutputPath `
                -ProjectCacheRootPath $ReservedLayout.ProjectCacheRootPath `
                -SentinelPath $SentinelPath `
                -ExpectedDiagnostic $RejectedSwitch `
                -AssertProjectCacheAbsent `
                -AssertOutputAbsent
        }

        $AllowedArgumentsCacheRootPath = Join-Path $TestRootPath "allowed-arguments-cache"
        $AllowedArgumentsOutputPath = Join-Path $TestRootPath "allowed-arguments-output"
        $AllowedArgumentsResult = Invoke-ControlledWrapper `
            -CachePath $AllowedArgumentsCacheRootPath `
            -InvocationOutputPath $AllowedArgumentsOutputPath `
            -AdditionalArguments @("--custom-argument", "pass-through", "--projectile", "safe")
        Assert-Success -Result $AllowedArgumentsResult -CaseName "Allowed additional argument pass-through"
        $AllowedArgumentsInvocation = Get-Content -LiteralPath $CapturePath |
            Where-Object { $_ -match '--build windows' } |
            Select-Object -Last 1
        if ($AllowedArgumentsInvocation -notmatch [regex]::Escape("--custom-argument pass-through")) {
            throw "Allowed additional argument was not passed through to the editor: '$AllowedArgumentsInvocation'."
        }
        if ($AllowedArgumentsInvocation -notmatch [regex]::Escape("--projectile safe")) {
            throw "Allowed project-like argument was not passed through to the editor: '$AllowedArgumentsInvocation'."
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
        $EditorProjectHash = Get-ExpectedProjectHash -ProjectRootPath $EditorProjectPath
        $ProjectCacheRootPath = Join-Path $CanonicalCacheRootPath ("v2\" + $ProjectHash)
        $ExpectedEditorCachePath = Join-Path $ProjectCacheRootPath ("e\" + $EditorProjectHash + "\release")
        $ExpectedEditorArtifactsPath = Join-Path $ExpectedEditorCachePath "a"
        $ExpectedEditorPublishPath = Join-Path $ExpectedEditorCachePath "p"
        $ExpectedPlatformCachePath = Join-Path $ProjectCacheRootPath "b\windows\release\profiler"
        $ExpectedLockPath = Join-Path $CanonicalCacheRootPath ("v2\l\" + $ProjectHash + ".lock")
        $ExpectedMetadataPath = Join-Path $ProjectCacheRootPath "m.json"
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
            Where-Object { $_.Name -match '^[0-9a-f]{32}$' -and $_.Parent.Name -ne 'v2' -and $_.Parent.Name -ne 'e' })
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

        if (-not (Test-Path -LiteralPath $ExpectedLockPath -PathType Leaf)) {
            throw "The wrapper did not retain persistent lock metadata at '$ExpectedLockPath'."
        }
        $LockMetadata = Get-Content -LiteralPath $ExpectedLockPath -Raw | ConvertFrom-Json
        if ($LockMetadata.projectPath -cne $CanonicalProjectPath) {
            throw "Persistent lock metadata recorded '$($LockMetadata.projectPath)' instead of '$CanonicalProjectPath'."
        }

        if (-not (Test-Path -LiteralPath $ExpectedStatePath -PathType Leaf)) {
            throw "Successful build state was not written to '$ExpectedStatePath'."
        }
        $SuccessfulState = Get-Content -LiteralPath $ExpectedStatePath -Raw | ConvertFrom-Json
        Assert-BuildStateDocument `
            -State $SuccessfulState `
            -ExpectedStatus "succeeded" `
            -ExpectedExitCode 0 `
            -ExpectedProjectPath $CanonicalProjectPath

        $ExpectedInvocationId = "b40ab19d-4d81-4db0-a0d4-9b818b49c7c0"
        $env:HELENGINE_BUILD_INVOCATION_ID = $ExpectedInvocationId
        try {
            $AdoptedInvocationResult = Invoke-ControlledWrapper -CachePath $CacheRootPath
        } finally {
            $env:HELENGINE_BUILD_INVOCATION_ID = $null
        }
        Assert-Success -Result $AdoptedInvocationResult -CaseName "The wrapper invocation with an explicit build identity"
        $AdoptedInvocationState = Get-Content -LiteralPath $ExpectedStatePath -Raw | ConvertFrom-Json
        if ($AdoptedInvocationState.buildId -cne $ExpectedInvocationId) {
            throw "The wrapper recorded build id '$($AdoptedInvocationState.buildId)' instead of '$ExpectedInvocationId'."
        }

        $GeneratedInvocationResult = Invoke-ControlledWrapper -CachePath $CacheRootPath
        Assert-Success -Result $GeneratedInvocationResult -CaseName "The wrapper invocation without an explicit build identity"
        $GeneratedInvocationState = Get-Content -LiteralPath $ExpectedStatePath -Raw | ConvertFrom-Json
        if ([string]$GeneratedInvocationState.buildId -cnotmatch '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$') {
            throw "The wrapper generated build id '$($GeneratedInvocationState.buildId)' instead of a canonical D-format GUID."
        }

        $InvalidInvocationOutputPath = Join-Path $TestRootPath "invalid-invocation-output"
        $InvocationCountBeforeInvalidInvocationId = @(Get-Content -LiteralPath $CapturePath).Count
        $env:HELENGINE_BUILD_INVOCATION_ID = "not-a-guid"
        try {
            $InvalidInvocationResult = Invoke-ControlledWrapper `
                -CachePath $CacheRootPath `
                -InvocationOutputPath $InvalidInvocationOutputPath
        } finally {
            $env:HELENGINE_BUILD_INVOCATION_ID = $null
        }
        if ($InvalidInvocationResult.ExitCode -ne 2) {
            throw "A malformed HELENGINE_BUILD_INVOCATION_ID must exit 2, got $($InvalidInvocationResult.ExitCode)."
        }
        if (@(Get-Content -LiteralPath $CapturePath).Count -ne $InvocationCountBeforeInvalidInvocationId) {
            throw "A malformed HELENGINE_BUILD_INVOCATION_ID reached the editor."
        }
        if (Test-Path -LiteralPath $InvalidInvocationOutputPath) {
            throw "A malformed HELENGINE_BUILD_INVOCATION_ID mutated output '$InvalidInvocationOutputPath'."
        }

        $RunningState = Get-Content -LiteralPath $RunningStateCapturePath -Raw | ConvertFrom-Json
        Assert-BuildStateDocument `
            -State $RunningState `
            -ExpectedStatus "running" `
            -ExpectedExitCode $null `
            -ExpectedProjectPath $CanonicalProjectPath

        $StateWriteCallSiteCount = @(Select-String -LiteralPath $WrapperPath -Pattern '^\s*Write-BuildPlatformState\b').Count
        if ($StateWriteCallSiteCount -ne 2) {
            throw "The wrapper contained $StateWriteCallSiteCount state-writer call sites instead of running plus terminal."
        }

        $EditorCacheSentinelPath = Join-Path $ExpectedEditorCachePath "state-failure-sentinel.txt"
        $PlatformCacheSentinelPath = Join-Path $ExpectedPlatformCachePath "state-failure-sentinel.txt"
        $null = New-Item -ItemType Directory -Path (Split-Path -Parent $EditorCacheSentinelPath) -Force
        $null = New-Item -ItemType Directory -Path (Split-Path -Parent $PlatformCacheSentinelPath) -Force
        Set-Content -LiteralPath $EditorCacheSentinelPath -Value "editor cache sentinel" -NoNewline
        Set-Content -LiteralPath $PlatformCacheSentinelPath -Value "platform cache sentinel" -NoNewline
        Remove-Item -LiteralPath $RunningStateCapturePath -Force
        $env:HELENGINE_WORKSPACE_EDITOR_EXIT_CODE = "37"
        try {
            $FailureResult = Invoke-ControlledWrapper -CachePath $CacheRootPath
        } finally {
            $env:HELENGINE_WORKSPACE_EDITOR_EXIT_CODE = $null
        }
        if ($FailureResult.ExitCode -ne 37) {
            throw "The failing fake editor exit code changed from 37 to $($FailureResult.ExitCode). $($FailureResult.Output -join [Environment]::NewLine)"
        }
        $FailedState = Get-Content -LiteralPath $ExpectedStatePath -Raw | ConvertFrom-Json
        Assert-BuildStateDocument `
            -State $FailedState `
            -ExpectedStatus "failed" `
            -ExpectedExitCode 37 `
            -ExpectedProjectPath $CanonicalProjectPath
        $FailureRunningState = Get-Content -LiteralPath $RunningStateCapturePath -Raw | ConvertFrom-Json
        Assert-BuildStateDocument `
            -State $FailureRunningState `
            -ExpectedStatus "running" `
            -ExpectedExitCode $null `
            -ExpectedProjectPath $CanonicalProjectPath

        foreach ($PreservedPath in @(
                (Join-Path $CanonicalProjectRootPath "fake-editor-mutation.txt"),
                (Join-Path $CanonicalOutputPath "partial-output.txt"),
                $EditorCacheSentinelPath,
                $PlatformCacheSentinelPath,
                $ExpectedLockPath
            )) {
            if (-not (Test-Path -LiteralPath $PreservedPath)) {
                throw "The failed direct-output build removed '$PreservedPath'."
            }
        }

        $TerminalStateFailureErrors = New-Object System.Collections.ArrayList
        foreach ($TerminalStateFailureCase in @(
                [pscustomobject]@{
                    Name = "successful native build with terminal state failure"
                    NativeExitCode = $null
                    ExpectedWrapperExitCode = 10
                },
                [pscustomobject]@{
                    Name = "failed native build with terminal state failure"
                    NativeExitCode = "37"
                    ExpectedWrapperExitCode = 37
                }
            )) {
            $EnvironmentCleanupCapturePath = Join-Path $TestRootPath (
                "environment-cleanup-" + $TerminalStateFailureCase.ExpectedWrapperExitCode + ".json"
            )
            foreach ($ResetPath in @(
                    $RunningStateCapturePath,
                    (Join-Path $CanonicalProjectRootPath "fake-editor-mutation.txt"),
                    (Join-Path $CanonicalOutputPath "partial-output.txt")
                )) {
                if (Test-Path -LiteralPath $ResetPath) {
                    Remove-Item -LiteralPath $ResetPath -Force
                }
            }

            $env:HELENGINE_WORKSPACE_SABOTAGE_STATE = "1"
            $env:HELENGINE_WORKSPACE_EDITOR_EXIT_CODE = $TerminalStateFailureCase.NativeExitCode
            try {
                $TerminalStateFailureResult = Invoke-ControlledWrapper `
                    -CachePath $CacheRootPath `
                    -CleanupCapturePath $EnvironmentCleanupCapturePath
            } finally {
                $env:HELENGINE_WORKSPACE_SABOTAGE_STATE = $null
                $env:HELENGINE_WORKSPACE_EDITOR_EXIT_CODE = $null
            }

            if ($TerminalStateFailureResult.ExitCode -ne $TerminalStateFailureCase.ExpectedWrapperExitCode) {
                $null = $TerminalStateFailureErrors.Add(
                    "$($TerminalStateFailureCase.Name) exited $($TerminalStateFailureResult.ExitCode) instead of $($TerminalStateFailureCase.ExpectedWrapperExitCode)."
                )
            }
            if (($TerminalStateFailureResult.Output -join [Environment]::NewLine) -notmatch 'Failed to write terminal build state') {
                $null = $TerminalStateFailureErrors.Add(
                    "$($TerminalStateFailureCase.Name) did not print the terminal state-write diagnostic."
                )
            }
            try {
                Assert-EnvironmentCleanupCapture -CapturePath $EnvironmentCleanupCapturePath
            } catch {
                $null = $TerminalStateFailureErrors.Add($_.Exception.Message)
            }

            foreach ($PreservedPath in @(
                    (Join-Path $CanonicalProjectRootPath "fake-editor-mutation.txt"),
                    (Join-Path $CanonicalOutputPath "partial-output.txt"),
                    $EditorCacheSentinelPath,
                    $PlatformCacheSentinelPath,
                    $ExpectedLockPath
                )) {
                if (-not (Test-Path -LiteralPath $PreservedPath)) {
                    $null = $TerminalStateFailureErrors.Add(
                        "$($TerminalStateFailureCase.Name) rolled back or removed '$PreservedPath'."
                    )
                }
            }
            if (-not (Test-Path -LiteralPath $ExpectedStatePath -PathType Container)) {
                $null = $TerminalStateFailureErrors.Add(
                    "$($TerminalStateFailureCase.Name) did not leave the sabotaged state-path directory in place."
                )
            }

            if (Test-Path -LiteralPath $ExpectedStatePath) {
                Remove-Item -LiteralPath $ExpectedStatePath -Recurse -Force
            }
            $RecoveryResult = Invoke-ControlledWrapper `
                -CachePath $CacheRootPath `
                -LockTimeoutMilliseconds 1500
            if ($RecoveryResult.ExitCode -ne 0) {
                $null = $TerminalStateFailureErrors.Add(
                    "$($TerminalStateFailureCase.Name) did not release the project lock for recovery: exit $($RecoveryResult.ExitCode)."
                )
            }
        }

        $MissingDotNetCleanupCapturePath = Join-Path $TestRootPath "environment-cleanup-missing-dotnet.json"
        $env:HELENGINE_DOTNET_EXECUTABLE_PATH = Join-Path $FakeToolsPath "missing-dotnet.cmd"
        try {
            $MissingDotNetResult = Invoke-ControlledWrapper `
                -CachePath $CacheRootPath `
                -CleanupCapturePath $MissingDotNetCleanupCapturePath
        } finally {
            $env:HELENGINE_DOTNET_EXECUTABLE_PATH = Join-Path $FakeToolsPath "dotnet.cmd"
        }
        if ($MissingDotNetResult.ExitCode -ne 10) {
            $null = $TerminalStateFailureErrors.Add(
                "The post-running missing-dotnet exception exited $($MissingDotNetResult.ExitCode) instead of wrapper code 10."
            )
        } else {
            $MissingDotNetState = Get-Content -LiteralPath $ExpectedStatePath -Raw | ConvertFrom-Json
            try {
                Assert-BuildStateDocument `
                    -State $MissingDotNetState `
                    -ExpectedStatus "failed" `
                    -ExpectedExitCode 10 `
                    -ExpectedProjectPath $CanonicalProjectPath
            } catch {
                $null = $TerminalStateFailureErrors.Add($_.Exception.Message)
            }
        }
        try {
            Assert-EnvironmentCleanupCapture -CapturePath $MissingDotNetCleanupCapturePath
        } catch {
            $null = $TerminalStateFailureErrors.Add($_.Exception.Message)
        }
        $MissingDotNetRecoveryResult = Invoke-ControlledWrapper `
            -CachePath $CacheRootPath `
            -LockTimeoutMilliseconds 1500
        if ($MissingDotNetRecoveryResult.ExitCode -ne 0) {
            $null = $TerminalStateFailureErrors.Add(
                "The post-running missing-dotnet exception did not release the project lock for recovery: exit $($MissingDotNetRecoveryResult.ExitCode)."
            )
        }

        if ($TerminalStateFailureErrors.Count -ne 0) {
            throw ("Terminal state failure regressions failed: " + ($TerminalStateFailureErrors -join " | "))
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
        $env:HELENGINE_WORKSPACE_EXPECTED_STATE_PATH = $OriginalExpectedStatePath
        $env:HELENGINE_WORKSPACE_RUNNING_STATE_CAPTURE = $OriginalRunningStateCapturePath
        $env:HELENGINE_WORKSPACE_EDITOR_EXIT_CODE = $OriginalEditorExitCode
        $env:HELENGINE_WORKSPACE_SABOTAGE_STATE = $OriginalSabotageState
        $env:HELENGINE_WORKSPACE_ROBOCOPY_MARKER = $OriginalRobocopyMarkerPath
        $env:HELENGINE_DOTNET_EXECUTABLE_PATH = $OriginalDotNetExecutablePath
    }

    Write-Output "WORKSPACE_TEST_PASS"
} finally {
    if (Test-Path -LiteralPath $TestRootPath) {
        Remove-Item -LiteralPath $TestRootPath -Recurse -Force
    }
}

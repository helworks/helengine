[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepositoryRootPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$WrapperPath = Join-Path $RepositoryRootPath "scripts\build-platform.ps1"
$FixtureRootPath = Join-Path $PSScriptRoot "fixtures\build-platform-smoke-project"
$WindowsPlatformSourcePath = "C:\dev\helworks\helengine-windows"
$WindowsBuilderAssemblyPath = Join-Path $WindowsPlatformSourcePath "builder\bin\Debug\net9.0\helengine.windows.builder.dll"
$CodegenToolPath = "C:\dev\helworks\csharpcodegen\codegen\bin\Release\net9.0\codegen.exe"
$TemporaryRootPath = [System.IO.Path]::GetFullPath("C:\tmp")
$TestRootPath = Join-Path $TemporaryRootPath ("hbp-" + [Guid]::NewGuid().ToString("N"))
$ProjectRootPath = Join-Path $TestRootPath "authored-project"
$ProjectPath = Join-Path $ProjectRootPath "project.heproj"
$CacheRootPath = Join-Path $TestRootPath "cache"
$OutputRootPath = Join-Path $TestRootPath "output"
$EngineUserSettingsRootPath = Join-Path $TestRootPath "engine-user-settings"
$StatePath = Join-Path $OutputRootPath ".helengine-build-state.json"
$ExecutablePath = Join-Path $OutputRootPath "helengine_windows.exe"
$OriginalEngineUserSettingsRoot = $env:HELENGINE_ENGINE_USER_SETTINGS_ROOT

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

function Invoke-Wrapper {
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
        $OutputRootPath,
        "-Configuration",
        "Debug",
        "-BuildProfile",
        "release",
        "-CacheRoot",
        $CacheRootPath
    )

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
        Output = @($InvocationOutput | ForEach-Object { [string]$_ })
    }
}

function Get-OutputValue {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Output,

        [Parameter(Mandatory = $true)]
        [string]$Prefix
    )

    $Lines = @($Output | Where-Object { $_.StartsWith($Prefix, [System.StringComparison]::OrdinalIgnoreCase) })
    if ($Lines.Count -ne 1) {
        throw "Expected one '$Prefix' line, but found $($Lines.Count). $($Output -join [Environment]::NewLine)"
    }

    return [System.IO.Path]::GetFullPath($Lines[0].Substring($Prefix.Length).Trim())
}

function Assert-WrapperSuccess {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Result,

        [Parameter(Mandatory = $true)]
        [string]$InvocationName,

        [Parameter(Mandatory = $true)]
        [string]$PlatformCachePath
    )

    if ($Result.ExitCode -ne 0) {
        $NativeLogPaths = @(
            (Join-Path -Path $PlatformCachePath -ChildPath "native\native\native-configure.log"),
            (Join-Path -Path $PlatformCachePath -ChildPath "native\native\native-build.log")
        )
        $NativeLogs = foreach ($NativeLogPath in $NativeLogPaths) {
            if (Test-Path -LiteralPath $NativeLogPath -PathType Leaf) {
                "Native log '$NativeLogPath': $([Environment]::NewLine)$(Get-Content -LiteralPath $NativeLogPath -Tail 120 | Out-String -Width 240)"
            } else {
                "Native log was not found at '$NativeLogPath'."
            }
        }
        throw "$InvocationName failed with exit code $($Result.ExitCode). $($Result.Output -join [Environment]::NewLine) $($NativeLogs -join [Environment]::NewLine)"
    }

    $CanonicalProjectPath = [System.IO.Path]::GetFullPath($ProjectPath)
    if (-not ($Result.Output -contains "Authored project: $CanonicalProjectPath")) {
        throw "$InvocationName did not identify the disposable authored project. $($Result.Output -join [Environment]::NewLine)"
    }
}

function Assert-CurrentSucceededState {
    param(
        [Parameter(Mandatory = $true)]
        [DateTimeOffset]$InvocationStartedUtc
    )

    if (-not (Test-Path -LiteralPath $StatePath -PathType Leaf)) {
        throw "The build did not write '$StatePath'."
    }

    $State = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
    $StateStartedUtc = [DateTimeOffset]::Parse([string]$State.startedUtc)
    $StateCompletedUtc = [DateTimeOffset]::Parse([string]$State.completedUtc)
    if ($State.status -cne "succeeded" -or [int]$State.exitCode -ne 0) {
        throw "The current build state was status='$($State.status)', exitCode='$($State.exitCode)'."
    }
    if (($State.projectPath -cne [System.IO.Path]::GetFullPath($ProjectPath)) -or
        ($State.platform -cne "windows") -or
        ($State.buildProfile -cne "release") -or
        ($State.configuration -cne "Debug")) {
        throw "The current succeeded state identity did not match the disposable native smoke build."
    }
    if ($StateStartedUtc -lt $InvocationStartedUtc -or $StateCompletedUtc -lt $StateStartedUtc) {
        throw "The current succeeded state was not produced by the latest wrapper invocation."
    }
}

try {
    if (-not (Test-Path -LiteralPath 'C:\dev\helworks\helengine-windows' -PathType Container)) { throw 'Windows platform source is required.' }
    if ($null -eq (Get-Command cmake.exe -ErrorAction SilentlyContinue)) { throw 'cmake.exe is required.' }
    if (-not (Test-Path -LiteralPath $WindowsBuilderAssemblyPath -PathType Leaf)) {
        throw "Windows builder assembly is required at '$WindowsBuilderAssemblyPath'."
    }
    if (-not (Test-Path -LiteralPath $CodegenToolPath -PathType Leaf)) {
        throw "External codegen tool is required at '$CodegenToolPath'."
    }
    if (-not (Test-Path -LiteralPath $TemporaryRootPath -PathType Container)) {
        throw "Short native smoke temporary root '$TemporaryRootPath' is required."
    }
    if (-not (Test-StrictDescendantPath -ParentPath $TemporaryRootPath -CandidatePath $TestRootPath)) {
        throw "Disposable native smoke root '$TestRootPath' must be a strict descendant of '$TemporaryRootPath'."
    }
    if (Test-Path -LiteralPath $TestRootPath) {
        throw "Disposable native smoke root '$TestRootPath' unexpectedly already exists."
    }

    $FixtureRelativePaths = @(
        "project.heproj",
        "settings\platforms.json",
        "user_settings\build_config.json",
        "assets\scenes\SmokeScene.helen",
        "assets\codebase\smoke\code.module.json"
    )
    foreach ($RelativePath in $FixtureRelativePaths) {
        $SourcePath = Join-Path $FixtureRootPath $RelativePath
        $DestinationPath = Join-Path $ProjectRootPath $RelativePath
        $null = New-Item -ItemType Directory -Path (Split-Path -Parent $DestinationPath) -Force
        Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath
    }

    $ProjectDefinition = Get-Content -LiteralPath $ProjectPath -Raw | ConvertFrom-Json
    $ProjectDefinition.supportedPlatforms = @("windows")
    $ProjectDefinition | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ProjectPath

    $PlatformsPath = Join-Path $ProjectRootPath "settings\platforms.json"
    $PlatformsDefinition = Get-Content -LiteralPath $PlatformsPath -Raw | ConvertFrom-Json
    $PlatformsDefinition.supportedPlatforms = @("windows")
    $PlatformsDefinition | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $PlatformsPath

    $BuildConfigurationPath = Join-Path $ProjectRootPath "user_settings\build_config.json"
    $BuildConfiguration = Get-Content -LiteralPath $BuildConfigurationPath -Raw | ConvertFrom-Json
    if ($BuildConfiguration.platforms.Count -ne 1) {
        throw "The native smoke fixture must contain exactly one platform configuration."
    }
    $PlatformConfiguration = $BuildConfiguration.platforms[0]
    $PlatformConfiguration.platformId = "windows"
    $PlatformConfiguration.selectedBuildProfileId = "release"
    $PlatformConfiguration.selectedGraphicsProfileId = "directx11"
    $PlatformConfiguration.selectedCodegenProfileId = "default"
    $PlatformConfiguration.selectedStorageProfileId = "loose-files"
    $PlatformConfiguration.selectedMediaProfileId = "windows-install-tree"
    $BuildConfiguration | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $BuildConfigurationPath

    $EncodedScenePayload = (Get-Content -LiteralPath (Join-Path $ProjectRootPath "assets\scenes\SmokeScene.helen") -Raw).Trim()
    [System.IO.File]::WriteAllBytes(
        (Join-Path $ProjectRootPath "assets\scenes\SmokeScene.helen"),
        [Convert]::FromBase64String($EncodedScenePayload))

    $null = New-Item -ItemType Directory -Path $EngineUserSettingsRootPath -Force
    [ordered]@{
        platforms = @(
            [ordered]@{
                engineVersion = "1.0.0-smoke"
                platformId = "windows"
                displayName = "Native Windows Cache Smoke"
                builderAssemblyPath = $WindowsBuilderAssemblyPath
                playerSourceRootPath = $WindowsPlatformSourcePath
                codegenToolPath = $CodegenToolPath
            }
        )
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $EngineUserSettingsRootPath "platforms.json")

    $env:HELENGINE_ENGINE_USER_SETTINGS_ROOT = $EngineUserSettingsRootPath
    $FirstResult = Invoke-Wrapper
    $FirstPlatformCachePath = Get-OutputValue -Output $FirstResult.Output -Prefix "Platform cache:"
    Write-Output "Platform cache: $FirstPlatformCachePath"
    Write-Output "Platform cache character count: $($FirstPlatformCachePath.Length)"
    Assert-WrapperSuccess -Result $FirstResult -InvocationName "First native wrapper invocation" -PlatformCachePath $FirstPlatformCachePath

    if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
        throw "The first native build did not produce '$ExecutablePath'."
    }
    if ((Get-Item -LiteralPath $ExecutablePath).Length -le 0) {
        throw "The first native build produced an empty executable at '$ExecutablePath'."
    }

    $SecondInvocationStartedUtc = [DateTimeOffset]::UtcNow
    $SecondResult = Invoke-Wrapper
    $SecondPlatformCachePath = Get-OutputValue -Output $SecondResult.Output -Prefix "Platform cache:"
    Assert-WrapperSuccess -Result $SecondResult -InvocationName "Second native wrapper invocation" -PlatformCachePath $SecondPlatformCachePath
    if ($FirstPlatformCachePath -cne $SecondPlatformCachePath) {
        throw "The platform cache path changed from '$FirstPlatformCachePath' to '$SecondPlatformCachePath'."
    }

    if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
        throw "The second native build did not produce '$ExecutablePath'."
    }
    if ((Get-Item -LiteralPath $ExecutablePath).Length -le 0) {
        throw "The second native build produced an empty executable at '$ExecutablePath'."
    }
    Assert-CurrentSucceededState -InvocationStartedUtc $SecondInvocationStartedUtc

    Write-Output "NATIVE_CACHE_SMOKE_TEST_PASS"
} finally {
    $env:HELENGINE_ENGINE_USER_SETTINGS_ROOT = $OriginalEngineUserSettingsRoot
    if (Test-Path -LiteralPath $TestRootPath) {
        if (-not (Test-StrictDescendantPath -ParentPath $TemporaryRootPath -CandidatePath $TestRootPath)) {
            throw "Refusing to remove disposable native smoke root '$TestRootPath' outside temporary root '$TemporaryRootPath'."
        }
        Remove-Item -LiteralPath $TestRootPath -Recurse -Force
    }
}

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepositoryRootPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$WrapperPath = Join-Path $RepositoryRootPath "scripts\build-platform.ps1"
$FixtureRootPath = Join-Path $PSScriptRoot "fixtures\build-platform-smoke-project"
$BuilderProjectPath = Join-Path $PSScriptRoot "fixtures\build-platform-smoke-builder\helengine.buildplatform.smokebuilder.csproj"
$CodegenProjectPath = "C:\dev\helworks\csharpcodegen\codegen\codegen.csproj"
$CodegenToolPath = "C:\dev\helworks\csharpcodegen\codegen\bin\Release\net9.0\codegen.exe"
$TestBuildRootPath = Join-Path ([System.IO.Path]::GetTempPath()) "helengine-build-platform-tests"
$TestRootPath = Join-Path $TestBuildRootPath ("build-platform-real-editor-smoke-" + [Guid]::NewGuid().ToString("N"))
$ProjectRootPath = Join-Path $TestRootPath "authored-project"
$ProjectPath = Join-Path $ProjectRootPath "project.heproj"
$CacheRootPath = Join-Path $TestRootPath "cache"
$OutputRootPath = Join-Path $TestRootPath "output"
$EngineUserSettingsRootPath = Join-Path $TestRootPath "engine-user-settings"
$StatePath = Join-Path $OutputRootPath ".helengine-build-state.json"
$SmokeMarkerPath = Join-Path $OutputRootPath "smoke-build.txt"
$OriginalEngineUserSettingsRoot = $env:HELENGINE_ENGINE_USER_SETTINGS_ROOT

function Invoke-DotNetBuild {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,

        [Parameter(Mandatory = $true)]
        [string]$Configuration
    )

    $BuildOutput = @(& dotnet build $ProjectPath -c $Configuration 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed for '$ProjectPath' with exit code $LASTEXITCODE. $($BuildOutput -join [Environment]::NewLine)"
    }
}

function Invoke-RealWrapper {
    $Arguments = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $WrapperPath,
        "-Project",
        $ProjectPath,
        "-Platform",
        "smoke",
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

    $Line = @($Output | Where-Object { $_.StartsWith($Prefix, [StringComparison]::OrdinalIgnoreCase) })
    if ($Line.Count -ne 1) {
        throw "Expected one '$Prefix' line, but found $($Line.Count). $($Output -join [Environment]::NewLine)"
    }

    return [System.IO.Path]::GetFullPath($Line[0].Substring($Prefix.Length).Trim())
}

function Get-PublishOutputPath {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Output
    )

    $PublishingLine = @($Output | Where-Object { $_.StartsWith("Publishing: ", [StringComparison]::OrdinalIgnoreCase) })
    if ($PublishingLine.Count -ne 1) {
        throw "Expected one Publishing line, but found $($PublishingLine.Count)."
    }

    $Match = [regex]::Match($PublishingLine[0], '(?:^|\s)-o\s+(?:"([^"]+)"|(\S+))')
    if (-not $Match.Success) {
        throw "Publishing line did not expose an output path: '$($PublishingLine[0])'."
    }

    $PathValue = if ($Match.Groups[1].Success) { $Match.Groups[1].Value } else { $Match.Groups[2].Value }
    return [System.IO.Path]::GetFullPath($PathValue)
}

function Get-SingleStablePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LeafName
    )

    $Matches = @(Get-ChildItem -LiteralPath $CacheRootPath -Recurse -Directory |
        Where-Object { $_.Name -ceq $LeafName })
    if ($Matches.Count -ne 1) {
        throw "Expected one stable '$LeafName' directory, but found $($Matches.Count)."
    }

    return $Matches[0].FullName
}

function Assert-WrapperSuccess {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Result,

        [Parameter(Mandatory = $true)]
        [string]$InvocationName
    )

    if ($Result.ExitCode -ne 0) {
        throw "$InvocationName failed with exit code $($Result.ExitCode). $($Result.Output -join [Environment]::NewLine)"
    }

    $CanonicalProjectPath = [System.IO.Path]::GetFullPath($ProjectPath)
    if (-not ($Result.Output -contains "Authored project: $CanonicalProjectPath")) {
        throw "$InvocationName did not identify the disposable authored project. $($Result.Output -join [Environment]::NewLine)"
    }

    $ExecutingLine = @($Result.Output | Where-Object { $_.StartsWith("Executing: ", [StringComparison]::OrdinalIgnoreCase) })
    if ($ExecutingLine.Count -ne 1 -or $ExecutingLine[0] -notmatch [regex]::Escape("--project $CanonicalProjectPath")) {
        throw "$InvocationName did not build the disposable authored project directly. $($ExecutingLine -join [Environment]::NewLine)"
    }
}

try {
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

    $EncodedScenePayload = (Get-Content -LiteralPath (Join-Path $ProjectRootPath "assets\scenes\SmokeScene.helen") -Raw).Trim()
    [System.IO.File]::WriteAllBytes(
        (Join-Path $ProjectRootPath "assets\scenes\SmokeScene.helen"),
        [Convert]::FromBase64String($EncodedScenePayload))

    Invoke-DotNetBuild -ProjectPath $CodegenProjectPath -Configuration "Release"
    Invoke-DotNetBuild -ProjectPath $BuilderProjectPath -Configuration "Debug"

    $BuilderAssemblyPath = [System.IO.Path]::GetFullPath(
        (Join-Path (Split-Path -Parent $BuilderProjectPath) "bin\Debug\net9.0\helengine.buildplatform.smokebuilder.dll"))
    if (-not (Test-Path -LiteralPath $BuilderAssemblyPath -PathType Leaf)) {
        throw "Smoke builder assembly was not found at '$BuilderAssemblyPath'."
    }
    if (-not (Test-Path -LiteralPath $CodegenToolPath -PathType Leaf)) {
        throw "Release codegen tool was not found at '$CodegenToolPath'."
    }

    $null = New-Item -ItemType Directory -Path $EngineUserSettingsRootPath -Force
    [ordered]@{
        platforms = @(
            [ordered]@{
                engineVersion = "1.0.0-smoke"
                platformId = "smoke"
                displayName = "Build Platform Smoke"
                builderAssemblyPath = $BuilderAssemblyPath
                playerSourceRootPath = $ProjectRootPath
                codegenToolPath = $CodegenToolPath
            }
        )
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $EngineUserSettingsRootPath "platforms.json")

    $env:HELENGINE_ENGINE_USER_SETTINGS_ROOT = $EngineUserSettingsRootPath
    $FirstResult = Invoke-RealWrapper
    Assert-WrapperSuccess -Result $FirstResult -InvocationName "First wrapper invocation"

    if (-not (Test-Path -LiteralPath $SmokeMarkerPath -PathType Leaf)) {
        throw "The first build did not write '$SmokeMarkerPath' directly."
    }
    $FirstEditorPublishPath = Get-PublishOutputPath -Output $FirstResult.Output
    $FirstEditorCachePath = Get-OutputValue -Output $FirstResult.Output -Prefix "Editor cache:"
    $FirstGeneratedDotNetPath = Get-SingleStablePath -LeafName "generated-dotnet"
    $FirstNativePath = [System.IO.Path]::GetFullPath((Get-Content -LiteralPath $SmokeMarkerPath -Raw).Trim())

    $SecondInvocationStartedUtc = [DateTimeOffset]::UtcNow
    $SecondResult = Invoke-RealWrapper
    Assert-WrapperSuccess -Result $SecondResult -InvocationName "Second wrapper invocation"

    $SecondEditorPublishPath = Get-PublishOutputPath -Output $SecondResult.Output
    $SecondEditorCachePath = Get-OutputValue -Output $SecondResult.Output -Prefix "Editor cache:"
    $SecondGeneratedDotNetPath = Get-SingleStablePath -LeafName "generated-dotnet"
    $SecondNativePath = [System.IO.Path]::GetFullPath((Get-Content -LiteralPath $SmokeMarkerPath -Raw).Trim())
    foreach ($StablePath in @(
            @($FirstEditorPublishPath, $SecondEditorPublishPath, "editor publish"),
            @($FirstEditorCachePath, $SecondEditorCachePath, "editor cache"),
            @($FirstGeneratedDotNetPath, $SecondGeneratedDotNetPath, "generated-dotnet"),
            @($FirstNativePath, $SecondNativePath, "native")
        )) {
        if ($StablePath[0] -cne $StablePath[1]) {
            throw "The $($StablePath[2]) path changed from '$($StablePath[0])' to '$($StablePath[1])'."
        }
    }

    if (-not (Test-Path -LiteralPath $SmokeMarkerPath -PathType Leaf)) {
        throw "The second build did not preserve the direct smoke output."
    }
    if (-not (Test-Path -LiteralPath $StatePath -PathType Leaf)) {
        throw "The second build did not write '$StatePath'."
    }

    $State = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
    $StateStartedUtc = [DateTimeOffset]::Parse([string]$State.startedUtc)
    $StateCompletedUtc = [DateTimeOffset]::Parse([string]$State.completedUtc)
    if ($State.status -cne "succeeded" -or [int]$State.exitCode -ne 0) {
        throw "The current build state was status='$($State.status)', exitCode='$($State.exitCode)'."
    }
    if (($State.projectPath -cne [System.IO.Path]::GetFullPath($ProjectPath)) -or
        ($State.platform -cne "smoke") -or
        ($State.buildProfile -cne "release") -or
        ($State.configuration -cne "Debug")) {
        throw "The current build state identity did not match the disposable smoke build."
    }
    if ($StateStartedUtc -lt $SecondInvocationStartedUtc -or $StateCompletedUtc -lt $StateStartedUtc) {
        throw "The current succeeded state was not produced by the second invocation."
    }

    $GuidLikeInvocationDirectories = @(Get-ChildItem -LiteralPath $CacheRootPath -Recurse -Directory |
        Where-Object { $_.Name -match '^[0-9a-f]{32}$' -and $_.Parent.Name -cne "projects" })
    if ($GuidLikeInvocationDirectories.Count -ne 0) {
        throw "The stable cache contains GUID-like invocation directories: $($GuidLikeInvocationDirectories.FullName -join ', ')."
    }

    $ProjectSource = Get-Content -LiteralPath $ProjectPath -Raw
    if ($ProjectSource -notmatch [regex]::Escape("AUTHORED_SOURCE_SENTINEL")) {
        throw "The authored-source fixture sentinel did not survive the direct builds."
    }

    Write-Output "REAL_EDITOR_SMOKE_TEST_PASS"
} finally {
    $env:HELENGINE_ENGINE_USER_SETTINGS_ROOT = $OriginalEngineUserSettingsRoot
    if (Test-Path -LiteralPath $TestRootPath) {
        Remove-Item -LiteralPath $TestRootPath -Recurse -Force
    }
}

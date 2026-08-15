[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$WrapperPath = Join-Path $RepositoryRoot "scripts\build-platform.ps1"
$TestBuildRootPath = "C:\dev\helworks\builds\helengine\tests"
$TestRootPath = Join-Path $TestBuildRootPath ("build-platform-profile-behavior-" + [Guid]::NewGuid().ToString("N"))
$FakeToolsPath = Join-Path $TestRootPath "fake-tools"
$CapturePath = Join-Path $TestRootPath "dotnet-invocations.txt"
$ProjectPath = Join-Path $TestRootPath "project\project.heproj"
$EditorProjectPath = Join-Path $TestRootPath "editor\editor.csproj"
$OutputPath = Join-Path $TestRootPath "output"
$CacheRootPath = Join-Path $TestRootPath "cache"
$EnvironmentCapturePath = Join-Path $TestRootPath "editor-environment.txt"

function Invoke-ControlledWrapper {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath
    )

    $WrapperOutput = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $ScriptPath `
        -Project $ProjectPath `
        -Platform "windows" `
        -Output $OutputPath `
        -Configuration "Release" `
        -BuildProfile "profiler" `
        -EditorProject $EditorProjectPath `
        -CacheRoot $CacheRootPath 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "The controlled wrapper failed with exit code $LASTEXITCODE. $WrapperOutput"
    }
}

try {
    $null = New-Item -ItemType Directory -Path $FakeToolsPath -Force
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $ProjectPath) -Force
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $EditorProjectPath) -Force
    Set-Content -LiteralPath $ProjectPath -Value "{}" -NoNewline
    Set-Content -LiteralPath $EditorProjectPath -Value "<Project />" -NoNewline
    Set-Content -LiteralPath (Join-Path $FakeToolsPath "dotnet.cmd") -Value @'
@echo off
setlocal EnableExtensions
echo %*>> "%HELENGINE_PROFILE_BEHAVIOR_CAPTURE%"
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
echo %* | findstr /C:"--build windows" >nul
if not errorlevel 1 echo %HELENGINE_BUILD_PROFILE%> "%HELENGINE_PROFILE_BEHAVIOR_ENVIRONMENT_CAPTURE%"
exit /b 0
'@ -NoNewline

    $OriginalPath = $env:PATH
    $OriginalCapturePath = $env:HELENGINE_PROFILE_BEHAVIOR_CAPTURE
    $OriginalEnvironmentCapturePath = $env:HELENGINE_PROFILE_BEHAVIOR_ENVIRONMENT_CAPTURE
    $OriginalDotNetExecutablePath = $env:HELENGINE_DOTNET_EXECUTABLE_PATH
    try {
        $env:PATH = $FakeToolsPath + ";" + $OriginalPath
        $env:HELENGINE_PROFILE_BEHAVIOR_CAPTURE = $CapturePath
        $env:HELENGINE_PROFILE_BEHAVIOR_ENVIRONMENT_CAPTURE = $EnvironmentCapturePath
        $env:HELENGINE_DOTNET_EXECUTABLE_PATH = Join-Path $FakeToolsPath "dotnet.cmd"

        Invoke-ControlledWrapper -ScriptPath $WrapperPath
    } finally {
        $env:PATH = $OriginalPath
        $env:HELENGINE_PROFILE_BEHAVIOR_CAPTURE = $OriginalCapturePath
        $env:HELENGINE_PROFILE_BEHAVIOR_ENVIRONMENT_CAPTURE = $OriginalEnvironmentCapturePath
        $env:HELENGINE_DOTNET_EXECUTABLE_PATH = $OriginalDotNetExecutablePath
    }

    $EditorInvocation = Get-Content -LiteralPath $CapturePath | Where-Object { $_ -match '--build-profile' } | Select-Object -Last 1
    if ($EditorInvocation -notmatch '--build-profile profiler') {
        throw "Expected the editor invocation to receive '--build-profile profiler', but captured '$EditorInvocation'."
    }
    $EditorBuildProfile = (Get-Content -LiteralPath $EnvironmentCapturePath -Raw).Trim()
    if ($EditorBuildProfile -cne "profiler") {
        throw "Expected the fake editor process to inherit HELENGINE_BUILD_PROFILE=profiler, but captured '$EditorBuildProfile'."
    }

    Write-Output "PROFILE_BEHAVIOR_TEST_PASS"
} finally {
    if (Test-Path -LiteralPath $TestRootPath) {
        Remove-Item -LiteralPath $TestRootPath -Recurse -Force
    }
}

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$WrapperPath = Join-Path $RepositoryRoot "scripts\build-platform.ps1"
$TestRootPath = Join-Path $RepositoryRoot ("scripts\tests\build-platform-profile-behavior-" + [Guid]::NewGuid().ToString("N"))
$FakeToolsPath = Join-Path $TestRootPath "fake-tools"
$CapturePath = Join-Path $TestRootPath "dotnet-invocations.txt"
$ProjectPath = Join-Path $TestRootPath "project\project.heproj"
$EditorProjectPath = Join-Path $TestRootPath "editor\editor.csproj"
$OutputPath = Join-Path $TestRootPath "output"
$WorkspaceTempPath = Join-Path $TestRootPath "temp"

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
        -EditorProject $EditorProjectPath 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "The controlled wrapper failed with exit code $LASTEXITCODE. $WrapperOutput"
    }
}

try {
    $null = New-Item -ItemType Directory -Path $FakeToolsPath -Force
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $ProjectPath) -Force
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $EditorProjectPath) -Force
    $null = New-Item -ItemType Directory -Path $WorkspaceTempPath -Force
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
exit /b 0
'@ -NoNewline

    $OriginalPath = $env:PATH
    $OriginalTemp = $env:TEMP
    $OriginalTmp = $env:TMP
    $OriginalCapturePath = $env:HELENGINE_PROFILE_BEHAVIOR_CAPTURE
    $OriginalDotNetExecutablePath = $env:HELENGINE_DOTNET_EXECUTABLE_PATH
    try {
        $env:PATH = $FakeToolsPath + ";" + $OriginalPath
        $env:TEMP = $WorkspaceTempPath
        $env:TMP = $WorkspaceTempPath
        $env:HELENGINE_PROFILE_BEHAVIOR_CAPTURE = $CapturePath
        $env:HELENGINE_DOTNET_EXECUTABLE_PATH = Join-Path $FakeToolsPath "dotnet.cmd"

        Invoke-ControlledWrapper -ScriptPath $WrapperPath
    } finally {
        $env:PATH = $OriginalPath
        $env:TEMP = $OriginalTemp
        $env:TMP = $OriginalTmp
        $env:HELENGINE_PROFILE_BEHAVIOR_CAPTURE = $OriginalCapturePath
        $env:HELENGINE_DOTNET_EXECUTABLE_PATH = $OriginalDotNetExecutablePath
    }

    $EditorInvocation = Get-Content -LiteralPath $CapturePath | Where-Object { $_ -match '--build-profile' } | Select-Object -Last 1
    if ($EditorInvocation -notmatch '--build-profile profiler') {
        throw "Expected the editor invocation to receive '--build-profile profiler', but captured '$EditorInvocation'."
    }

    Write-Output "PROFILE_BEHAVIOR_TEST_PASS"
} finally {
    if (Test-Path -LiteralPath $TestRootPath) {
        Remove-Item -LiteralPath $TestRootPath -Recurse -Force
    }
}

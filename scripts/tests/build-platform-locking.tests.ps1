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

$TestBuildRootPath = Join-Path ([System.IO.Path]::GetTempPath()) "helengine-build-platform-tests"
$TestRootPath = Join-Path $TestBuildRootPath ("build-platform-locking-" + [Guid]::NewGuid().ToString("N"))
$FakeToolsPath = Join-Path $TestRootPath "fake-tools"
$FakeDotNetPath = Join-Path $FakeToolsPath "dotnet.cmd"
$EditorProjectPath = Join-Path $TestRootPath "editor\editor.csproj"
$CacheRootPath = Join-Path $TestRootPath "cache"
$StartedInvocations = New-Object System.Collections.ArrayList
$InvocationEnvironmentVariableNames = @(
    "HELENGINE_DOTNET_EXECUTABLE_PATH",
    "HELENGINE_LOCK_TEST_MARKER",
    "HELENGINE_LOCK_TEST_RELEASE",
    "HELENGINE_LOCK_TEST_CHILD_PID",
    "HELENGINE_LOCK_TEST_DONE"
)

$HarnessSource = Get-Content -LiteralPath $PSCommandPath -Raw
$ProcessStartInfoEnvironmentPropertyPattern = [regex]::Escape('$StartInfo' + '.') + 'Environment(?:Variables)?\b'
if ([regex]::IsMatch($HarnessSource, $ProcessStartInfoEnvironmentPropertyPattern)) {
    throw "The locking harness must not access a ProcessStartInfo environment dictionary."
}

function ConvertTo-ProcessArgument {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Value
    )

    if ($Value.Length -eq 0) {
        return '""'
    }
    if ($Value -notmatch '[\s"]') {
        return $Value
    }

    $EscapedValue = $Value -replace '(\\*)"', '$1$1\"'
    $EscapedValue = $EscapedValue -replace '(\\+)$', '$1$1'
    return '"' + $EscapedValue + '"'
}

function New-TestProject {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $ProjectPath = Join-Path $TestRootPath ("projects\" + $Name + "\project.heproj")
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $ProjectPath) -Force
    Set-Content -LiteralPath $ProjectPath -Value "{}" -NoNewline
    return $ProjectPath
}

function New-InvocationControl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,

        [Parameter()]
        [switch]$Released,

        [Parameter()]
        [TimeSpan]$LockTimeout = [TimeSpan]::FromSeconds(20)
    )

    $ControlRootPath = Join-Path $TestRootPath ("controls\" + $Name)
    $null = New-Item -ItemType Directory -Path $ControlRootPath -Force
    $Control = [pscustomobject]@{
        Name = $Name
        ProjectPath = $ProjectPath
        OutputPath = Join-Path $TestRootPath ("outputs\" + $Name)
        MarkerPath = Join-Path $ControlRootPath "dotnet-reached.marker"
        ReleasePath = Join-Path $ControlRootPath "release.marker"
        ChildProcessIdPath = Join-Path $ControlRootPath "child-process-id.txt"
        DonePath = Join-Path $ControlRootPath "dotnet-finished.marker"
        LockTimeout = $LockTimeout
        StartedUtc = $null
        Process = $null
    }
    if ($Released) {
        Set-Content -LiteralPath $Control.ReleasePath -Value "released" -NoNewline
    }
    return $Control
}

function Start-WrapperInvocation {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Control
    )

    $Arguments = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $WrapperPath,
        "-Project",
        $Control.ProjectPath,
        "-Platform",
        "windows",
        "-Output",
        $Control.OutputPath,
        "-Configuration",
        "Release",
        "-BuildProfile",
        "profiler",
        "-EditorProject",
        $EditorProjectPath,
        "-CacheRoot",
        $CacheRootPath,
        "-LockTimeout",
        $Control.LockTimeout.ToString("c")
    )

    $StartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $StartInfo.FileName = "powershell.exe"
    $StartInfo.Arguments = (($Arguments | ForEach-Object { ConvertTo-ProcessArgument -Value $_ }) -join " ")
    $StartInfo.UseShellExecute = $false
    $StartInfo.CreateNoWindow = $true
    $StartInfo.RedirectStandardOutput = $true
    $StartInfo.RedirectStandardError = $true

    $Process = New-Object System.Diagnostics.Process
    $Process.StartInfo = $StartInfo
    $InvocationEnvironmentValues = @{
        HELENGINE_DOTNET_EXECUTABLE_PATH = $FakeDotNetPath
        HELENGINE_LOCK_TEST_MARKER = $Control.MarkerPath
        HELENGINE_LOCK_TEST_RELEASE = $Control.ReleasePath
        HELENGINE_LOCK_TEST_CHILD_PID = $Control.ChildProcessIdPath
        HELENGINE_LOCK_TEST_DONE = $Control.DonePath
    }
    $SavedInvocationEnvironmentState = @{}
    foreach ($EnvironmentVariableName in $InvocationEnvironmentVariableNames) {
        $EnvironmentVariableValue = [System.Environment]::GetEnvironmentVariable(
            $EnvironmentVariableName,
            [System.EnvironmentVariableTarget]::Process)
        $SavedInvocationEnvironmentState[$EnvironmentVariableName] = [pscustomobject]@{
            Exists = $null -ne $EnvironmentVariableValue
            Value = $EnvironmentVariableValue
        }
    }

    try {
        foreach ($EnvironmentVariableName in $InvocationEnvironmentVariableNames) {
            [System.Environment]::SetEnvironmentVariable(
                $EnvironmentVariableName,
                $InvocationEnvironmentValues[$EnvironmentVariableName],
                [System.EnvironmentVariableTarget]::Process)
        }
        if (-not $Process.Start()) {
            $Process.Dispose()
            throw "Wrapper '$($Control.Name)' failed to start."
        }
    } finally {
        foreach ($EnvironmentVariableName in $InvocationEnvironmentVariableNames) {
            $SavedEnvironmentVariable = $SavedInvocationEnvironmentState[$EnvironmentVariableName]
            $RestoredEnvironmentVariableValue = if ($SavedEnvironmentVariable.Exists) {
                $SavedEnvironmentVariable.Value
            } else {
                $null
            }
            [System.Environment]::SetEnvironmentVariable(
                $EnvironmentVariableName,
                $RestoredEnvironmentVariableValue,
                [System.EnvironmentVariableTarget]::Process)

            $ActualRestoredEnvironmentVariableValue = [System.Environment]::GetEnvironmentVariable(
                $EnvironmentVariableName,
                [System.EnvironmentVariableTarget]::Process)
            if (($SavedEnvironmentVariable.Exists -and
                    $ActualRestoredEnvironmentVariableValue -cne $SavedEnvironmentVariable.Value) -or
                (-not $SavedEnvironmentVariable.Exists -and
                    $null -ne $ActualRestoredEnvironmentVariableValue)) {
                throw "The locking harness did not restore inherited '$EnvironmentVariableName'."
            }
        }
    }

    $Control.Process = $Process
    $Control.StartedUtc = [DateTime]::UtcNow
    $null = $StartedInvocations.Add($Control)
    return $Control
}

function Wait-ForPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Description,

        [Parameter()]
        [int]$TimeoutMilliseconds = 10000
    )

    $Stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($Stopwatch.ElapsedMilliseconds -lt $TimeoutMilliseconds) {
        if (Test-Path -LiteralPath $Path) {
            return
        }
        Start-Sleep -Milliseconds 50
    }
    throw "Timed out waiting for $Description at '$Path'."
}

function Release-Invocation {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Control
    )

    if (-not (Test-Path -LiteralPath $Control.ReleasePath)) {
        Set-Content -LiteralPath $Control.ReleasePath -Value "released" -NoNewline
    }
}

function Complete-Invocation {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Control
    )

    $ElapsedMilliseconds = [int]([DateTime]::UtcNow - $Control.StartedUtc).TotalMilliseconds
    $RemainingMilliseconds = [Math]::Max(1, 20000 - $ElapsedMilliseconds)
    if (-not $Control.Process.WaitForExit($RemainingMilliseconds)) {
        $Control.Process.Kill()
        $Control.Process.WaitForExit()
        throw "Wrapper '$($Control.Name)' exceeded the 20-second process cap."
    }

    return [pscustomobject]@{
        ExitCode = $Control.Process.ExitCode
        StandardOutput = $Control.Process.StandardOutput.ReadToEnd()
        StandardError = $Control.Process.StandardError.ReadToEnd()
    }
}

function Assert-SuccessfulInvocation {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Control
    )

    $Result = Complete-Invocation -Control $Control
    if ($Result.ExitCode -ne 0) {
        throw "Wrapper '$($Control.Name)' failed with exit code $($Result.ExitCode). stdout: $($Result.StandardOutput) stderr: $($Result.StandardError)"
    }
    return $Result
}

try {
    $null = New-Item -ItemType Directory -Path $FakeToolsPath -Force
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $EditorProjectPath) -Force
    Set-Content -LiteralPath $EditorProjectPath -Value "<Project />" -NoNewline
    Set-Content -LiteralPath $FakeDotNetPath -Value @'
@echo off
setlocal EnableExtensions
> "%HELENGINE_LOCK_TEST_MARKER%" echo reached
powershell.exe -NoProfile -Command "$PID | Set-Content -LiteralPath $env:HELENGINE_LOCK_TEST_CHILD_PID -NoNewline; while (-not (Test-Path -LiteralPath $env:HELENGINE_LOCK_TEST_RELEASE)) { Start-Sleep -Milliseconds 50 }"
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
> "%HELENGINE_LOCK_TEST_DONE%" echo finished
exit /b 0
'@ -NoNewline

    $ExpectedLockCommands = @(
        "Enter-BuildPlatformProjectLock",
        "Exit-BuildPlatformProjectLock",
        "Test-BuildPlatformProjectLockHeld"
    )
    $ActualLockCommands = @(Get-Command -Module BuildPlatformLock | Select-Object -ExpandProperty Name | Sort-Object)
    if (($ActualLockCommands -join "|") -cne ($ExpectedLockCommands -join "|")) {
        throw "The lock module exported '$($ActualLockCommands -join "', '")' instead of exactly the three public lock functions."
    }

    $DirectLockPath = Join-Path $TestRootPath "direct-lock\project.lock"
    if (Test-BuildPlatformProjectLockHeld -LockPath $DirectLockPath) {
        throw "A lock probe treated an unowned file as held."
    }
    $DirectMetadata = [ordered]@{
        processId = $PID
        projectPath = Join-Path $TestRootPath "direct-project\project.heproj"
        platform = "windows"
        profile = "profiler"
        output = Join-Path $TestRootPath "direct-output"
        startedUtc = [DateTime]::UtcNow.ToString("o")
    }
    $DirectLockHandle = Enter-BuildPlatformProjectLock `
        -LockPath $DirectLockPath `
        -Metadata $DirectMetadata `
        -Timeout ([TimeSpan]::FromSeconds(1))
    try {
        if (-not (Test-BuildPlatformProjectLockHeld -LockPath $DirectLockPath)) {
            throw "A lock probe did not detect the live owner handle."
        }
        $ReadableDirectMetadata = Get-Content -LiteralPath $DirectLockPath -Raw | ConvertFrom-Json
        if ($ReadableDirectMetadata.processId -ne $PID) {
            throw "Owner metadata was not readable while the live handle was held."
        }
    } finally {
        Exit-BuildPlatformProjectLock -LockHandle $DirectLockHandle
    }
    if (-not (Test-Path -LiteralPath $DirectLockPath -PathType Leaf)) {
        throw "Lock exit deleted the readable metadata file."
    }
    if (Test-BuildPlatformProjectLockHeld -LockPath $DirectLockPath) {
        throw "A leftover readable metadata file was treated as ownership."
    }

    $SameProjectPath = New-TestProject -Name "MixedCaseProject"
    $SameProjectOwner = Start-WrapperInvocation -Control (New-InvocationControl `
        -Name "same-project-owner" `
        -ProjectPath $SameProjectPath)
    Wait-ForPath -Path $SameProjectOwner.MarkerPath -Description "the same-project owner to enter fake dotnet"

    $MixedCaseSameProjectPath = $SameProjectPath.ToUpperInvariant()
    $SameProjectWaiter = Start-WrapperInvocation -Control (New-InvocationControl `
        -Name "same-project-waiter" `
        -ProjectPath $MixedCaseSameProjectPath `
        -Released)
    Start-Sleep -Milliseconds 750
    if (Test-Path -LiteralPath $SameProjectWaiter.MarkerPath) {
        throw "Same canonical project wrappers overlapped: the second wrapper reached fake dotnet before the owner released."
    }
    Release-Invocation -Control $SameProjectOwner
    $null = Assert-SuccessfulInvocation -Control $SameProjectOwner
    $null = Assert-SuccessfulInvocation -Control $SameProjectWaiter
    if (-not (Test-Path -LiteralPath $SameProjectWaiter.MarkerPath)) {
        throw "The waiting same-project wrapper did not proceed after the owner released."
    }

    $DifferentOwnerProjectPath = New-TestProject -Name "different-owner"
    $DifferentWaiterProjectPath = New-TestProject -Name "different-waiter"
    $DifferentProjectOwner = Start-WrapperInvocation -Control (New-InvocationControl `
        -Name "different-project-owner" `
        -ProjectPath $DifferentOwnerProjectPath)
    Wait-ForPath -Path $DifferentProjectOwner.MarkerPath -Description "the different-project owner to enter fake dotnet"
    $DifferentProjectWaiter = Start-WrapperInvocation -Control (New-InvocationControl `
        -Name "different-project-waiter" `
        -ProjectPath $DifferentWaiterProjectPath `
        -Released)
    Wait-ForPath -Path $DifferentProjectWaiter.MarkerPath -Description "the different-project wrapper to enter fake dotnet while the owner is blocked"
    if ($DifferentProjectOwner.Process.HasExited) {
        throw "The different-project owner was not still blocked when the second project reached fake dotnet."
    }
    $null = Assert-SuccessfulInvocation -Control $DifferentProjectWaiter
    Release-Invocation -Control $DifferentProjectOwner
    $null = Assert-SuccessfulInvocation -Control $DifferentProjectOwner

    $TimeoutProjectPath = New-TestProject -Name "timeout-project"
    $TimeoutOwner = Start-WrapperInvocation -Control (New-InvocationControl `
        -Name "timeout-owner" `
        -ProjectPath $TimeoutProjectPath)
    Wait-ForPath -Path $TimeoutOwner.MarkerPath -Description "the timeout owner to enter fake dotnet"
    $TimeoutWaiter = Start-WrapperInvocation -Control (New-InvocationControl `
        -Name "timeout-waiter" `
        -ProjectPath $TimeoutProjectPath `
        -Released `
        -LockTimeout ([TimeSpan]::FromMilliseconds(500)))
    $TimeoutResult = Complete-Invocation -Control $TimeoutWaiter
    if ($TimeoutResult.ExitCode -eq 0) {
        throw "The same-project timeout wrapper succeeded while the owner still held the lock."
    }
    $TimeoutCombinedOutput = $TimeoutResult.StandardOutput + [Environment]::NewLine + $TimeoutResult.StandardError
    $TimeoutLayout = Resolve-BuildPlatformCacheLayout `
        -CacheRootPath $CacheRootPath `
        -ProjectRootPath (Split-Path -Parent $TimeoutProjectPath) `
        -Platform "windows" `
        -Configuration "Release" `
        -BuildProfile "profiler"
    foreach ($ExpectedTimeoutText in @(
            ('"processId":' + $TimeoutOwner.Process.Id),
            "Timed out after",
            [System.IO.Path]::GetFullPath($TimeoutProjectPath),
            $TimeoutLayout.LockPath
        )) {
        if ($TimeoutCombinedOutput -notmatch [regex]::Escape($ExpectedTimeoutText)) {
            throw "Timeout diagnostics did not mention '$ExpectedTimeoutText'. Output: $TimeoutCombinedOutput"
        }
    }
    Release-Invocation -Control $TimeoutOwner
    $null = Assert-SuccessfulInvocation -Control $TimeoutOwner

    $CrashProjectPath = New-TestProject -Name "crash-project"
    $CrashOwner = Start-WrapperInvocation -Control (New-InvocationControl `
        -Name "crash-owner" `
        -ProjectPath $CrashProjectPath)
    Wait-ForPath -Path $CrashOwner.MarkerPath -Description "the crash owner to enter fake dotnet"
    $CrashLayout = Resolve-BuildPlatformCacheLayout `
        -CacheRootPath $CacheRootPath `
        -ProjectRootPath (Split-Path -Parent $CrashProjectPath) `
        -Platform "windows" `
        -Configuration "Release" `
        -BuildProfile "profiler"
    if (-not (Test-Path -LiteralPath $CrashLayout.LockPath -PathType Leaf)) {
        throw "The owner did not leave readable lock metadata at '$($CrashLayout.LockPath)'."
    }
    $CrashOwner.Process.Kill()
    if (-not $CrashOwner.Process.WaitForExit(20000)) {
        throw "The terminated crash owner did not exit within the 20-second process cap."
    }
    Release-Invocation -Control $CrashOwner
    Wait-ForPath -Path $CrashOwner.DonePath -Description "the terminated owner's exact fake-dotnet child to finish"
    $LeftoverMetadata = Get-Content -LiteralPath $CrashLayout.LockPath -Raw | ConvertFrom-Json
    if ($LeftoverMetadata.processId -ne $CrashOwner.Process.Id) {
        throw "The post-crash lock metadata was not readable or did not describe the terminated owner."
    }

    $CrashRecovery = Start-WrapperInvocation -Control (New-InvocationControl `
        -Name "crash-recovery" `
        -ProjectPath $CrashProjectPath `
        -Released)
    Wait-ForPath -Path $CrashRecovery.MarkerPath -Description "the post-crash wrapper to acquire the OS lock"
    $null = Assert-SuccessfulInvocation -Control $CrashRecovery

    Write-Output "LOCKING_TEST_PASS"
} finally {
    foreach ($Invocation in $StartedInvocations) {
        try {
            Release-Invocation -Control $Invocation
            if ($null -ne $Invocation.Process -and -not $Invocation.Process.HasExited) {
                if (-not $Invocation.Process.WaitForExit(2000)) {
                    $Invocation.Process.Kill()
                    $Invocation.Process.WaitForExit()
                }
            }
        } catch {
        } finally {
            if ($null -ne $Invocation.Process) {
                $Invocation.Process.Dispose()
            }
        }
    }
    if (Test-Path -LiteralPath $TestRootPath) {
        Remove-Item -LiteralPath $TestRootPath -Recurse -Force
    }
}

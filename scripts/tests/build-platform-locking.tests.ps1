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
$StartedMutexOwners = New-Object System.Collections.ArrayList
$MutexOwnerScriptPath = Join-Path $TestRootPath "mutex-owner.ps1"
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
$ExactChildCleanupFunctionToken = 'function Stop-InvocationExact' + 'ChildProcess'
$ExactWrapperCleanupFunctionToken = 'function Stop-InvocationExact' + 'WrapperProcess'
$ExactChildCleanupStartIndex = $HarnessSource.IndexOf($ExactChildCleanupFunctionToken, [StringComparison]::Ordinal)
$ExactChildCleanupEndIndex = $HarnessSource.IndexOf(
    $ExactWrapperCleanupFunctionToken,
    $ExactChildCleanupStartIndex,
    [StringComparison]::Ordinal)
if ($ExactChildCleanupStartIndex -lt 0 -or $ExactChildCleanupEndIndex -le $ExactChildCleanupStartIndex) {
    throw "The locking harness could not locate its exact-child cleanup function."
}
$ExactChildCleanupSource = $HarnessSource.Substring(
    $ExactChildCleanupStartIndex,
    $ExactChildCleanupEndIndex - $ExactChildCleanupStartIndex)
$ExactHandleAcquisitionToken = 'GetProcess' + 'ById'
$ExactHandleOpenToken = 'RecordedChildProcess.' + 'Handle'
$ReleaseInvocationToken = 'Release-' + 'Invocation -Control'
$ExactHandleAcquisitionIndex = $ExactChildCleanupSource.IndexOf(
    $ExactHandleAcquisitionToken,
    [StringComparison]::Ordinal)
$ExactHandleOpenIndex = $ExactChildCleanupSource.IndexOf(
    $ExactHandleOpenToken,
    [StringComparison]::Ordinal)
$ReleaseInvocationIndex = $ExactChildCleanupSource.IndexOf(
    $ReleaseInvocationToken,
    [StringComparison]::Ordinal)
if ($ExactHandleAcquisitionIndex -lt 0 -or
    $ExactHandleOpenIndex -lt $ExactHandleAcquisitionIndex -or
    $ReleaseInvocationIndex -lt 0 -or
    $ReleaseInvocationIndex -lt $ExactHandleOpenIndex) {
    throw "Exact-child cleanup must acquire the recorded process handle before releasing the child."
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
        [string]$CacheRootPath = $script:CacheRootPath,

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
        CacheRootPath = [System.IO.Path]::GetFullPath($CacheRootPath)
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
        $Control.CacheRootPath,
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
        $Control.StartedUtc = [DateTime]::UtcNow
        if (-not $Process.Start()) {
            $Process.Dispose()
            throw "Wrapper '$($Control.Name)' failed to start."
        }
        $Control.Process = $Process
        $null = $StartedInvocations.Add($Control)
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

function Wait-ForOptionalPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutMilliseconds
    )

    $Stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($Stopwatch.ElapsedMilliseconds -lt $TimeoutMilliseconds) {
        if (Test-Path -LiteralPath $Path) {
            return $true
        }
        Start-Sleep -Milliseconds 25
    }
    return Test-Path -LiteralPath $Path
}

function Get-InvocationRemainingWaitMilliseconds {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Control,

        [Parameter(Mandatory = $true)]
        [int]$MaximumMilliseconds
    )

    if ($null -eq $Control.StartedUtc) {
        return $MaximumMilliseconds
    }

    $ElapsedMilliseconds = [int]([DateTime]::UtcNow - $Control.StartedUtc).TotalMilliseconds
    return [Math]::Max(0, [Math]::Min($MaximumMilliseconds, 20000 - $ElapsedMilliseconds))
}

function Stop-InvocationExactChildProcess {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Control
    )

    $RecordedChildProcess = $null
    $RecordedChildProcessValidated = $false
    try {
        try {
            $ChildProcessIdWaitMilliseconds = Get-InvocationRemainingWaitMilliseconds `
                -Control $Control `
                -MaximumMilliseconds 1000
            if (Wait-ForOptionalPath `
                    -Path $Control.ChildProcessIdPath `
                    -TimeoutMilliseconds $ChildProcessIdWaitMilliseconds) {
                $ChildProcessIdFile = Get-Item -LiteralPath $Control.ChildProcessIdPath
                $RecordedChildProcessId = 0
                $RecordedChildProcessIdText = (Get-Content -LiteralPath $Control.ChildProcessIdPath -Raw).Trim()
                if ([int]::TryParse($RecordedChildProcessIdText, [ref]$RecordedChildProcessId) -and
                    $RecordedChildProcessId -gt 0) {
                    try {
                        $RecordedChildProcess = [System.Diagnostics.Process]::GetProcessById($RecordedChildProcessId)
                    } catch [System.ArgumentException] {
                    }

                    if ($null -ne $RecordedChildProcess -and -not $RecordedChildProcess.HasExited) {
                        $null = $RecordedChildProcess.Handle
                        $RecordedChildProcessName = $RecordedChildProcess.ProcessName
                        $RecordedChildProcessStartedUtc = $RecordedChildProcess.StartTime.ToUniversalTime()
                        $RecordedChildProcessValidated = $RecordedChildProcessName -ieq "powershell" -and
                            $null -ne $Control.StartedUtc -and
                            $RecordedChildProcessStartedUtc -ge $Control.StartedUtc -and
                            $RecordedChildProcessStartedUtc -le $ChildProcessIdFile.LastWriteTimeUtc
                    }
                }
            }
        } catch {
        } finally {
            Release-Invocation -Control $Control
        }

        if ($RecordedChildProcessValidated -and -not $RecordedChildProcess.HasExited) {
            $GracefulWaitMilliseconds = Get-InvocationRemainingWaitMilliseconds `
                -Control $Control `
                -MaximumMilliseconds 1000
            $RecordedChildProcessExited = $RecordedChildProcess.HasExited
            if (-not $RecordedChildProcessExited -and $GracefulWaitMilliseconds -gt 0) {
                $RecordedChildProcessExited = $RecordedChildProcess.WaitForExit($GracefulWaitMilliseconds)
            }
            if (-not $RecordedChildProcessExited -and -not $RecordedChildProcess.HasExited) {
                $RecordedChildProcess.Kill()
                $KilledWaitMilliseconds = Get-InvocationRemainingWaitMilliseconds `
                    -Control $Control `
                    -MaximumMilliseconds 1000
                if ($KilledWaitMilliseconds -gt 0) {
                    $null = $RecordedChildProcess.WaitForExit($KilledWaitMilliseconds)
                }
            }
        }

        $DoneWaitMilliseconds = Get-InvocationRemainingWaitMilliseconds `
            -Control $Control `
            -MaximumMilliseconds 1000
        if ($DoneWaitMilliseconds -gt 0) {
            $null = Wait-ForOptionalPath -Path $Control.DonePath -TimeoutMilliseconds $DoneWaitMilliseconds
        }
    } finally {
        if ($null -ne $RecordedChildProcess) {
            $RecordedChildProcess.Dispose()
        }
    }
}

function Stop-InvocationExactWrapperProcess {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Control
    )

    if ($null -eq $Control.Process -or $Control.Process.HasExited) {
        return
    }

    $GracefulWaitMilliseconds = Get-InvocationRemainingWaitMilliseconds `
        -Control $Control `
        -MaximumMilliseconds 1000
    if ($GracefulWaitMilliseconds -gt 0 -and
        $Control.Process.WaitForExit($GracefulWaitMilliseconds)) {
        return
    }

    if (-not $Control.Process.HasExited) {
        $Control.Process.Kill()
        $KilledWaitMilliseconds = Get-InvocationRemainingWaitMilliseconds `
            -Control $Control `
            -MaximumMilliseconds 1000
        if ($KilledWaitMilliseconds -gt 0) {
            $null = $Control.Process.WaitForExit($KilledWaitMilliseconds)
        }
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

function New-MutexOwnerControl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$ProjectHash,

        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    $ControlRootPath = Join-Path $TestRootPath ("mutex-controls\" + $Name)
    $null = New-Item -ItemType Directory -Path $ControlRootPath -Force
    return [pscustomobject]@{
        Name = $Name
        ProjectHash = $ProjectHash
        ProjectPath = $ProjectPath
        OwnedMarkerPath = Join-Path $ControlRootPath "owned.marker"
        ReleaseMarkerPath = Join-Path $ControlRootPath "release.marker"
        Process = $null
    }
}

function Start-MutexOwnerProcess {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Control
    )

    $Arguments = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $MutexOwnerScriptPath,
        "-LockModulePath",
        $LockModulePath,
        "-ProjectHash",
        $Control.ProjectHash,
        "-ProjectPath",
        $Control.ProjectPath,
        "-OwnedMarkerPath",
        $Control.OwnedMarkerPath,
        "-ReleaseMarkerPath",
        $Control.ReleaseMarkerPath
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
    if (-not $Process.Start()) {
        $Process.Dispose()
        throw "Mutex owner '$($Control.Name)' failed to start."
    }
    $Control.Process = $Process
    $null = $StartedMutexOwners.Add($Control)
    return $Control
}

function Release-MutexOwnerProcess {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Control
    )

    if (-not (Test-Path -LiteralPath $Control.ReleaseMarkerPath)) {
        Set-Content -LiteralPath $Control.ReleaseMarkerPath -Value "released" -NoNewline
    }
}

function Assert-SuccessfulMutexOwnerProcess {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Control
    )

    if (-not $Control.Process.WaitForExit(10000)) {
        throw "Mutex owner '$($Control.Name)' did not exit after release."
    }
    $StandardOutput = $Control.Process.StandardOutput.ReadToEnd()
    $StandardError = $Control.Process.StandardError.ReadToEnd()
    if ($Control.Process.ExitCode -ne 0) {
        throw "Mutex owner '$($Control.Name)' failed with exit code $($Control.Process.ExitCode). stdout: $StandardOutput stderr: $StandardError"
    }
}

try {
    $null = New-Item -ItemType Directory -Path $FakeToolsPath -Force
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $EditorProjectPath) -Force
    Set-Content -LiteralPath $EditorProjectPath -Value "<Project />" -NoNewline
    Set-Content -LiteralPath $FakeDotNetPath -Value @'
@echo off
setlocal EnableExtensions
if exist "%HELENGINE_LOCK_TEST_CHILD_PID%" del /Q "%HELENGINE_LOCK_TEST_CHILD_PID%"
if exist "%HELENGINE_LOCK_TEST_DONE%" del /Q "%HELENGINE_LOCK_TEST_DONE%"
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

    Set-Content -LiteralPath $MutexOwnerScriptPath -Value @'
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$LockModulePath,
    [Parameter(Mandatory = $true)][string]$ProjectHash,
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$OwnedMarkerPath,
    [Parameter(Mandatory = $true)][string]$ReleaseMarkerPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Import-Module $LockModulePath -Force
$Handle = Enter-BuildPlatformProjectMutex `
    -ProjectHash $ProjectHash `
    -ProjectPath $ProjectPath `
    -Timeout ([TimeSpan]::FromSeconds(10))
Set-Content -LiteralPath $OwnedMarkerPath -Value $PID -NoNewline
try {
    while (-not (Test-Path -LiteralPath $ReleaseMarkerPath)) {
        Start-Sleep -Milliseconds 25
    }
} finally {
    Exit-BuildPlatformProjectMutex -MutexHandle $Handle
}
'@ -NoNewline

    $ExpectedLockCommands = @(
        "Enter-BuildPlatformProjectLock",
        "Enter-BuildPlatformProjectLockNonBlocking",
        "Enter-BuildPlatformProjectMutex",
        "Exit-BuildPlatformProjectLock",
        "Exit-BuildPlatformProjectMutex",
        "Test-BuildPlatformProjectLockHeld"
    )
    $ActualLockCommands = @(Get-Command -Module BuildPlatformLock | Select-Object -ExpandProperty Name | Sort-Object)
    if (($ActualLockCommands -join "|") -cne ($ExpectedLockCommands -join "|")) {
        throw "The lock module exported '$($ActualLockCommands -join "', '")' instead of exactly the six public lock functions."
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

    $DirectMutexProjectPath = Join-Path $TestRootPath "direct-mutex\project.heproj"
    foreach ($InvalidProjectHash in @("", ("A" * 32), ("g" * 32), ("a" * 31))) {
        $InvalidHashThrew = $false
        try {
            $null = Enter-BuildPlatformProjectMutex `
                -ProjectHash $InvalidProjectHash `
                -ProjectPath $DirectMutexProjectPath `
                -Timeout ([TimeSpan]::Zero)
        } catch {
            $InvalidHashThrew = $true
        }
        if (-not $InvalidHashThrew) {
            throw "Invalid project hash '$InvalidProjectHash' did not throw."
        }
    }
    $NegativeTimeoutThrew = $false
    try {
        $null = Enter-BuildPlatformProjectMutex `
            -ProjectHash ("1" * 32) `
            -ProjectPath $DirectMutexProjectPath `
            -Timeout ([TimeSpan]::FromMilliseconds(-1))
    } catch {
        $NegativeTimeoutThrew = $true
    }
    if (-not $NegativeTimeoutThrew) {
        throw "A negative mutex timeout did not throw."
    }

    $ExitReleaseHash = "11111111111111111111111111111111"
    $ExitReleaseHandle = Enter-BuildPlatformProjectMutex `
        -ProjectHash $ExitReleaseHash `
        -ProjectPath $DirectMutexProjectPath `
        -Timeout ([TimeSpan]::Zero)
    Exit-BuildPlatformProjectMutex -MutexHandle $ExitReleaseHandle
    $ExitReleaseContender = Start-MutexOwnerProcess -Control (New-MutexOwnerControl `
        -Name "exit-release-contender" `
        -ProjectHash $ExitReleaseHash `
        -ProjectPath $DirectMutexProjectPath)
    Wait-ForPath -Path $ExitReleaseContender.OwnedMarkerPath -Description "the mutex contender after explicit release"
    Release-MutexOwnerProcess -Control $ExitReleaseContender
    Assert-SuccessfulMutexOwnerProcess -Control $ExitReleaseContender

    $TimeoutMutexHash = "22222222222222222222222222222222"
    $TimeoutMutexOwner = Start-MutexOwnerProcess -Control (New-MutexOwnerControl `
        -Name "zero-timeout-owner" `
        -ProjectHash $TimeoutMutexHash `
        -ProjectPath $DirectMutexProjectPath)
    Wait-ForPath -Path $TimeoutMutexOwner.OwnedMarkerPath -Description "the zero-timeout mutex owner"
    $ZeroTimeoutError = $null
    try {
        $null = Enter-BuildPlatformProjectMutex `
            -ProjectHash $TimeoutMutexHash `
            -ProjectPath $DirectMutexProjectPath `
            -Timeout ([TimeSpan]::Zero)
    } catch {
        $ZeroTimeoutError = $_
    }
    if ($null -eq $ZeroTimeoutError -or $ZeroTimeoutError.Exception.Message -notmatch "Timed out after") {
        throw "A zero-timeout mutex contender did not receive a timeout error while another process owned the mutex."
    }
    Release-MutexOwnerProcess -Control $TimeoutMutexOwner
    Assert-SuccessfulMutexOwnerProcess -Control $TimeoutMutexOwner

    $AbandonedMutexHash = "33333333333333333333333333333333"
    $AbandonedMutexOwner = Start-MutexOwnerProcess -Control (New-MutexOwnerControl `
        -Name "abandoned-owner" `
        -ProjectHash $AbandonedMutexHash `
        -ProjectPath $DirectMutexProjectPath)
    Wait-ForPath -Path $AbandonedMutexOwner.OwnedMarkerPath -Description "the mutex owner to abandon its mutex"
    $AbandonedMutexOwner.Process.Kill()
    if (-not $AbandonedMutexOwner.Process.WaitForExit(10000)) {
        throw "The exact recorded mutex owner did not terminate for abandonment coverage."
    }
    $AbandonedMutexHandle = Enter-BuildPlatformProjectMutex `
        -ProjectHash $AbandonedMutexHash `
        -ProjectPath $DirectMutexProjectPath `
        -Timeout ([TimeSpan]::Zero)
    Exit-BuildPlatformProjectMutex -MutexHandle $AbandonedMutexHandle

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

    $CrossCacheProjectPath = New-TestProject -Name "cross-cache-project"
    $CrossCacheOwner = Start-WrapperInvocation -Control (New-InvocationControl `
        -Name "cross-cache-owner" `
        -ProjectPath $CrossCacheProjectPath `
        -CacheRootPath (Join-Path $TestRootPath "cache-a"))
    Wait-ForPath -Path $CrossCacheOwner.MarkerPath -Description "the cross-cache owner to enter fake dotnet"

    $CrossCacheWaiter = Start-WrapperInvocation -Control (New-InvocationControl `
        -Name "cross-cache-waiter" `
        -ProjectPath $CrossCacheProjectPath `
        -CacheRootPath (Join-Path $TestRootPath "cache-b") `
        -Released)
    Start-Sleep -Milliseconds 750
    if (Test-Path -LiteralPath $CrossCacheWaiter.MarkerPath) {
        throw "Same canonical project wrappers bypassed serialization through different cache roots."
    }
    Release-Invocation -Control $CrossCacheOwner
    $null = Assert-SuccessfulInvocation -Control $CrossCacheOwner
    $null = Assert-SuccessfulInvocation -Control $CrossCacheWaiter

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
    $TimeoutMutexName = "Global\helengine.build-platform.project.v1.$($TimeoutLayout.ProjectHash)"
    foreach ($ExpectedTimeoutText in @(
            "Timed out after",
            [System.IO.Path]::GetFullPath($TimeoutProjectPath),
            $TimeoutMutexName
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
    Wait-ForPath -Path $CrashOwner.ChildProcessIdPath -Description "the crash owner's exact fake-dotnet child PID"
    $CrashChildProcessId = [int](Get-Content -LiteralPath $CrashOwner.ChildProcessIdPath -Raw)
    $CrashChildProcess = $null
    try {
        $CrashChildProcess = [System.Diagnostics.Process]::GetProcessById($CrashChildProcessId)
        Stop-InvocationExactWrapperProcess -Control $CrashOwner
        if (-not $CrashOwner.Process.HasExited) {
            throw "The terminated crash owner did not exit within the 20-second process cap."
        }
        Stop-InvocationExactChildProcess -Control $CrashOwner
        $CrashChildProcessExited = $CrashChildProcess.HasExited
        $CrashChildWaitMilliseconds = Get-InvocationRemainingWaitMilliseconds `
            -Control $CrashOwner `
            -MaximumMilliseconds 2000
        if (-not $CrashChildProcessExited -and $CrashChildWaitMilliseconds -gt 0) {
            $CrashChildProcessExited = $CrashChildProcess.WaitForExit($CrashChildWaitMilliseconds)
        }
        if (-not $CrashChildProcessExited) {
            throw "Exact-child cleanup left the terminated wrapper's recorded child alive."
        }
    } finally {
        if ($null -ne $CrashChildProcess) {
            $CrashChildProcess.Dispose()
        }
    }
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
            Stop-InvocationExactChildProcess -Control $Invocation
        } catch {
        }
        try {
            Stop-InvocationExactWrapperProcess -Control $Invocation
        } catch {
        }
        try {
            Stop-InvocationExactChildProcess -Control $Invocation
        } catch {
        }
        if ($null -ne $Invocation.Process) {
            $Invocation.Process.Dispose()
        }
    }
    foreach ($MutexOwner in $StartedMutexOwners) {
        if ($null -ne $MutexOwner.Process) {
            try {
                if (-not $MutexOwner.Process.HasExited) {
                    $MutexOwner.Process.Kill()
                    $null = $MutexOwner.Process.WaitForExit(1000)
                }
            } catch {
            }
            $MutexOwner.Process.Dispose()
        }
    }
    if (Test-Path -LiteralPath $TestRootPath) {
        Remove-Item -LiteralPath $TestRootPath -Recurse -Force
    }
}

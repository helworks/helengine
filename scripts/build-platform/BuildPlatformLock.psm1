Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Read-BuildPlatformProjectLockMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LockPath
    )

    $ReadStream = $null
    $Reader = $null
    try {
        $ReadStream = [System.IO.File]::Open(
            $LockPath,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::ReadWrite)
        $Reader = New-Object System.IO.StreamReader($ReadStream, [System.Text.Encoding]::UTF8, $true, 1024, $true)
        $MetadataText = $Reader.ReadToEnd()
        if ([string]::IsNullOrWhiteSpace($MetadataText)) {
            return "unavailable"
        }
        return $MetadataText.Trim()
    } catch {
        return "unavailable"
    } finally {
        if ($null -ne $Reader) {
            $Reader.Dispose()
        }
        if ($null -ne $ReadStream) {
            $ReadStream.Dispose()
        }
    }
}

function Enter-BuildPlatformProjectLock {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LockPath,

        [Parameter(Mandatory = $true)]
        [psobject]$Metadata,

        [Parameter(Mandatory = $true)]
        [TimeSpan]$Timeout
    )

    if ([string]::IsNullOrWhiteSpace($LockPath)) {
        throw "Lock path must be provided."
    }
    if ($Timeout -lt [TimeSpan]::Zero) {
        throw "Lock timeout must be zero or positive."
    }

    $CanonicalLockPath = [System.IO.Path]::GetFullPath($LockPath)
    $LockDirectoryPath = Split-Path -Parent $CanonicalLockPath
    $null = New-Item -ItemType Directory -Path $LockDirectoryPath -Force

    $Stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $LastStatusElapsed = [TimeSpan]::FromSeconds(-5)
    while ($true) {
        $OwnerStream = $null
        try {
            $OwnerStream = [System.IO.File]::Open(
                $CanonicalLockPath,
                [System.IO.FileMode]::OpenOrCreate,
                [System.IO.FileAccess]::ReadWrite,
                [System.IO.FileShare]::Read)
        } catch [System.IO.IOException] {
            $OwnerMetadata = Read-BuildPlatformProjectLockMetadata -LockPath $CanonicalLockPath
            $Elapsed = $Stopwatch.Elapsed
            if (($Elapsed - $LastStatusElapsed) -ge [TimeSpan]::FromSeconds(5)) {
                Write-Host ("Waiting for project lock '{0}' after {1}. Active owner metadata: {2}" -f `
                    $CanonicalLockPath,
                    $Elapsed.ToString("c"),
                    $OwnerMetadata)
                $LastStatusElapsed = $Elapsed
            }

            if ($Elapsed -ge $Timeout) {
                $CanonicalProjectPath = [string]$Metadata.projectPath
                throw ("Timed out after {0} waiting for project lock '{1}' for canonical project '{2}'. Active owner metadata: {3}" -f `
                    $Elapsed.ToString("c"),
                    $CanonicalLockPath,
                    $CanonicalProjectPath,
                    $OwnerMetadata)
            }

            $RemainingMilliseconds = [Math]::Max(1, [Math]::Min(100, ($Timeout - $Elapsed).TotalMilliseconds))
            Start-Sleep -Milliseconds ([int]$RemainingMilliseconds)
            continue
        }

        try {
            $MetadataJson = $Metadata | ConvertTo-Json -Compress
            $MetadataBytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($MetadataJson)
            $OwnerStream.SetLength(0)
            $OwnerStream.Position = 0
            $OwnerStream.Write($MetadataBytes, 0, $MetadataBytes.Length)
            $OwnerStream.Flush()

            return [pscustomobject]@{
                LockPath = $CanonicalLockPath
                Stream = $OwnerStream
                Metadata = $Metadata
            }
        } catch {
            $OwnerStream.Dispose()
            throw
        }
    }
}

function Enter-BuildPlatformProjectLockNonBlocking {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LockPath,

        [Parameter(Mandatory = $true)]
        [psobject]$Metadata
    )

    if ([string]::IsNullOrWhiteSpace($LockPath)) {
        throw "Lock path must be provided."
    }

    $CanonicalLockPath = [System.IO.Path]::GetFullPath($LockPath)
    $LockDirectoryPath = Split-Path -Parent $CanonicalLockPath
    $null = New-Item -ItemType Directory -Path $LockDirectoryPath -Force
    $OwnerStream = $null
    try {
        try {
            $OwnerStream = [System.IO.File]::Open(
                $CanonicalLockPath,
                [System.IO.FileMode]::OpenOrCreate,
                [System.IO.FileAccess]::ReadWrite,
                [System.IO.FileShare]::Read)
        } catch [System.IO.IOException] {
            $NativeErrorCode = $_.Exception.HResult -band 0xFFFF
            if ($NativeErrorCode -eq 32 -or $NativeErrorCode -eq 33) {
                return $null
            }
            throw
        }

        $MetadataJson = $Metadata | ConvertTo-Json -Compress
        $MetadataBytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($MetadataJson)
        $OwnerStream.SetLength(0)
        $OwnerStream.Position = 0
        $OwnerStream.Write($MetadataBytes, 0, $MetadataBytes.Length)
        $OwnerStream.Flush()

        return [pscustomobject]@{
            LockPath = $CanonicalLockPath
            Stream = $OwnerStream
            Metadata = $Metadata
        }
    } catch {
        if ($null -ne $OwnerStream) {
            $OwnerStream.Dispose()
        }
        throw
    }
}

function Test-BuildPlatformProjectLockHeld {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LockPath
    )

    if ([string]::IsNullOrWhiteSpace($LockPath)) {
        throw "Lock path must be provided."
    }

    $CanonicalLockPath = [System.IO.Path]::GetFullPath($LockPath)
    $LockDirectoryPath = Split-Path -Parent $CanonicalLockPath
    $null = New-Item -ItemType Directory -Path $LockDirectoryPath -Force
    $ProbeStream = $null
    try {
        $ProbeStream = [System.IO.File]::Open(
            $CanonicalLockPath,
            [System.IO.FileMode]::OpenOrCreate,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::Read)
        return $false
    } catch [System.IO.IOException] {
        return $true
    } finally {
        if ($null -ne $ProbeStream) {
            $ProbeStream.Dispose()
        }
    }
}

function Exit-BuildPlatformProjectLock {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$LockHandle
    )

    if ($null -ne $LockHandle.Stream) {
        $LockHandle.Stream.Dispose()
    }
}

function Enter-BuildPlatformNamedMutex {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Hash,

        [Parameter(Mandatory = $true)]
        [string]$TargetPath,

        [Parameter(Mandatory = $true)]
        [TimeSpan]$Timeout,

        [Parameter(Mandatory = $true)]
        [string]$MutexName,

        [Parameter(Mandatory = $true)]
        [string]$TargetKind
    )

    $TargetDisplayName = $TargetKind.Substring(0, 1).ToUpperInvariant() + $TargetKind.Substring(1)
    if ($Hash -cnotmatch '^[0-9a-f]{32}$') {
        throw "$TargetDisplayName hash must be 32 lowercase hexadecimal characters."
    }
    if ([string]::IsNullOrWhiteSpace($TargetPath)) {
        throw "$TargetDisplayName path must be provided."
    }
    if ($Timeout -lt [TimeSpan]::Zero) {
        throw "Lock timeout must be zero or positive."
    }

    $Mutex = New-Object System.Threading.Mutex($false, $MutexName)
    $OwnsMutex = $false
    try {
        try {
            $OwnsMutex = $Mutex.WaitOne($Timeout)
        } catch [System.Threading.AbandonedMutexException] {
            $OwnsMutex = $true
        }
        if (-not $OwnsMutex) {
            throw "Timed out after $($Timeout.ToString('c')) waiting for $TargetKind mutex '$MutexName' for canonical $TargetKind '$TargetPath'."
        }
        return [pscustomobject]@{
            Name = $MutexName
            Mutex = $Mutex
            OwnsMutex = $true
        }
    } catch {
        try {
            if ($OwnsMutex) {
                $Mutex.ReleaseMutex()
            }
        } finally {
            $Mutex.Dispose()
        }
        throw
    }
}

function Exit-BuildPlatformNamedMutex {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$MutexHandle
    )

    try {
        if ($MutexHandle.OwnsMutex) {
            $MutexHandle.Mutex.ReleaseMutex()
        }
    } finally {
        $MutexHandle.Mutex.Dispose()
    }
}

function Enter-BuildPlatformProjectMutex {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectHash,

        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,

        [Parameter(Mandatory = $true)]
        [TimeSpan]$Timeout
    )

    return Enter-BuildPlatformNamedMutex `
        -Hash $ProjectHash `
        -TargetPath $ProjectPath `
        -Timeout $Timeout `
        -MutexName "Global\helengine.build-platform.project.v1.$ProjectHash" `
        -TargetKind "project"
}

function Exit-BuildPlatformProjectMutex {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$MutexHandle
    )

    Exit-BuildPlatformNamedMutex -MutexHandle $MutexHandle
}

function Enter-BuildPlatformOutputMutex {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputHash,

        [Parameter(Mandatory = $true)]
        [string]$OutputPath,

        [Parameter(Mandatory = $true)]
        [TimeSpan]$Timeout
    )

    return Enter-BuildPlatformNamedMutex `
        -Hash $OutputHash `
        -TargetPath $OutputPath `
        -Timeout $Timeout `
        -MutexName "Global\helengine.build-platform.output.v1.$OutputHash" `
        -TargetKind "output"
}

function Exit-BuildPlatformOutputMutex {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$MutexHandle
    )

    Exit-BuildPlatformNamedMutex -MutexHandle $MutexHandle
}

Export-ModuleMember -Function @(
    'Enter-BuildPlatformOutputMutex',
    'Enter-BuildPlatformProjectLock',
    'Enter-BuildPlatformProjectLockNonBlocking',
    'Enter-BuildPlatformProjectMutex',
    'Exit-BuildPlatformOutputMutex',
    'Test-BuildPlatformProjectLockHeld',
    'Exit-BuildPlatformProjectLock',
    'Exit-BuildPlatformProjectMutex'
)

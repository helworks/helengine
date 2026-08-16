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

Export-ModuleMember -Function @(
    'Enter-BuildPlatformProjectLock',
    'Enter-BuildPlatformProjectLockNonBlocking',
    'Test-BuildPlatformProjectLockHeld',
    'Exit-BuildPlatformProjectLock'
)

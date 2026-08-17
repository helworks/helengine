Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-BuildPlatformWaiterCanonicalDirectoryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "Output root path must be provided."
    }

    $FullPath = [IO.Path]::GetFullPath($Path)
    $RootPath = [IO.Path]::GetPathRoot($FullPath)
    if ($FullPath.Length -le $RootPath.Length) {
        return $RootPath
    }

    $DirectorySeparators = [char[]]@(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar
    )
    return $FullPath.TrimEnd($DirectorySeparators)
}

function Get-BuildPlatformWaiterChildPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputRootPath,

        [Parameter(Mandatory = $true)]
        [string]$ChildName
    )

    $CandidatePath = [IO.Path]::GetFullPath((Join-Path $OutputRootPath $ChildName))
    $OutputPrefix = $OutputRootPath
    if (-not $OutputPrefix.EndsWith([IO.Path]::DirectorySeparatorChar) -and
        -not $OutputPrefix.EndsWith([IO.Path]::AltDirectorySeparatorChar)) {
        $OutputPrefix += [IO.Path]::DirectorySeparatorChar
    }
    if (-not $CandidatePath.StartsWith($OutputPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Waiter protocol path '$CandidatePath' must remain beneath output root '$OutputRootPath'."
    }
    return $CandidatePath
}

function Test-BuildPlatformWaiterAcknowledgementBytes {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AcknowledgementPath,

        [Parameter(Mandatory = $true)]
        [string]$InvocationId
    )

    $ExpectedBytes = [Text.Encoding]::ASCII.GetBytes($InvocationId)
    $ActualBytes = [IO.File]::ReadAllBytes($AcknowledgementPath)
    if ($ActualBytes.Length -ne $ExpectedBytes.Length) {
        return $false
    }
    for ($ByteIndex = 0; $ByteIndex -lt $ExpectedBytes.Length; $ByteIndex++) {
        if ($ActualBytes[$ByteIndex] -ne $ExpectedBytes[$ByteIndex]) {
            return $false
        }
    }
    return $true
}

function Resolve-BuildPlatformWaiterHandshake {
    param(
        [Parameter()]
        [AllowNull()]
        [object]$ProtocolValue,

        [Parameter(Mandatory = $true)]
        [bool]$InvocationIdWasSupplied,

        [Parameter(Mandatory = $true)]
        [string]$InvocationId,

        [Parameter(Mandatory = $true)]
        [string]$OutputRootPath
    )

    $Enabled = $false
    if ($null -ne $ProtocolValue) {
        if ($ProtocolValue -cne "ack-v1") {
            throw "HELENGINE_BUILD_WAITER_PROTOCOL must be exactly 'ack-v1' when supplied."
        }
        if (-not $InvocationIdWasSupplied) {
            throw "HELENGINE_BUILD_INVOCATION_ID must be supplied when HELENGINE_BUILD_WAITER_PROTOCOL is 'ack-v1'."
        }
        $Enabled = $true
    }

    $ParsedInvocationId = [Guid]::Empty
    if (-not [Guid]::TryParseExact($InvocationId, "D", [ref]$ParsedInvocationId) -or
        $InvocationId -cne $ParsedInvocationId.ToString("D")) {
        throw "HELENGINE_BUILD_INVOCATION_ID must be a canonical GUID in D format."
    }
    $CanonicalInvocationId = $ParsedInvocationId.ToString("D")
    $CanonicalOutputRootPath = Get-BuildPlatformWaiterCanonicalDirectoryPath -Path $OutputRootPath
    $ProofPath = Get-BuildPlatformWaiterChildPath `
        -OutputRootPath $CanonicalOutputRootPath `
        -ChildName (".helengine-build-state." + $CanonicalInvocationId + ".json")
    $AcknowledgementPath = Get-BuildPlatformWaiterChildPath `
        -OutputRootPath $CanonicalOutputRootPath `
        -ChildName (".helengine-build-state." + $CanonicalInvocationId + ".ack")

    if ($Enabled -and (Test-Path -LiteralPath $AcknowledgementPath)) {
        throw "Waiter acknowledgment '$AcknowledgementPath' already exists."
    }

    return [pscustomobject]@{
        Enabled = $Enabled
        InvocationId = $CanonicalInvocationId
        ProofPath = $ProofPath
        AcknowledgementPath = $AcknowledgementPath
    }
}

function Wait-BuildPlatformWaiterAcknowledgement {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Handshake,

        [Parameter(Mandatory = $true)]
        [TimeSpan]$Timeout
    )

    $Stopwatch = [Diagnostics.Stopwatch]::StartNew()
    do {
        if (Test-Path -LiteralPath $Handshake.AcknowledgementPath -PathType Leaf) {
            try {
                if (Test-BuildPlatformWaiterAcknowledgementBytes `
                        -AcknowledgementPath $Handshake.AcknowledgementPath `
                        -InvocationId $Handshake.InvocationId) {
                    return $true
                }
            } catch [IO.IOException] {
            } catch [UnauthorizedAccessException] {
            }
        }
        if ($Stopwatch.Elapsed -ge $Timeout) {
            return $false
        }
        Start-Sleep -Milliseconds 25
    } while ($true)
}

function Remove-BuildPlatformWaiterAcknowledgement {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Handshake
    )

    if (-not (Test-Path -LiteralPath $Handshake.AcknowledgementPath -PathType Leaf)) {
        throw "Waiter acknowledgment '$($Handshake.AcknowledgementPath)' was not found for removal."
    }
    if (-not (Test-BuildPlatformWaiterAcknowledgementBytes `
            -AcknowledgementPath $Handshake.AcknowledgementPath `
            -InvocationId $Handshake.InvocationId)) {
        throw "Waiter acknowledgment '$($Handshake.AcknowledgementPath)' did not contain the exact ASCII invocation ID bytes."
    }
    Remove-Item -LiteralPath $Handshake.AcknowledgementPath -Force
}

Export-ModuleMember -Function @(
    "Resolve-BuildPlatformWaiterHandshake",
    "Wait-BuildPlatformWaiterAcknowledgement",
    "Remove-BuildPlatformWaiterAcknowledgement"
)

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\\.."))
$ModulePath = Join-Path $RepositoryRoot "scripts\\build-platform\\BuildPlatformWaiterHandshake.psm1"
$TestRootPath = Join-Path ([System.IO.Path]::GetTempPath()) ("helengine-build-platform-waiter-handshake-" + [Guid]::NewGuid().ToString("N"))
$OutputRoot = Join-Path $TestRootPath "output"
$InvocationId = "b40ab19d-4d81-4db0-a0d4-9b818b49c7c0"

function Assert-Throws {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Action,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    try {
        & $Action
    } catch {
        return
    }
    throw "$Description did not throw."
}

try {
    $null = New-Item -ItemType Directory -Path $OutputRoot -Force
    Import-Module $ModulePath -Force

    $ExportedCommandNames = @(Get-Command -Module BuildPlatformWaiterHandshake | ForEach-Object Name | Sort-Object)
    $ExpectedCommandNames = @(
        "Remove-BuildPlatformWaiterAcknowledgement",
        "Resolve-BuildPlatformWaiterHandshake",
        "Wait-BuildPlatformWaiterAcknowledgement"
    )
    if (($ExportedCommandNames -join "|") -cne ($ExpectedCommandNames -join "|")) {
        throw "The waiter handshake module exported '$($ExportedCommandNames -join ", ")' instead of '$($ExpectedCommandNames -join ", ")'."
    }

    $Handshake = Resolve-BuildPlatformWaiterHandshake `
        -ProtocolValue 'ack-v1' `
        -InvocationIdWasSupplied $true `
        -InvocationId $InvocationId `
        -OutputRootPath $OutputRoot
    if (-not $Handshake.Enabled) { throw 'ack-v1 was not enabled.' }
    if ($Handshake.ProofPath -cne (Join-Path $OutputRoot '.helengine-build-state.b40ab19d-4d81-4db0-a0d4-9b818b49c7c0.json')) { throw 'Proof path changed.' }
    if ($Handshake.AcknowledgementPath -cne (Join-Path $OutputRoot '.helengine-build-state.b40ab19d-4d81-4db0-a0d4-9b818b49c7c0.ack')) { throw 'Acknowledgment path changed.' }

    $DirectHandshake = Resolve-BuildPlatformWaiterHandshake `
        -ProtocolValue $null `
        -InvocationIdWasSupplied $false `
        -InvocationId $InvocationId `
        -OutputRootPath ($OutputRoot + [IO.Path]::DirectorySeparatorChar)
    if ($DirectHandshake.Enabled) {
        throw "Direct mode was enabled."
    }
    if ($DirectHandshake.InvocationId -cne $InvocationId) {
        throw "Direct mode changed the canonical invocation ID."
    }

    foreach ($InvalidProtocolCase in @(
            [pscustomobject]@{ Name = "uppercase protocol"; ProtocolValue = "ACK-V1"; InvocationIdWasSupplied = $true; InvocationId = $InvocationId },
            [pscustomobject]@{ Name = "missing supplied ID"; ProtocolValue = "ack-v1"; InvocationIdWasSupplied = $false; InvocationId = $InvocationId },
            [pscustomobject]@{ Name = "malformed ID"; ProtocolValue = "ack-v1"; InvocationIdWasSupplied = $true; InvocationId = "not-a-guid" },
            [pscustomobject]@{ Name = "uppercase ID"; ProtocolValue = "ack-v1"; InvocationIdWasSupplied = $true; InvocationId = $InvocationId.ToUpperInvariant() },
            [pscustomobject]@{ Name = "padded ID"; ProtocolValue = "ack-v1"; InvocationIdWasSupplied = $true; InvocationId = (" " + $InvocationId) }
        )) {
        Assert-Throws -Description $InvalidProtocolCase.Name -Action {
            Resolve-BuildPlatformWaiterHandshake `
                -ProtocolValue $InvalidProtocolCase.ProtocolValue `
                -InvocationIdWasSupplied $InvalidProtocolCase.InvocationIdWasSupplied `
                -InvocationId $InvalidProtocolCase.InvocationId `
                -OutputRootPath $OutputRoot
        }
    }

    [IO.File]::WriteAllText($Handshake.AcknowledgementPath, $InvocationId)
    $OutputBeforePreExistingAcknowledgement = @(Get-ChildItem -LiteralPath $OutputRoot -Force | ForEach-Object Name | Sort-Object) -join "|"
    Assert-Throws -Description "A pre-existing acknowledgment" -Action {
        Resolve-BuildPlatformWaiterHandshake `
            -ProtocolValue "ack-v1" `
            -InvocationIdWasSupplied $true `
            -InvocationId $InvocationId `
            -OutputRootPath $OutputRoot
    }
    $OutputAfterPreExistingAcknowledgement = @(Get-ChildItem -LiteralPath $OutputRoot -Force | ForEach-Object Name | Sort-Object) -join "|"
    if ($OutputAfterPreExistingAcknowledgement -cne $OutputBeforePreExistingAcknowledgement) {
        throw "A pre-existing acknowledgment validation created an additional path."
    }
    Remove-Item -LiteralPath $Handshake.AcknowledgementPath -Force

    foreach ($InvalidAcknowledgementContents in @(
            "00000000-0000-0000-0000-000000000000",
            $InvocationId.ToUpperInvariant(),
            $InvocationId.Substring(0, $InvocationId.Length - 1),
            ($InvocationId + [Environment]::NewLine)
        )) {
        [IO.File]::WriteAllText($Handshake.AcknowledgementPath, $InvalidAcknowledgementContents)
        if (Wait-BuildPlatformWaiterAcknowledgement -Handshake $Handshake -Timeout ([TimeSpan]::FromMilliseconds(100))) {
            throw "An inexact acknowledgment '$InvalidAcknowledgementContents' was accepted."
        }
    }

    [IO.File]::WriteAllText($Handshake.AcknowledgementPath, $InvocationId)
    $SiblingSentinelPath = Join-Path $OutputRoot ".helengine-build-state.$InvocationId.sibling"
    [IO.File]::WriteAllText($Handshake.ProofPath, "proof")
    [IO.File]::WriteAllText($SiblingSentinelPath, "sentinel")
    if (-not (Wait-BuildPlatformWaiterAcknowledgement -Handshake $Handshake -Timeout ([TimeSpan]::FromMilliseconds(100)))) {
        throw "The exact no-newline acknowledgment was not accepted."
    }
    Remove-BuildPlatformWaiterAcknowledgement -Handshake $Handshake
    if (Test-Path -LiteralPath $Handshake.AcknowledgementPath) {
        throw "Exact acknowledgment was not removed."
    }
    if (-not (Test-Path -LiteralPath $Handshake.ProofPath) -or -not (Test-Path -LiteralPath $SiblingSentinelPath)) {
        throw "Acknowledgment removal deleted a proof or sibling sentinel."
    }

    Write-Output "WAITER_HANDSHAKE_TEST_PASS"
} finally {
    Remove-Module BuildPlatformWaiterHandshake -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $TestRootPath) {
        Remove-Item -LiteralPath $TestRootPath -Recurse -Force
    }
}

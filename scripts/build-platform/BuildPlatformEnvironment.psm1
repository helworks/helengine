Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$BuildPlatformEnvironmentVariableNames = @(
    "HELENGINE_BUILD_CACHE_ROOT",
    "HELENGINE_BUILD_CONFIGURATION",
    "HELENGINE_BUILD_PROFILE",
    "HELENGINE_SOURCE_ROOT"
)

function Save-BuildPlatformEnvironmentState {
    $State = @{}
    foreach ($EnvironmentVariableName in $BuildPlatformEnvironmentVariableNames) {
        $Value = [Environment]::GetEnvironmentVariable(
            $EnvironmentVariableName,
            [EnvironmentVariableTarget]::Process
        )
        $State[$EnvironmentVariableName] = [pscustomobject]@{
            Exists = $null -ne $Value
            Value = $Value
        }
    }
    return $State
}

function Restore-BuildPlatformEnvironmentState {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary]$State
    )

    foreach ($EnvironmentVariableName in $BuildPlatformEnvironmentVariableNames) {
        if (-not $State.Contains($EnvironmentVariableName)) {
            throw "Saved environment state is missing '$EnvironmentVariableName'."
        }

        $SavedVariable = $State[$EnvironmentVariableName]
        $RestoredValue = if ($SavedVariable.Exists) { $SavedVariable.Value } else { $null }
        [Environment]::SetEnvironmentVariable(
            $EnvironmentVariableName,
            $RestoredValue,
            [EnvironmentVariableTarget]::Process
        )
    }
}

Export-ModuleMember -Function @(
    'Save-BuildPlatformEnvironmentState',
    'Restore-BuildPlatformEnvironmentState'
)

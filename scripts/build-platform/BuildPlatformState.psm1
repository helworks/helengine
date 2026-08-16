Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-BuildPlatformState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$StatePath,

        [Parameter(Mandatory = $true)]
        [string]$BuildId,

        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,

        [Parameter(Mandatory = $true)]
        [string]$Platform,

        [Parameter(Mandatory = $true)]
        [string]$BuildProfile,

        [Parameter(Mandatory = $true)]
        [string]$Configuration,

        [Parameter(Mandatory = $true)]
        [string]$StartedUtc,

        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [object]$CompletedUtc,

        [Parameter(Mandatory = $true)]
        [string]$Status,

        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [object]$ExitCode
    )

    $State = [ordered]@{
        buildId = $BuildId
        projectPath = $ProjectPath
        platform = $Platform
        buildProfile = $BuildProfile
        configuration = $Configuration
        startedUtc = $StartedUtc
        completedUtc = $CompletedUtc
        status = $Status
        exitCode = $ExitCode
    }
    $State | ConvertTo-Json | Set-Content -LiteralPath $StatePath -Encoding UTF8
}

Export-ModuleMember -Function 'Write-BuildPlatformState'

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-BuildPlatformCanonicalDirectoryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $FullPath = [System.IO.Path]::GetFullPath($Path)
    $RootPath = [System.IO.Path]::GetPathRoot($FullPath)
    if ($FullPath.Length -le $RootPath.Length) {
        return $RootPath
    }

    $DirectorySeparators = [char[]]@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar
    )
    return $FullPath.TrimEnd($DirectorySeparators)
}

function Get-BuildPlatformProjectHash {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRootPath
    )

    if ([string]::IsNullOrWhiteSpace($ProjectRootPath)) {
        throw "Project root path must be provided."
    }

    $FullProjectRootPath = Get-BuildPlatformCanonicalDirectoryPath -Path $ProjectRootPath
    $ProjectRootBytes = [System.Text.Encoding]::UTF8.GetBytes($FullProjectRootPath)
    $Sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $HashBytes = $Sha256.ComputeHash($ProjectRootBytes)
    } finally {
        $Sha256.Dispose()
    }

    $Builder = New-Object System.Text.StringBuilder
    for ($Index = 0; $Index -lt 16; $Index++) {
        $null = $Builder.Append($HashBytes[$Index].ToString("x2"))
    }
    return $Builder.ToString()
}

function Get-BuildPlatformSafeSegment {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "Path segment must be provided."
    }

    $InvalidCharacters = [System.IO.Path]::GetInvalidFileNameChars()
    $Builder = New-Object System.Text.StringBuilder
    foreach ($Character in $Value.ToCharArray()) {
        if ($InvalidCharacters -contains $Character) {
            $null = $Builder.Append('_')
        } else {
            $null = $Builder.Append($Character)
        }
    }
    return $Builder.ToString()
}

function Resolve-BuildPlatformCacheLayout {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CacheRootPath,

        [Parameter(Mandatory = $true)]
        [string]$ProjectRootPath,

        [Parameter(Mandatory = $true)]
        [string]$Platform,

        [Parameter(Mandatory = $true)]
        [string]$Configuration,

        [Parameter(Mandatory = $true)]
        [string]$BuildProfile
    )

    if ([string]::IsNullOrWhiteSpace($CacheRootPath)) {
        throw "Cache root path must be provided."
    }

    $FullCacheRootPath = Get-BuildPlatformCanonicalDirectoryPath -Path $CacheRootPath
    $ProjectHash = Get-BuildPlatformProjectHash -ProjectRootPath $ProjectRootPath
    $PlatformSegment = Get-BuildPlatformSafeSegment -Value $Platform
    $ConfigurationSegment = Get-BuildPlatformSafeSegment -Value $Configuration.ToLowerInvariant()
    $ProfileSegment = Get-BuildPlatformSafeSegment -Value $BuildProfile
    $ProjectCacheRootPath = Join-Path $FullCacheRootPath ("projects\" + $ProjectHash)

    return [pscustomobject]@{
        CacheRootPath = $FullCacheRootPath
        ProjectHash = $ProjectHash
        ProjectCacheRootPath = $ProjectCacheRootPath
        LockPath = Join-Path $FullCacheRootPath ("locks\" + $ProjectHash + ".lock")
        EditorArtifactsPath = Join-Path $ProjectCacheRootPath ("editor\" + $ConfigurationSegment + "\artifacts")
        EditorPublishPath = Join-Path $ProjectCacheRootPath ("editor\" + $ConfigurationSegment + "\publish")
        PlatformCacheRootPath = Join-Path $ProjectCacheRootPath ("platforms\" + $PlatformSegment + "\" + $ConfigurationSegment + "\" + $ProfileSegment)
        MetadataPath = Join-Path $ProjectCacheRootPath "cache-metadata.json"
    }
}

function Write-BuildPlatformCacheMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Layout,

        [Parameter(Mandatory = $true)]
        [string]$ProjectRootPath
    )

    $CanonicalProjectRootPath = Get-BuildPlatformCanonicalDirectoryPath -Path $ProjectRootPath
    $null = New-Item -ItemType Directory -Path $Layout.ProjectCacheRootPath -Force
    $Metadata = [ordered]@{
        projectRootPath = $CanonicalProjectRootPath
        lastUsedUtc = [DateTime]::UtcNow.ToString("o")
    }
    $Metadata | ConvertTo-Json | Set-Content -LiteralPath $Layout.MetadataPath -Encoding UTF8
}

Export-ModuleMember -Function @(
    'Get-BuildPlatformProjectHash',
    'Get-BuildPlatformSafeSegment',
    'Resolve-BuildPlatformCacheLayout',
    'Write-BuildPlatformCacheMetadata'
)

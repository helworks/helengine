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

function Join-BuildPlatformStrictDescendantPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ParentPath,

        [Parameter(Mandatory = $true)]
        [string]$ChildPath
    )

    $CanonicalParentPath = Get-BuildPlatformCanonicalDirectoryPath -Path $ParentPath
    $CanonicalCandidatePath = Get-BuildPlatformCanonicalDirectoryPath -Path (Join-Path $CanonicalParentPath $ChildPath)
    $ParentPrefix = $CanonicalParentPath
    if (-not $ParentPrefix.EndsWith([System.IO.Path]::DirectorySeparatorChar) -and
        -not $ParentPrefix.EndsWith([System.IO.Path]::AltDirectorySeparatorChar)) {
        $ParentPrefix += [System.IO.Path]::DirectorySeparatorChar
    }

    if ($CanonicalCandidatePath.Equals($CanonicalParentPath, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not $CanonicalCandidatePath.StartsWith($ParentPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path '$CanonicalCandidatePath' must be a strict descendant of '$CanonicalParentPath'."
    }

    return $CanonicalCandidatePath
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
    $ProjectIdentityPath = $FullProjectRootPath.ToLowerInvariant()
    $ProjectRootBytes = [System.Text.Encoding]::UTF8.GetBytes($ProjectIdentityPath)
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
    if ($Value -eq "." -or $Value -eq "..") {
        throw "Path segment '$Value' is not allowed."
    }
    if ($Value.EndsWith(".", [System.StringComparison]::Ordinal) -or
        $Value.EndsWith(" ", [System.StringComparison]::Ordinal)) {
        throw "Path segment '$Value' has a trailing Windows alias character."
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
    $SafeSegment = $Builder.ToString()
    if ($SafeSegment -eq "." -or $SafeSegment -eq "..") {
        throw "Path segment '$Value' resolves to a traversal segment."
    }
    if ($SafeSegment.EndsWith(".", [System.StringComparison]::Ordinal) -or
        $SafeSegment.EndsWith(" ", [System.StringComparison]::Ordinal)) {
        throw "Path segment '$Value' resolves to a trailing Windows alias character."
    }

    $ReservedDevicePattern = '^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)'
    $ReservedDeviceOptions = [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant
    if ([regex]::IsMatch($SafeSegment, $ReservedDevicePattern, $ReservedDeviceOptions)) {
        throw "Path segment '$Value' uses a reserved Windows device basename."
    }
    return $SafeSegment
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
    $LocksRootPath = Join-BuildPlatformStrictDescendantPath -ParentPath $FullCacheRootPath -ChildPath "locks"
    $LockPath = Join-BuildPlatformStrictDescendantPath -ParentPath $LocksRootPath -ChildPath ($ProjectHash + ".lock")
    $ProjectsRootPath = Join-BuildPlatformStrictDescendantPath -ParentPath $FullCacheRootPath -ChildPath "projects"
    $ProjectCacheRootPath = Join-BuildPlatformStrictDescendantPath -ParentPath $ProjectsRootPath -ChildPath $ProjectHash
    $MetadataPath = Join-BuildPlatformStrictDescendantPath -ParentPath $ProjectCacheRootPath -ChildPath "cache-metadata.json"
    $EditorRootPath = Join-BuildPlatformStrictDescendantPath -ParentPath $ProjectCacheRootPath -ChildPath "editor"
    $EditorConfigurationRootPath = Join-BuildPlatformStrictDescendantPath -ParentPath $EditorRootPath -ChildPath $ConfigurationSegment
    $EditorArtifactsPath = Join-BuildPlatformStrictDescendantPath -ParentPath $EditorConfigurationRootPath -ChildPath "artifacts"
    $EditorPublishPath = Join-BuildPlatformStrictDescendantPath -ParentPath $EditorConfigurationRootPath -ChildPath "publish"
    $PlatformsRootPath = Join-BuildPlatformStrictDescendantPath -ParentPath $ProjectCacheRootPath -ChildPath "platforms"
    $PlatformRootPath = Join-BuildPlatformStrictDescendantPath -ParentPath $PlatformsRootPath -ChildPath $PlatformSegment
    $PlatformConfigurationRootPath = Join-BuildPlatformStrictDescendantPath -ParentPath $PlatformRootPath -ChildPath $ConfigurationSegment
    $PlatformCacheRootPath = Join-BuildPlatformStrictDescendantPath -ParentPath $PlatformConfigurationRootPath -ChildPath $ProfileSegment

    return [pscustomobject]@{
        CacheRootPath = $FullCacheRootPath
        ProjectHash = $ProjectHash
        ProjectCacheRootPath = $ProjectCacheRootPath
        LockPath = $LockPath
        EditorConfigurationRootPath = $EditorConfigurationRootPath
        EditorArtifactsPath = $EditorArtifactsPath
        EditorPublishPath = $EditorPublishPath
        PlatformCacheRootPath = $PlatformCacheRootPath
        MetadataPath = $MetadataPath
    }
}

function Get-BuildPlatformGuardedDeleteTarget {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AllowedRootPath,

        [Parameter(Mandatory = $true)]
        [string]$TargetPath
    )

    if ([string]::IsNullOrWhiteSpace($AllowedRootPath)) {
        throw "Allowed delete root must be provided."
    }
    if ([string]::IsNullOrWhiteSpace($TargetPath)) {
        throw "Delete target must be provided."
    }

    $TargetSegments = [regex]::Split($TargetPath, '[\\/]+')
    if ($TargetSegments -contains "..") {
        throw "Delete target '$TargetPath' contains a parent traversal segment."
    }

    $CanonicalAllowedRootPath = Get-BuildPlatformCanonicalDirectoryPath -Path $AllowedRootPath
    $CanonicalTargetPath = Get-BuildPlatformCanonicalDirectoryPath -Path $TargetPath
    $AllowedPrefix = $CanonicalAllowedRootPath
    if (-not $AllowedPrefix.EndsWith([System.IO.Path]::DirectorySeparatorChar) -and
        -not $AllowedPrefix.EndsWith([System.IO.Path]::AltDirectorySeparatorChar)) {
        $AllowedPrefix += [System.IO.Path]::DirectorySeparatorChar
    }

    if ($CanonicalTargetPath.Equals($CanonicalAllowedRootPath, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not $CanonicalTargetPath.StartsWith($AllowedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Delete target '$CanonicalTargetPath' must be a strict descendant of '$CanonicalAllowedRootPath'."
    }

    $FileSystemRootPath = [System.IO.Path]::GetPathRoot($CanonicalTargetPath)
    $CurrentPath = $FileSystemRootPath
    if (Test-Path -LiteralPath $CurrentPath) {
        $RootItem = Get-Item -LiteralPath $CurrentPath -Force
        if (($RootItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Delete target '$CanonicalTargetPath' traverses reparse point '$CurrentPath'."
        }
    }

    $RelativeTargetPath = $CanonicalTargetPath.Substring($FileSystemRootPath.Length)
    foreach ($Segment in [regex]::Split($RelativeTargetPath, '[\\/]+')) {
        if ([string]::IsNullOrEmpty($Segment)) {
            continue
        }
        $CurrentPath = Join-Path $CurrentPath $Segment
        if (-not (Test-Path -LiteralPath $CurrentPath)) {
            continue
        }

        $Item = Get-Item -LiteralPath $CurrentPath -Force
        if (($Item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Delete target '$CanonicalTargetPath' traverses reparse point '$CurrentPath'."
        }
    }

    return $CanonicalTargetPath
}

function Remove-BuildPlatformGuardedDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AllowedRootPath,

        [Parameter(Mandatory = $true)]
        [string[]]$TargetPath
    )

    $CanonicalTargetPaths = @()
    foreach ($CandidatePath in $TargetPath) {
        $CanonicalTargetPaths += Get-BuildPlatformGuardedDeleteTarget `
            -AllowedRootPath $AllowedRootPath `
            -TargetPath $CandidatePath
    }

    foreach ($CanonicalTargetPath in $CanonicalTargetPaths) {
        if (Test-Path -LiteralPath $CanonicalTargetPath) {
            Remove-Item -LiteralPath $CanonicalTargetPath -Recurse -Force
        }
    }
}

function Remove-BuildPlatformSelectedCache {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Layout
    )

    Remove-BuildPlatformGuardedDirectory `
        -AllowedRootPath $Layout.ProjectCacheRootPath `
        -TargetPath @(
            $Layout.EditorConfigurationRootPath,
            $Layout.PlatformCacheRootPath
        )
}

function Remove-BuildPlatformExpiredProjectCaches {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CacheRootPath,

        [Parameter(Mandatory = $true)]
        [int]$OlderThanDays,

        [Parameter()]
        [DateTime]$NowUtc = [DateTime]::UtcNow
    )

    if ($OlderThanDays -lt 0) {
        throw "Cache prune age must be zero or positive."
    }
    if ($OlderThanDays -eq 0) {
        return
    }

    $CanonicalCacheRootPath = Get-BuildPlatformCanonicalDirectoryPath -Path $CacheRootPath
    $ProjectsRootPath = Join-BuildPlatformStrictDescendantPath `
        -ParentPath $CanonicalCacheRootPath `
        -ChildPath "projects"
    if (-not (Test-Path -LiteralPath $ProjectsRootPath -PathType Container)) {
        return
    }

    $ExpirationUtc = $NowUtc.ToUniversalTime().AddDays(-$OlderThanDays)
    $ProjectDirectories = Get-ChildItem -LiteralPath $ProjectsRootPath -Directory -Force
    foreach ($ProjectDirectory in $ProjectDirectories) {
        if ($ProjectDirectory.Name -cnotmatch '^[0-9a-f]{32}$') {
            continue
        }

        try {
            $ProjectCacheRootPath = Get-BuildPlatformGuardedDeleteTarget `
                -AllowedRootPath $ProjectsRootPath `
                -TargetPath $ProjectDirectory.FullName
            $MetadataPath = Join-Path $ProjectCacheRootPath "cache-metadata.json"
            if (-not (Test-Path -LiteralPath $MetadataPath -PathType Leaf)) {
                continue
            }
            $MetadataItem = Get-Item -LiteralPath $MetadataPath -Force
            if (($MetadataItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                continue
            }

            $Metadata = Get-Content -LiteralPath $MetadataPath -Raw | ConvertFrom-Json
            $ProjectRootPath = [string]$Metadata.projectRootPath
            $LastUsedText = [string]$Metadata.lastUsedUtc
            if ([string]::IsNullOrWhiteSpace($ProjectRootPath) -or
                [string]::IsNullOrWhiteSpace($LastUsedText)) {
                continue
            }

            $CanonicalProjectRootPath = Get-BuildPlatformCanonicalDirectoryPath -Path $ProjectRootPath
            $LastUsed = [DateTimeOffset]::MinValue
            if (-not [DateTimeOffset]::TryParse(
                    $LastUsedText,
                    [System.Globalization.CultureInfo]::InvariantCulture,
                    [System.Globalization.DateTimeStyles]::RoundtripKind,
                    [ref]$LastUsed)) {
                continue
            }
            if ($LastUsed.UtcDateTime -ge $ExpirationUtc) {
                continue
            }

            $ExpectedHash = Get-BuildPlatformProjectHash -ProjectRootPath $CanonicalProjectRootPath
            if ($ExpectedHash -cne $ProjectDirectory.Name) {
                continue
            }

            $LocksRootPath = Join-BuildPlatformStrictDescendantPath `
                -ParentPath $CanonicalCacheRootPath `
                -ChildPath "locks"
            $LockPath = Join-BuildPlatformStrictDescendantPath `
                -ParentPath $LocksRootPath `
                -ChildPath ($ProjectDirectory.Name + ".lock")
            if (Test-BuildPlatformProjectLockHeld -LockPath $LockPath) {
                continue
            }

            Remove-BuildPlatformGuardedDirectory `
                -AllowedRootPath $ProjectsRootPath `
                -TargetPath @($ProjectCacheRootPath)
        } catch {
            Write-Warning "Skipping unsafe or invalid project cache '$($ProjectDirectory.FullName)': $($_.Exception.Message)"
        }
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
    'Write-BuildPlatformCacheMetadata',
    'Remove-BuildPlatformSelectedCache',
    'Remove-BuildPlatformExpiredProjectCaches'
)

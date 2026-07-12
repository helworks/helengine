[CmdletBinding()]
param(
    [Parameter()]
    [string]$OutputRoot = "",

    [Parameter()]
    [string]$Configuration = "Release",

    [Parameter()]
    [string]$EditorProject = "",

    [Parameter()]
    [string]$PlatformsManifest = "",

    [Parameter()]
    [string[]]$PlatformIds = @(),

    [Parameter()]
    [string]$CodegenProject = "",

    [Parameter()]
    [string]$GeneratedCoreCppRoot = "",

    [Parameter()]
    [string]$Version = "",

    [Parameter()]
    [switch]$SkipEditor,

    [Parameter()]
    [switch]$SkipPlatforms,

    [Parameter()]
    [switch]$NoZip
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter()]
        [string]$BasePath = ""
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "Path must be provided."
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    if ([string]::IsNullOrWhiteSpace($BasePath)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

function Reset-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }

    $null = New-Item -ItemType Directory -Path $Path -Force
}

function Ensure-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        $null = New-Item -ItemType Directory -Path $Path -Force
    }
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter()]
        [string]$WorkingDirectory = ""
    )

    $Display = @("dotnet")
    foreach ($Argument in $Arguments) {
        if ($Argument -match '[\s"]') {
            $Display += '"' + $Argument.Replace('"', '\"') + '"'
        } else {
            $Display += $Argument
        }
    }

    Write-Host ($Display -join " ")

    if ([string]::IsNullOrWhiteSpace($WorkingDirectory)) {
        & dotnet @Arguments | Out-Host
    } else {
        Push-Location $WorkingDirectory
        try {
            & dotnet @Arguments | Out-Host
        } finally {
            Pop-Location
        }
    }

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed with exit code $LASTEXITCODE."
    }
}

function Get-ReleaseVersionFromAssembly {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AssemblyPath
    )

    if (-not (Test-Path -LiteralPath $AssemblyPath -PathType Leaf)) {
        throw "Assembly was not found at '$AssemblyPath'."
    }

    $FileVersionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($AssemblyPath)
    if (-not [string]::IsNullOrWhiteSpace($FileVersionInfo.ProductVersion)) {
        return $FileVersionInfo.ProductVersion
    }

    $AssemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($AssemblyPath)
    if ($AssemblyName.Version -ne $null) {
        return $AssemblyName.Version.ToString()
    }

    throw "Could not resolve a version from '$AssemblyPath'."
}

function Load-PlatformManifestEntries {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ManifestPath
    )

    if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
        throw "Platforms manifest was not found at '$ManifestPath'."
    }

    $Document = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    if ($null -eq $Document -or $null -eq $Document.platforms) {
        throw "Platforms manifest '$ManifestPath' does not contain a platforms array."
    }

    return @($Document.platforms)
}

function Get-OptionalObjectPropertyValue {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Object,

        [Parameter(Mandatory = $true)]
        [string]$PropertyName
    )

    if ($null -eq $Object) {
        return $null
    }

    $Property = $Object.PSObject.Properties[$PropertyName]
    if ($null -eq $Property) {
        return $null
    }

    return $Property.Value
}

function Get-OptionalObjectPropertyString {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Object,

        [Parameter(Mandatory = $true)]
        [string]$PropertyName
    )

    $Value = Get-OptionalObjectPropertyValue -Object $Object -PropertyName $PropertyName
    if ($null -eq $Value) {
        return ""
    }

    return [string]$Value
}

function Select-PlatformEntries {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Entries,

        [Parameter()]
        [string[]]$RequestedPlatformIds = @()
    )

    $AllEntries = @($Entries)
    $UniqueEntries = @(
        $AllEntries |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_.platformId) } |
            Group-Object -Property platformId |
            ForEach-Object { $_.Group[0] }
    )

    if (@($RequestedPlatformIds).Count -eq 0) {
        return @($UniqueEntries | Sort-Object { [string]$_.platformId })
    }

    $SelectedEntries = foreach ($PlatformId in ($RequestedPlatformIds | Sort-Object -Unique)) {
        $Match = $UniqueEntries | Where-Object { [string]$_.platformId -eq $PlatformId } | Select-Object -First 1
        if ($null -eq $Match) {
            throw "Platform '$PlatformId' was not found in the source manifest."
        }

        $Match
    }

    return @($SelectedEntries)
}

function Get-RelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BasePath,

        [Parameter(Mandatory = $true)]
        [string]$TargetPath
    )

    $ResolvedBasePath = [System.IO.Path]::GetFullPath($BasePath)
    $ResolvedTargetPath = [System.IO.Path]::GetFullPath($TargetPath)
    if (-not $ResolvedBasePath.EndsWith([System.IO.Path]::DirectorySeparatorChar.ToString(), [System.StringComparison]::Ordinal)) {
        $ResolvedBasePath += [System.IO.Path]::DirectorySeparatorChar
    }

    $BaseUri = New-Object System.Uri($ResolvedBasePath)
    $TargetUri = New-Object System.Uri($ResolvedTargetPath)
    $RelativeUri = $BaseUri.MakeRelativeUri($TargetUri)
    return [System.Uri]::UnescapeDataString($RelativeUri.ToString()).Replace('\', '/')
}

function Copy-DirectoryFiltered {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,

        [Parameter(Mandatory = $true)]
        [string]$DestinationPath,

        [Parameter(Mandatory = $true)]
        [string[]]$ExcludedDirectoryNames
    )

    if (-not (Test-Path -LiteralPath $SourcePath -PathType Container)) {
        throw "Source directory '$SourcePath' was not found."
    }

    Ensure-Directory -Path $DestinationPath

    foreach ($Item in (Get-ChildItem -LiteralPath $SourcePath -Force)) {
        $DestinationItemPath = Join-Path $DestinationPath $Item.Name
        if ($Item.PSIsContainer) {
            if ($ExcludedDirectoryNames -contains $Item.Name) {
                continue
            }

            if (($Item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                continue
            }

            Copy-DirectoryFiltered -SourcePath $Item.FullName -DestinationPath $DestinationItemPath -ExcludedDirectoryNames $ExcludedDirectoryNames
            continue
        }

        Copy-Item -LiteralPath $Item.FullName -Destination $DestinationItemPath -Force
    }
}

function Find-BuilderProjectPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PlayerSourceRootPath
    )

    $BuilderRootPath = Join-Path $PlayerSourceRootPath "builder"
    if (-not (Test-Path -LiteralPath $BuilderRootPath -PathType Container)) {
        throw "Builder directory was not found under '$PlayerSourceRootPath'."
    }

    $Projects = @(Get-ChildItem -LiteralPath $BuilderRootPath -Filter "*.csproj" -File)
    if ($Projects.Count -eq 0) {
        throw "No builder project was found under '$BuilderRootPath'."
    }
    if ($Projects.Count -gt 1) {
        throw "Expected one builder project under '$BuilderRootPath' but found $($Projects.Count)."
    }

    return $Projects[0].FullName
}

function Get-BuilderAssemblyFileName {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Entry,

        [Parameter()]
        [string]$PluginManifestPath = "",

        [Parameter(Mandatory = $true)]
        [string]$BuilderProjectPath
    )

    $BuilderAssemblyPath = Get-OptionalObjectPropertyString -Object $Entry -PropertyName "builderAssemblyPath"
    if (-not [string]::IsNullOrWhiteSpace($BuilderAssemblyPath)) {
        return [System.IO.Path]::GetFileName($BuilderAssemblyPath)
    }

    if (-not [string]::IsNullOrWhiteSpace($PluginManifestPath) -and (Test-Path -LiteralPath $PluginManifestPath -PathType Leaf)) {
        $PluginDocument = Get-Content -LiteralPath $PluginManifestPath -Raw | ConvertFrom-Json
        if ($PluginDocument -ne $null -and -not [string]::IsNullOrWhiteSpace($PluginDocument.builderAssemblyPath)) {
            return [System.IO.Path]::GetFileName([string]$PluginDocument.builderAssemblyPath)
        }
    }

    return ([System.IO.Path]::GetFileNameWithoutExtension($BuilderProjectPath) + ".dll")
}

function Publish-EditorPackage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,

        [Parameter(Mandatory = $true)]
        [string]$Configuration,

        [Parameter(Mandatory = $true)]
        [string]$OutputPath,

        [Parameter(Mandatory = $true)]
        [string]$ArtifactsPath
    )

    Reset-Directory -Path $OutputPath
    Ensure-Directory -Path $ArtifactsPath

    Invoke-DotNet -Arguments @(
        "publish",
        $ProjectPath,
        "-c",
        $Configuration,
        "-o",
        $OutputPath,
        "--artifacts-path",
        $ArtifactsPath
    )

    $EditorAssemblyPath = Join-Path $OutputPath "helengine.editor.app.dll"
    if (-not (Test-Path -LiteralPath $EditorAssemblyPath -PathType Leaf)) {
        throw "Editor publish output is missing '$EditorAssemblyPath'."
    }

    return $EditorAssemblyPath
}

function Publish-CodegenPackage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,

        [Parameter(Mandatory = $true)]
        [string]$Configuration,

        [Parameter(Mandatory = $true)]
        [string]$OutputPath
    )

    Reset-Directory -Path $OutputPath
    Invoke-DotNet -Arguments @(
        "publish",
        $ProjectPath,
        "-c",
        $Configuration,
        "-o",
        $OutputPath,
        "-p:UseAppHost=true"
    )

    $CodegenExecutablePath = Join-Path $OutputPath "codegen.exe"
    if (-not (Test-Path -LiteralPath $CodegenExecutablePath -PathType Leaf)) {
        throw "Codegen publish output is missing '$CodegenExecutablePath'."
    }

    return $CodegenExecutablePath
}

function Publish-PlatformBuilder {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BuilderProjectPath,

        [Parameter(Mandatory = $true)]
        [string]$Configuration,

        [Parameter(Mandatory = $true)]
        [string]$OutputPath,

        [Parameter(Mandatory = $true)]
        [string]$HelEngineRoot
    )

    Reset-Directory -Path $OutputPath
    $OriginalHelengineRoot = $env:HELENGINE_ROOT
    try {
        $env:HELENGINE_ROOT = $HelEngineRoot
        Invoke-DotNet -Arguments @(
            "publish",
            $BuilderProjectPath,
            "-c",
            $Configuration,
            "-o",
            $OutputPath,
            "-p:HelengineRoot=$HelEngineRoot"
        )
    } finally {
        $env:HELENGINE_ROOT = $OriginalHelengineRoot
    }
}

function Write-PlatformManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ManifestPath,

        [Parameter(Mandatory = $true)]
        [object[]]$PlatformEntries
    )

    $ManifestObject = [ordered]@{
        platforms = @($PlatformEntries)
    }

    $Json = $ManifestObject | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText($ManifestPath, $Json)
}

function Create-ZipFromDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DirectoryPath,

        [Parameter(Mandatory = $true)]
        [string]$ZipPath
    )

    if (Test-Path -LiteralPath $ZipPath -PathType Leaf) {
        Remove-Item -LiteralPath $ZipPath -Force
    }

    Compress-Archive -Path $DirectoryPath -DestinationPath $ZipPath -CompressionLevel Optimal
}

if ($SkipEditor -and $SkipPlatforms) {
    throw "At least one of editor or platforms packaging must be enabled."
}

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$HelEngineRoot = Resolve-FullPath -Path ".." -BasePath $ScriptRoot
$ResolvedOutputRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Resolve-FullPath -Path "release" -BasePath $ScriptRoot
} else {
    Resolve-FullPath -Path $OutputRoot
}
$ResolvedEditorProject = if ([string]::IsNullOrWhiteSpace($EditorProject)) {
    Resolve-FullPath -Path "..\helengine.ui\helengine.editor.app\helengine.editor.app.csproj" -BasePath $ScriptRoot
} else {
    Resolve-FullPath -Path $EditorProject
}
$ResolvedPlatformsManifest = if ([string]::IsNullOrWhiteSpace($PlatformsManifest)) {
    Resolve-FullPath -Path "..\user_settings\platforms.json" -BasePath $ScriptRoot
} else {
    Resolve-FullPath -Path $PlatformsManifest
}
$ResolvedCodegenProject = if ([string]::IsNullOrWhiteSpace($CodegenProject)) {
    Resolve-FullPath -Path "..\..\csharpcodegen\codegen\codegen.csproj" -BasePath $ScriptRoot
} else {
    Resolve-FullPath -Path $CodegenProject
}
$ResolvedGeneratedCoreCppRoot = if ([string]::IsNullOrWhiteSpace($GeneratedCoreCppRoot)) {
    Resolve-FullPath -Path "..\tmp\helengine-core-cpp-regenerated" -BasePath $ScriptRoot
} else {
    Resolve-FullPath -Path $GeneratedCoreCppRoot
}

Ensure-Directory -Path $ResolvedOutputRoot

$TempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("helengine-release-" + [Guid]::NewGuid().ToString("N"))
$TempEditorRoot = Join-Path $TempRoot "editor"
$TempEditorArtifactsRoot = Join-Path $TempRoot "editor-artifacts"
$TempPlatformsRoot = Join-Path $TempRoot "platforms"

$ReleaseVersion = $Version
$ReleaseRootPath = ""

try {
    Ensure-Directory -Path $TempRoot

    if (-not $SkipEditor) {
        $EditorAssemblyPath = Publish-EditorPackage `
            -ProjectPath $ResolvedEditorProject `
            -Configuration $Configuration `
            -OutputPath $TempEditorRoot `
            -ArtifactsPath $TempEditorArtifactsRoot

        if ([string]::IsNullOrWhiteSpace($ReleaseVersion)) {
            $ReleaseVersion = Get-ReleaseVersionFromAssembly -AssemblyPath $EditorAssemblyPath
        }
    }

    if ([string]::IsNullOrWhiteSpace($ReleaseVersion)) {
        $ManifestEntries = @(Load-PlatformManifestEntries -ManifestPath $ResolvedPlatformsManifest)
        if (@($ManifestEntries).Count -eq 0 -or [string]::IsNullOrWhiteSpace($ManifestEntries[0].engineVersion)) {
            throw "Could not resolve a release version from the source platforms manifest."
        }

        $ReleaseVersion = [string]$ManifestEntries[0].engineVersion
    }

    $ReleaseRootPath = Join-Path $ResolvedOutputRoot $ReleaseVersion
    Reset-Directory -Path $ReleaseRootPath

    if (-not $SkipEditor) {
        Move-Item -LiteralPath $TempEditorRoot -Destination (Join-Path $ReleaseRootPath "editor")
    }

    if (-not $SkipPlatforms) {
        Reset-Directory -Path $TempPlatformsRoot

        $SourceEntries = @(Select-PlatformEntries `
            -Entries (Load-PlatformManifestEntries -ManifestPath $ResolvedPlatformsManifest) `
            -RequestedPlatformIds $PlatformIds)

        if (@($SourceEntries).Count -eq 0) {
            throw "No platform entries were selected for packaging."
        }

        $SharedRootPath = Join-Path $TempPlatformsRoot "shared"
        $PackagedCodegenRootPath = Join-Path $SharedRootPath "codegen"
        $PackagedGeneratedCoreRootPath = Join-Path $SharedRootPath "generated-core"
        $NeedsGeneratedCore = $false
        foreach ($SourceEntry in $SourceEntries) {
            if (-not [string]::IsNullOrWhiteSpace((Get-OptionalObjectPropertyString -Object $SourceEntry -PropertyName "generatedCoreCppRootPath"))) {
                $NeedsGeneratedCore = $true
                break
            }
        }

        $CodegenExecutablePath = Publish-CodegenPackage `
            -ProjectPath $ResolvedCodegenProject `
            -Configuration $Configuration `
            -OutputPath $PackagedCodegenRootPath

        if ($NeedsGeneratedCore) {
            Ensure-Directory -Path $PackagedGeneratedCoreRootPath
            if (Test-Path -LiteralPath $ResolvedGeneratedCoreCppRoot -PathType Container) {
                Copy-DirectoryFiltered `
                    -SourcePath $ResolvedGeneratedCoreCppRoot `
                    -DestinationPath $PackagedGeneratedCoreRootPath `
                    -ExcludedDirectoryNames @(".git", ".codex", ".worktrees", "bin", "obj", "tmp", "coverage")
            }
        }

        $ExcludedPlayerDirectoryNames = @(
            ".git",
            ".codex",
            ".worktrees",
            ".vs",
            ".vscode",
            "bin",
            "obj",
            "tmp",
            "coverage"
        )

        $PackagedManifestEntries = @()
        foreach ($SourceEntry in $SourceEntries) {
            $PlatformId = [string]$SourceEntry.platformId
            $DisplayName = [string]$SourceEntry.displayName
            $PlayerSourceRootPath = [string]$SourceEntry.playerSourceRootPath
            $PluginManifestPath = Get-OptionalObjectPropertyString -Object $SourceEntry -PropertyName "pluginManifestPath"

            $ResolvedPlayerSourceRootPath = Resolve-FullPath -Path $PlayerSourceRootPath -BasePath (Split-Path -Parent $ResolvedPlatformsManifest)
            $ResolvedPluginManifestPath = if ([string]::IsNullOrWhiteSpace($PluginManifestPath)) {
                ""
            } else {
                Resolve-FullPath -Path $PluginManifestPath -BasePath (Split-Path -Parent $ResolvedPlatformsManifest)
            }

            $BuilderProjectPath = Find-BuilderProjectPath -PlayerSourceRootPath $ResolvedPlayerSourceRootPath
            $BuilderAssemblyFileName = Get-BuilderAssemblyFileName `
                -Entry $SourceEntry `
                -PluginManifestPath $ResolvedPluginManifestPath `
                -BuilderProjectPath $BuilderProjectPath

            $PackagedPlatformRootPath = Join-Path $TempPlatformsRoot ("platforms\" + $PlatformId)
            $PackagedPlayerRootPath = Join-Path $PackagedPlatformRootPath "player"
            $PackagedBuilderRootPath = Join-Path $PackagedPlatformRootPath "builder"

            Publish-PlatformBuilder `
                -BuilderProjectPath $BuilderProjectPath `
                -Configuration $Configuration `
                -OutputPath $PackagedBuilderRootPath `
                -HelEngineRoot $HelEngineRoot

            $PackagedBuilderAssemblyPath = Join-Path $PackagedBuilderRootPath $BuilderAssemblyFileName
            if (-not (Test-Path -LiteralPath $PackagedBuilderAssemblyPath -PathType Leaf)) {
                throw "Packaged builder output is missing '$PackagedBuilderAssemblyPath'."
            }

            Copy-DirectoryFiltered `
                -SourcePath $ResolvedPlayerSourceRootPath `
                -DestinationPath $PackagedPlayerRootPath `
                -ExcludedDirectoryNames $ExcludedPlayerDirectoryNames

            $ManifestEntry = [ordered]@{
                engineVersion = $ReleaseVersion
                platformId = $PlatformId
                displayName = $DisplayName
                builderAssemblyPath = Get-RelativePath -BasePath $TempPlatformsRoot -TargetPath $PackagedBuilderAssemblyPath
                playerSourceRootPath = Get-RelativePath -BasePath $TempPlatformsRoot -TargetPath $PackagedPlayerRootPath
            }

            if ($NeedsGeneratedCore) {
                $ManifestEntry.generatedCoreCppRootPath = Get-RelativePath -BasePath $TempPlatformsRoot -TargetPath $PackagedGeneratedCoreRootPath
            }

            $ManifestEntry.codegenToolPath = Get-RelativePath -BasePath $TempPlatformsRoot -TargetPath $CodegenExecutablePath

            if (-not [string]::IsNullOrWhiteSpace($ResolvedPluginManifestPath)) {
                $PackagedPluginManifestPath = Join-Path $PackagedPlayerRootPath ([System.IO.Path]::GetFileName($ResolvedPluginManifestPath))
                if (-not (Test-Path -LiteralPath $PackagedPluginManifestPath -PathType Leaf)) {
                    throw "Packaged plugin manifest is missing '$PackagedPluginManifestPath'."
                }

                $ManifestEntry.pluginManifestPath = Get-RelativePath -BasePath $TempPlatformsRoot -TargetPath $PackagedPluginManifestPath
            }

            $PackagedManifestEntries += ,$ManifestEntry
        }

        Write-PlatformManifest -ManifestPath (Join-Path $TempPlatformsRoot "platforms.json") -PlatformEntries @($PackagedManifestEntries)
        Move-Item -LiteralPath $TempPlatformsRoot -Destination (Join-Path $ReleaseRootPath "platforms")
    }

    if (-not $NoZip) {
        if (-not $SkipEditor) {
            Create-ZipFromDirectory `
                -DirectoryPath (Join-Path $ReleaseRootPath "editor") `
                -ZipPath (Join-Path $ReleaseRootPath ("helengine-editor-windows-" + $ReleaseVersion + ".zip"))
        }

        if (-not $SkipPlatforms) {
            Create-ZipFromDirectory `
                -DirectoryPath (Join-Path $ReleaseRootPath "platforms") `
                -ZipPath (Join-Path $ReleaseRootPath ("helengine-platforms-" + $ReleaseVersion + ".zip"))
        }

        Create-ZipFromDirectory `
            -DirectoryPath $ReleaseRootPath `
            -ZipPath (Join-Path $ResolvedOutputRoot ("helengine-release-" + $ReleaseVersion + ".zip"))
    }

    Write-Host ("Release version: " + $ReleaseVersion)
    Write-Host ("Release root: " + $ReleaseRootPath)
    if (-not $SkipEditor) {
        Write-Host ("Editor package: " + (Join-Path $ReleaseRootPath "editor"))
    }
    if (-not $SkipPlatforms) {
        Write-Host ("Platforms package: " + (Join-Path $ReleaseRootPath "platforms"))
    }
} finally {
    if (Test-Path -LiteralPath $TempRoot) {
        Remove-Item -LiteralPath $TempRoot -Recurse -Force
    }
}

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Project,

    [Parameter(Mandatory = $true)]
    [string]$Platform,

    [Parameter(Mandatory = $true)]
    [string]$Output,

    [Parameter()]
    [string]$Configuration = "Debug",

    [Parameter()]
    [string]$EditorProject = "",

    [Parameter()]
    [string[]]$AdditionalArgs = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-SafePathSegment {
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

function Get-ProjectIsolationHash {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRootPath
    )

    if ([string]::IsNullOrWhiteSpace($ProjectRootPath)) {
        throw "Project root path must be provided."
    }

    $FullProjectRootPath = [System.IO.Path]::GetFullPath($ProjectRootPath)
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

function Get-EditorArtifactsOutputPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$EditorArtifactsPath,

        [Parameter(Mandatory = $true)]
        [string]$Configuration
    )

    if ([string]::IsNullOrWhiteSpace($EditorArtifactsPath)) {
        throw "Editor artifacts path must be provided."
    } elseif ([string]::IsNullOrWhiteSpace($Configuration)) {
        throw "Configuration must be provided."
    }

    return Join-Path $EditorArtifactsPath ("bin\helengine.editor.app\" + $Configuration.ToLowerInvariant())
}

function Sync-EditorProjectReferenceOutputs {
    param(
        [Parameter(Mandatory = $true)]
        [string]$EditorArtifactsPath,

        [Parameter(Mandatory = $true)]
        [string]$Configuration
    )

    if ([string]::IsNullOrWhiteSpace($EditorArtifactsPath)) {
        throw "Editor artifacts path must be provided."
    } elseif ([string]::IsNullOrWhiteSpace($Configuration)) {
        throw "Configuration must be provided."
    }

    $EditorOutputPath = Get-EditorArtifactsOutputPath -EditorArtifactsPath $EditorArtifactsPath -Configuration $Configuration
    if (-not (Test-Path -LiteralPath $EditorOutputPath -PathType Container)) {
        throw "Editor app output was not found at '$EditorOutputPath'."
    }

    $ArtifactsBinRootPath = Join-Path $EditorArtifactsPath "bin"
    if (-not (Test-Path -LiteralPath $ArtifactsBinRootPath -PathType Container)) {
        throw "Editor artifacts bin root was not found at '$ArtifactsBinRootPath'."
    }

    $ProjectOutputDirectories = Get-ChildItem -LiteralPath $ArtifactsBinRootPath -Directory |
        Where-Object { $_.Name -ne "helengine.editor.app" }
    foreach ($ProjectOutputDirectory in $ProjectOutputDirectories) {
        $ProjectOutputPath = Join-Path $ProjectOutputDirectory.FullName $Configuration.ToLowerInvariant()
        if (-not (Test-Path -LiteralPath $ProjectOutputPath -PathType Container)) {
            continue
        }

        $OutputFiles = Get-ChildItem -LiteralPath $ProjectOutputPath -File
        foreach ($OutputFile in $OutputFiles) {
            Copy-Item -LiteralPath $OutputFile.FullName -Destination (Join-Path $EditorOutputPath $OutputFile.Name) -Force
        }
    }

    return $EditorOutputPath
}

function ConvertTo-NativeProcessArgument {
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

function Invoke-StreamingNativeProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList
    )

    if ([string]::IsNullOrWhiteSpace($FilePath)) {
        throw "Native process path must be provided."
    }

    $StartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $StartInfo.FileName = $FilePath
    $StartInfo.Arguments = (($ArgumentList | ForEach-Object { ConvertTo-NativeProcessArgument -Value $_ }) -join " ")
    $StartInfo.UseShellExecute = $false
    $StartInfo.CreateNoWindow = $true
    $StartInfo.RedirectStandardOutput = $true
    $StartInfo.RedirectStandardError = $true

    $Process = New-Object System.Diagnostics.Process
    $Process.StartInfo = $StartInfo
    $OutputSubscription = $null
    $ErrorSubscription = $null
    try {
        $OutputSubscription = Register-ObjectEvent -InputObject $Process -EventName OutputDataReceived -Action {
            if ($null -ne $EventArgs.Data) {
                [Console]::Out.WriteLine($EventArgs.Data)
                [Console]::Out.Flush()
            }
        }
        $ErrorSubscription = Register-ObjectEvent -InputObject $Process -EventName ErrorDataReceived -Action {
            if ($null -ne $EventArgs.Data) {
                [Console]::Error.WriteLine($EventArgs.Data)
                [Console]::Error.Flush()
            }
        }

        if (-not $Process.Start()) {
            throw "Native process '$FilePath' failed to start."
        }

        $Process.BeginOutputReadLine()
        $Process.BeginErrorReadLine()
        $Process.WaitForExit()
        $Process.WaitForExit()
        return $Process.ExitCode
    } finally {
        if ($null -ne $OutputSubscription) {
            Unregister-Event -SubscriptionId $OutputSubscription.Id -ErrorAction SilentlyContinue
        }
        if ($null -ne $ErrorSubscription) {
            Unregister-Event -SubscriptionId $ErrorSubscription.Id -ErrorAction SilentlyContinue
        }
        $Process.Dispose()
    }
}

if ([string]::IsNullOrWhiteSpace($Project)) { [Console]::Error.WriteLine("Project is required."); exit 2 }
if ([string]::IsNullOrWhiteSpace($Platform)) { [Console]::Error.WriteLine("Platform is required."); exit 2 }
if ([string]::IsNullOrWhiteSpace($Output)) { [Console]::Error.WriteLine("Output is required."); exit 2 }
if ([string]::IsNullOrWhiteSpace($Configuration)) { [Console]::Error.WriteLine("Configuration is required."); exit 2 }

try {
    if ([string]::IsNullOrWhiteSpace($EditorProject)) {
        $EditorProject = Join-Path $PSScriptRoot "..\\helengine.ui\\helengine.editor.app\\helengine.editor.app.csproj"
    }

    $ResolvedEditorProject = [System.IO.Path]::GetFullPath($EditorProject)
    if (-not (Test-Path -LiteralPath $ResolvedEditorProject -PathType Leaf)) {
        [Console]::Error.WriteLine("Editor project was not found at '$ResolvedEditorProject'.")
        exit 3
    }

    $ResolvedProjectCandidate = [System.IO.Path]::GetFullPath($Project)
    if (Test-Path -LiteralPath $ResolvedProjectCandidate -PathType Container) {
        $ResolvedProjectCandidate = Join-Path $ResolvedProjectCandidate "project.heproj"
    }

    $ResolvedProjectPath = [System.IO.Path]::GetFullPath($ResolvedProjectCandidate)
    if (-not (Test-Path -LiteralPath $ResolvedProjectPath -PathType Leaf)) {
        [Console]::Error.WriteLine("Project file was not found at '$ResolvedProjectPath'. Pass either a project directory that contains project.heproj or an explicit .heproj path.")
        exit 4
    }

    $ResolvedProjectRootPath = Split-Path -Parent $ResolvedProjectPath
    $ProjectIsolationHash = Get-ProjectIsolationHash -ProjectRootPath $ResolvedProjectRootPath
    $PlatformIsolationSegment = Get-SafePathSegment -Value $Platform
    $ResolvedHelEngineRootPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
    $EditorIsolationRootPath = Join-Path ([System.IO.Path]::GetTempPath()) ("helengine-builds\" + $ProjectIsolationHash + "\" + $PlatformIsolationSegment + "\editor-app")
    $EditorArtifactsPath = Join-Path $EditorIsolationRootPath "artifacts"
    $EditorPublishPath = Join-Path $EditorIsolationRootPath "publish"

    $ResolvedOutputPath = [System.IO.Path]::GetFullPath($Output)
    if (-not (Test-Path -LiteralPath $ResolvedOutputPath -PathType Container)) {
        $null = New-Item -ItemType Directory -Path $ResolvedOutputPath -Force
    }

    $DotNetSharedPropertyArguments = @(
        "--artifacts-path",
        $EditorArtifactsPath
    )

    $DotNetRestoreArguments = @(
        "restore",
        $ResolvedEditorProject
    ) + $DotNetSharedPropertyArguments

    $DotNetPublishArguments = @(
        "publish",
        $ResolvedEditorProject,
        "--no-restore",
        "-c",
        $Configuration,
        "-o",
        $EditorPublishPath
    ) + $DotNetSharedPropertyArguments

    $EditorRunArguments = @(
        "--project",
        $ResolvedProjectPath,
        "--build",
        $Platform
    )

    if ($Configuration -ieq "Debug" -or $Configuration -ieq "Release") {
        $EditorRunArguments += @(
            "--build-profile",
            $Configuration.ToLowerInvariant()
        )
    }

    $EditorRunArguments += @(
        "--output",
        $ResolvedOutputPath
    )

    if ($AdditionalArgs.Count -gt 0) {
        $EditorRunArguments += $AdditionalArgs
    }

    $RestoreDisplayArguments = @("dotnet")
    foreach ($Argument in $DotNetRestoreArguments) {
        if ($Argument -match '[\s"]') {
            $RestoreDisplayArguments += '"' + $Argument.Replace('"', '\"') + '"'
        } else {
            $RestoreDisplayArguments += $Argument
        }
    }

    Write-Host ("Restoring: " + ($RestoreDisplayArguments -join " "))

    $DotNetRestoreExitCode = Invoke-StreamingNativeProcess -FilePath "dotnet" -ArgumentList $DotNetRestoreArguments
    if ($DotNetRestoreExitCode -ne 0) {
        [Console]::Error.WriteLine("Editor project restore failed with exit code $DotNetRestoreExitCode.")
        exit $DotNetRestoreExitCode
    }

    $BuildDisplayArguments = @("dotnet")
    foreach ($Argument in $DotNetPublishArguments) {
        if ($Argument -match '[\s"]') {
            $BuildDisplayArguments += '"' + $Argument.Replace('"', '\"') + '"'
        } else {
            $BuildDisplayArguments += $Argument
        }
    }

    Write-Host ("Publishing: " + ($BuildDisplayArguments -join " "))

    $DotNetBuildExitCode = Invoke-StreamingNativeProcess -FilePath "dotnet" -ArgumentList $DotNetPublishArguments
    if ($DotNetBuildExitCode -ne 0) {
        [Console]::Error.WriteLine("Editor project publish failed with exit code $DotNetBuildExitCode.")
        exit $DotNetBuildExitCode
    }

    $EditorAssemblyPath = Join-Path $EditorPublishPath "helengine.editor.app.dll"
    if (-not (Test-Path -LiteralPath $EditorAssemblyPath -PathType Leaf)) {
        [Console]::Error.WriteLine("Editor app assembly was not found at '$EditorAssemblyPath'.")
        exit 5
    }

    $DisplayArguments = @("dotnet", $EditorAssemblyPath)
    foreach ($Argument in $EditorRunArguments) {
        if ($Argument -match '[\s"]') {
            $DisplayArguments += '"' + $Argument.Replace('"', '\"') + '"'
        } else {
            $DisplayArguments += $Argument
        }
    }

    Write-Host ("Executing: " + ($DisplayArguments -join " "))

    $OriginalHelEngineSourceRootPath = $env:HELENGINE_SOURCE_ROOT
    try {
        $env:HELENGINE_SOURCE_ROOT = $ResolvedHelEngineRootPath

        if ($Platform -ieq "ps2" -or $Platform -ieq "windows") {
            $SceneGenerationArguments = @(
                $EditorAssemblyPath,
                "--project",
                $ResolvedProjectPath,
                "--editor-command",
                "menu.generate-game-scenes"
            )
            Write-Host ("Regenerating generated game scenes: dotnet " + ($SceneGenerationArguments -join " "))
            $SceneGenerationExitCode = Invoke-StreamingNativeProcess -FilePath "dotnet" -ArgumentList $SceneGenerationArguments
            if ($SceneGenerationExitCode -ne 0) {
                [Console]::Error.WriteLine("Generated game scene refresh failed with exit code $SceneGenerationExitCode.")
                exit $SceneGenerationExitCode
            }

            $PresentationAttachmentArguments = @(
                $EditorAssemblyPath,
                "--project",
                $ResolvedProjectPath,
                "--editor-command",
                "menu.attach-tilt-trial-presentation-blueprints"
            )
            Write-Host ("Refreshing Tilt Trial presentation bindings: dotnet " + ($PresentationAttachmentArguments -join " "))
            $PresentationAttachmentExitCode = Invoke-StreamingNativeProcess -FilePath "dotnet" -ArgumentList $PresentationAttachmentArguments
            if ($PresentationAttachmentExitCode -ne 0) {
                [Console]::Error.WriteLine("Tilt Trial presentation binding refresh failed with exit code $PresentationAttachmentExitCode.")
                exit $PresentationAttachmentExitCode
            }
        }

        $DotNetExitCode = Invoke-StreamingNativeProcess -FilePath "dotnet" -ArgumentList (@($EditorAssemblyPath) + $EditorRunArguments)
        if ($DotNetExitCode -ne 0) {
            [Console]::Error.WriteLine("Editor platform build failed with exit code $DotNetExitCode.")
            exit $DotNetExitCode
        }
    } finally {
        $env:HELENGINE_SOURCE_ROOT = $OriginalHelEngineSourceRootPath
    }

    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 10
}

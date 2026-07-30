[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CodegenPath,

    [Parameter()]
    [string]$OutputPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-LogTail {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LogPath
    )

    if (-not (Test-Path -LiteralPath $LogPath -PathType Leaf)) {
        return
    }

    Write-Host "Last lines from '$LogPath':"
    Get-Content -LiteralPath $LogPath -Tail 40 | ForEach-Object { Write-Host $_ }
}

$ScriptRootPath = Split-Path -Parent $PSCommandPath
$RepositoryRootPath = [System.IO.Path]::GetFullPath((Join-Path $ScriptRootPath ".."))
$ValidationRootPath = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRootPath ".validation"))

$ResolvedCodegenPath = if ([System.IO.Path]::IsPathRooted($CodegenPath)) {
    [System.IO.Path]::GetFullPath($CodegenPath)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $RepositoryRootPath $CodegenPath))
}
if (-not (Test-Path -LiteralPath $ResolvedCodegenPath -PathType Leaf)) {
    throw "Code generator executable was not found at '$ResolvedCodegenPath'."
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $GeneratedPath = Join-Path $ValidationRootPath ("helphysics-generated-cpp-" + [guid]::NewGuid().ToString("N"))
} else {
    if (($OutputPath -split '[\\/]') -contains "..") {
        throw "OutputPath must not contain parent-directory traversal segments."
    }

    $GeneratedPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
        [System.IO.Path]::GetFullPath($OutputPath)
    } else {
        [System.IO.Path]::GetFullPath((Join-Path $RepositoryRootPath $OutputPath))
    }
}

$ValidationRootPrefix = $ValidationRootPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $GeneratedPath.StartsWith($ValidationRootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputPath must be a descendant of repository validation root '$ValidationRootPath'."
}
if (Test-Path -LiteralPath $GeneratedPath) {
    throw "OutputPath must be unique and must not already exist: '$GeneratedPath'."
}

$ContainmentProbePath = Split-Path -Parent $GeneratedPath
while ($true) {
    if (Test-Path -LiteralPath $ContainmentProbePath) {
        $ContainmentProbeItem = Get-Item -LiteralPath $ContainmentProbePath -Force
        if (($ContainmentProbeItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "OutputPath must not pass through reparse-point directory '$ContainmentProbePath'."
        }
    }

    if ($ContainmentProbePath.Equals($ValidationRootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        break
    }

    $ParentContainmentProbePath = Split-Path -Parent $ContainmentProbePath
    if ([string]::IsNullOrWhiteSpace($ParentContainmentProbePath) -or
        $ParentContainmentProbePath.Equals($ContainmentProbePath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "OutputPath containment could not be verified beneath '$ValidationRootPath'."
    }

    $ContainmentProbePath = $ParentContainmentProbePath
}

$null = [System.IO.Directory]::CreateDirectory($GeneratedPath)

$CodegenLogPath = Join-Path $GeneratedPath "codegen.log"
Push-Location $RepositoryRootPath
try {
    $PreviousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $CodegenOutput = @(& $ResolvedCodegenPath --cpp --project engine\helengine.helphysics\helengine.helphysics.csproj --output $GeneratedPath --feature-catalog engine\helengine.editor\codegen\features\helengine-feature-catalog.json --platform windows --language cpp --endianness little --set include-project-defined-preprocessor-symbols=false --set additional-preprocessor-symbols=HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION --set write-conversion-report=true 2>&1)
        $CodegenExitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $PreviousErrorActionPreference
    }
} finally {
    Pop-Location
}
$CodegenOutput | Out-File -LiteralPath $CodegenLogPath -Encoding utf8
if ($CodegenExitCode -ne 0) {
    Write-LogTail -LogPath $CodegenLogPath
    throw "C++ generation failed with exit code $CodegenExitCode. Full output is preserved at '$CodegenLogPath'."
}

$ConversionReportPath = Join-Path $GeneratedPath "cpp-conversion-report.json"
if (-not (Test-Path -LiteralPath $ConversionReportPath -PathType Leaf)) {
    throw "Generated conversion report was not found at '$ConversionReportPath'."
}
$ConversionReport = Get-Content -LiteralPath $ConversionReportPath -Raw | ConvertFrom-Json
if (-not ($ConversionReport.PSObject.Properties.Name -contains "hasErrors") -or
    -not ($ConversionReport.PSObject.Properties.Name -contains "errorCount") -or
    -not ($ConversionReport.PSObject.Properties.Name -contains "diagnostics")) {
    throw "Generated conversion report does not satisfy the required error-reporting contract at '$ConversionReportPath'."
}
if ($ConversionReport.hasErrors -or [int]$ConversionReport.errorCount -gt 0) {
    Write-Host "C++ generation reported $($ConversionReport.errorCount) conversion error(s):"
    @($ConversionReport.diagnostics |
        Where-Object { $_.severity -eq "Error" } |
        Select-Object -First 20) |
        ForEach-Object {
            Write-Host "$($_.code): $($_.sourceTypeName).$($_.sourceMemberName): $($_.message)"
        }
    throw "C++ generation reported conversion errors. Full diagnostics are preserved at '$ConversionReportPath'."
}
$AuditIssues = @()
$ReportAuditPattern = '(?i:\b(?:unresolved|unsupported)\s+(?:symbol|type|member|method|dependency|reference)\b|\b(?:symbol|type|member|method|dependency|reference)\s+(?:is\s+)?(?:unresolved|unsupported)\b)|System(?:\.|::)Numerics|\bVector(?:\s*<|\\u003C)'
$GeneratedAuditPattern = '(?i:\b(?:unresolved|unsupported)[_ ](?:symbol|dependency|reference)\b|\b(?:symbol|dependency|reference)\s+(?:is\s+)?(?:unresolved|unsupported)\b|__(?:unresolved|unsupported))|System(?:\.|::)Numerics|\bVector\s*<'
$ReportMatches = Select-String -LiteralPath $ConversionReportPath -Pattern $ReportAuditPattern -CaseSensitive
foreach ($ReportMatch in $ReportMatches) {
    $AuditIssues += "$($ReportMatch.Path):$($ReportMatch.LineNumber): $($ReportMatch.Line.Trim())"
}
$AuditedFiles = Get-ChildItem -LiteralPath $GeneratedPath -Recurse -File |
    Where-Object { $_.Extension -in @(".c", ".cc", ".cpp", ".h", ".hh", ".hpp", ".inc") } |
    Select-Object -ExpandProperty FullName
foreach ($AuditedFile in $AuditedFiles) {
    $Matches = Select-String -LiteralPath $AuditedFile -Pattern $GeneratedAuditPattern -CaseSensitive
    foreach ($Match in $Matches) {
        $AuditIssues += "$($Match.Path):$($Match.LineNumber): $($Match.Line.Trim())"
    }
}
$AuditIssues = @($AuditIssues | Sort-Object -Unique)
if ($AuditIssues.Count -gt 0) {
    Write-Host "Generated C++ audit found unresolved or unsupported output:"
    $AuditIssues | ForEach-Object { Write-Host $_ }
    throw "Generated C++ audit failed with $($AuditIssues.Count) matched line(s)."
}

$BuildScripts = @(Get-ChildItem -LiteralPath $GeneratedPath -Recurse -File -Filter "build_msvc.bat")
if ($BuildScripts.Count -eq 0) {
    throw "Generated build_msvc.bat was not found beneath '$GeneratedPath'."
}
if ($BuildScripts.Count -gt 1) {
    $BuildScriptPaths = ($BuildScripts | Select-Object -ExpandProperty FullName) -join "', '"
    throw "Expected one generated build_msvc.bat beneath '$GeneratedPath', but found '$BuildScriptPaths'."
}

$VsDevCmdPath = "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat"
if (-not (Test-Path -LiteralPath $VsDevCmdPath -PathType Leaf)) {
    throw "Visual Studio developer command script was not found at '$VsDevCmdPath'."
}
$BuildScriptPath = $BuildScripts[0].FullName
$BuildLogPath = Join-Path $GeneratedPath "msvc-build.log"
$BuildCommand = 'call "{0}" -arch=amd64 -host_arch=amd64 && call "{1}"' -f $VsDevCmdPath, $BuildScriptPath
Push-Location $GeneratedPath
try {
    $PreviousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & $env:ComSpec /d /c $BuildCommand *> $BuildLogPath
        $BuildExitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $PreviousErrorActionPreference
    }
} finally {
    Pop-Location
}
if ($BuildExitCode -ne 0) {
    Write-LogTail -LogPath $BuildLogPath
    throw "Generated MSVC build failed with exit code $BuildExitCode. Full output is preserved at '$BuildLogPath'."
}

$GeneratedObjectPath = Join-Path $GeneratedPath "build\msvc\generated_unity.obj"
if (-not (Test-Path -LiteralPath $GeneratedObjectPath -PathType Leaf)) {
    throw "Generated MSVC build completed without expected object '$GeneratedObjectPath'."
}

Write-Host "HelPhysics generated C++ validation succeeded."
Write-Host "Output: $GeneratedPath"
Write-Host "Report: $ConversionReportPath"
Write-Host "Object: $GeneratedObjectPath"

<#
.SYNOPSIS
Clones selected platform repositories beside the main helengine checkout.
.DESCRIPTION
Uses the checked-in repository catalog, offers interactive selection, and preserves
existing checkouts. Initializes missing submodules without resetting initialized ones.
.EXAMPLE
.\scripts\setup-workspace.ps1 -All
.EXAMPLE
.\scripts\setup-workspace.ps1 -Platforms ps1,n64
#>
[CmdletBinding(DefaultParameterSetName = 'Select', SupportsShouldProcess = $true)]
param(
    [Parameter(ParameterSetName = 'All', Mandatory = $true)]
    [switch]$All,
    [Parameter(ParameterSetName = 'Select')]
    [Alias('Repositories')]
    [string[]]$Platforms = @(),
    [Parameter(ParameterSetName = 'List', Mandatory = $true)]
    [switch]$List,
    [ValidateSet('Auto', 'Ssh', 'Https')]
    [string]$Protocol = 'Auto',
    [switch]$SkipSubmodules,
    [string]$EngineRoot = '',
    [string]$CatalogPath = ''
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:WorkspaceGit = $null

<# .SYNOPSIS Runs Git with an argument array and reports its actual exit status. #>
function Invoke-WorkspaceGit {
    param([string[]]$Arguments)
    $Previous = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $Output = @(& $script:WorkspaceGit @Arguments 2>&1)
        $Code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $Previous }
    if ($Code -ne 0) { throw "Git failed (exit $Code): $($Output -join [Environment]::NewLine)" }
    return ($Output -join [Environment]::NewLine).Trim()
}

<# .SYNOPSIS Recognizes an independent checkout rather than its enclosing repository. #>
function Test-WorkspaceCheckout {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { return $false }
    try { $TopLevel = Invoke-WorkspaceGit @('-C', $Path, 'rev-parse', '--show-toplevel') }
    catch { return $false }
    return [IO.Path]::GetFullPath($TopLevel).TrimEnd('\', '/') -eq [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
}

<# .SYNOPSIS Compares GitHub SSH/HTTPS origins equivalently while retaining local fixture paths. #>
function Get-WorkspaceRemoteIdentity {
    param([string]$Remote)
    if ($Remote -match '^git@github\.com:(.+)$') {
        return 'github.com/' + ($Matches[1].TrimEnd('/') -replace '\.git$', '').ToLowerInvariant()
    }
    $Uri = $null
    if ([Uri]::TryCreate($Remote, [UriKind]::Absolute, [ref]$Uri) -and $Uri.Host -eq 'github.com') {
        return 'github.com/' + ($Uri.AbsolutePath.Trim('/') -replace '\.git$', '').ToLowerInvariant()
    }
    if ([IO.Path]::IsPathRooted($Remote)) { return [IO.Path]::GetFullPath($Remote).TrimEnd('\', '/') }
    return $Remote.TrimEnd('/')
}

<# .SYNOPSIS Selects the requested GitHub transport without rewriting non-GitHub remotes. #>
function Get-WorkspaceCloneUrl {
    param([string]$Url, [string]$Transport)
    $Identity = Get-WorkspaceRemoteIdentity $Url
    if (-not $Identity.StartsWith('github.com/')) { return $Url }
    $Repository = $Identity.Substring('github.com/'.Length)
    if ($Transport -eq 'Https') { return "https://github.com/$Repository.git" }
    return "git@github.com:$Repository.git"
}

<# .SYNOPSIS Initializes only missing submodule checkouts, including interrupted recursive clones. #>
function Initialize-WorkspaceSubmodules {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param([string]$RepositoryRoot, [ValidateSet('Ssh', 'Https')][string]$Transport)
    $ModulesFile = Join-Path $RepositoryRoot '.gitmodules'
    if (-not (Test-Path -LiteralPath $ModulesFile -PathType Leaf)) { return }
    $Configuration = Invoke-WorkspaceGit @('-C', $RepositoryRoot, 'config', '--file', $ModulesFile, '--list')
    # Git must be able to map every tracked gitlink before any checkout changes.
    $null = Invoke-WorkspaceGit @('-C', $RepositoryRoot, 'submodule', 'status')
    foreach ($Line in ($Configuration -split '\r?\n')) {
        if ($Line -notmatch '^submodule\..+\.path=(.+)$') { continue }
        $RelativePath = $Matches[1]
        $Child = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot $RelativePath))
        $Prefix = $RepositoryRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
        if (-not $Child.StartsWith($Prefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Submodule path escapes its repository: $RelativePath"
        }
        $Tracked = Invoke-WorkspaceGit @('--literal-pathspecs', '-C', $RepositoryRoot, 'ls-files', '--stage', '--', $RelativePath)
        $GitlinkPattern = '(?m)^160000 [0-9a-f]+ 0\t' + [regex]::Escape($RelativePath) + '\r?$'
        if ($Tracked -notmatch $GitlinkPattern) {
            Write-Warning "Ignoring stale submodule declaration (not a tracked gitlink): $Child"
            continue
        }
        if (-not (Test-WorkspaceCheckout $Child)) {
            if (-not $PSCmdlet.ShouldProcess($Child, 'Initialize missing pinned submodule')) { continue }
            Write-Host "Initializing submodule: $Child"
            # Process-local URL rewrites also cover SSH URLs pinned by .gitmodules when the user chose HTTPS.
            if ($Transport -eq 'Https') { $GitArguments = @('-c', 'url.https://github.com/.insteadOf=git@github.com:') }
            else { $GitArguments = @('-c', 'url.git@github.com:.insteadOf=https://github.com/') }
            $GitArguments += @('-C', $RepositoryRoot, 'submodule', 'update', '--init', '--', $RelativePath)
            $Output = Invoke-WorkspaceGit $GitArguments
            if ($Output) { Write-Host $Output }
        }
        Initialize-WorkspaceSubmodules -RepositoryRoot $Child -Transport $Transport
    }
}

if ([string]::IsNullOrWhiteSpace($EngineRoot)) { $EngineRoot = Join-Path $PSScriptRoot '..' }
if ([string]::IsNullOrWhiteSpace($CatalogPath)) { $CatalogPath = Join-Path $PSScriptRoot 'workspace-repositories.json' }
$EngineRoot = [IO.Path]::GetFullPath($EngineRoot).TrimEnd('\', '/')
$WorkspaceRoot = Split-Path -Parent $EngineRoot
$Catalog = Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json
if ($Catalog.schemaVersion -ne 1) { throw 'Unsupported workspace repository catalog schema.' }
$Repositories = @($Catalog.repositories)
$Ids = @{}
$Directories = @{}
foreach ($Repository in $Repositories) {
    if ([string]::IsNullOrWhiteSpace($Repository.id) -or [string]::IsNullOrWhiteSpace($Repository.name) -or
        [string]::IsNullOrWhiteSpace($Repository.url) -or [string]::IsNullOrWhiteSpace($Repository.directory)) {
        throw 'Repository catalog entries require id, name, directory and url.'
    }
    $Directory = [string]$Repository.directory
    if ($Directory -in @('.', '..') -or $Directory.IndexOfAny([char[]]'/\:') -ge 0 -or
        $Ids.ContainsKey($Repository.id) -or $Directories.ContainsKey($Directory)) {
        throw "Unsafe or duplicate repository catalog entry: $($Repository.id)"
    }
    $Ids[$Repository.id] = $Repository
    $Directories[$Directory] = $true
}
if ($List) {
    $Repositories | Select-Object id, name, directory
    return
}
if ($All) { $Selection = @($Repositories.id) }
else {
    $Selection = @($Platforms | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
    if ($Selection.Count -eq 0) {
        Write-Host 'Select repositories to clone beside the main helengine checkout:'
        Write-Host '  0. All repositories'
        for ($Index = 0; $Index -lt $Repositories.Count; ++$Index) {
            Write-Host ('  {0}. {1} [{2}]' -f ($Index + 1), $Repositories[$Index].name, $Repositories[$Index].id)
        }
        $Answer = Read-Host 'Enter IDs or numbers separated by commas (Enter cancels)'
        if ([string]::IsNullOrWhiteSpace($Answer)) { Write-Host 'Cancelled.'; return }
        if ($Answer.Trim() -in @('0', 'all')) { $Selection = @($Repositories.id) }
        else {
            $Selection = @($Answer -split ',' | ForEach-Object {
                $Value = $_.Trim().ToLowerInvariant()
                $Number = 0
                if ([int]::TryParse($Value, [ref]$Number) -and $Number -gt 0 -and $Number -le $Repositories.Count) {
                    $Repositories[$Number - 1].id
                } else { $Value }
            })
        }
    }
}
$Unknown = @($Selection | Where-Object { -not $Ids.ContainsKey($_) })
if ($Unknown.Count -gt 0) { throw "Unknown repository/platform IDs: $($Unknown -join ', '). Use -List." }
$Selected = @($Repositories | Where-Object { $_.id -in $Selection })
$script:WorkspaceGit = (Get-Command git -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
if (-not (Test-WorkspaceCheckout $EngineRoot)) { throw "EngineRoot must be the main Git repository checkout: $EngineRoot" }
$MainOrigin = Invoke-WorkspaceGit @('-C', $EngineRoot, 'remote', 'get-url', 'origin')
if ($Protocol -eq 'Auto') {
    if ($MainOrigin -match '^https://') { $Protocol = 'Https' } else { $Protocol = 'Ssh' }
}

# Validate the complete selection before making any checkout or submodule changes.
foreach ($Repository in $Selected) {
    $Target = Join-Path $WorkspaceRoot $Repository.directory
    if ($Target -eq $EngineRoot) { throw "Catalog target overlaps the main repository: $Target" }
    if (-not (Test-Path -LiteralPath $Target)) { continue }
    if (-not (Test-WorkspaceCheckout $Target)) { throw "Existing path is not an independent Git repository: $Target" }
    $ActualOrigin = Invoke-WorkspaceGit @('-C', $Target, 'remote', 'get-url', 'origin')
    if ((Get-WorkspaceRemoteIdentity $ActualOrigin) -ne (Get-WorkspaceRemoteIdentity $Repository.url)) {
        throw "Existing repository origin does not match the catalog: $Target"
    }
}
if (-not $SkipSubmodules) { Initialize-WorkspaceSubmodules -RepositoryRoot $EngineRoot -Transport $Protocol }
foreach ($Repository in $Selected) {
    $Target = Join-Path $WorkspaceRoot $Repository.directory
    $Status = 'Existing'
    if (-not (Test-Path -LiteralPath $Target)) {
        if ($PSCmdlet.ShouldProcess($Target, "Clone $($Repository.name)")) {
            Write-Host "Cloning $($Repository.name): $Target"
            $Url = Get-WorkspaceCloneUrl $Repository.url $Protocol
            $Output = Invoke-WorkspaceGit @('clone', '--origin', 'origin', '--', $Url, $Target)
            if ($Output) { Write-Host $Output }
            $Status = 'Cloned'
        } else { $Status = 'Planned' }
    } else { Write-Host "Preserving existing checkout: $Target" }
    if ($Status -ne 'Planned' -and -not $SkipSubmodules) { Initialize-WorkspaceSubmodules -RepositoryRoot $Target -Transport $Protocol }
    [PSCustomObject]@{ Repository = $Repository.id; Status = $Status; Path = $Target }
}

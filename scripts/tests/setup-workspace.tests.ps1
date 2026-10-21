[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Script = Join-Path $PSScriptRoot '..\setup-workspace.ps1'
$IsWindowsHost = [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT
$Root = if ($IsWindowsHost) { 'C:\temp\helengine-bootstrap-tests' } else { '/tmp/helengine-bootstrap-tests' }
$ExpectedRoot = [IO.Path]::GetFullPath($Root)
if (Test-Path -LiteralPath $Root) { throw "Test scratch already exists: $Root" }
$Git = (Get-Command git -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$OriginalProtocols = $env:GIT_ALLOW_PROTOCOL
$OriginalTrace = $env:GIT_TRACE2_EVENT
$Passed = 0

function Invoke-TestGit {
    param([string[]]$Arguments)
    $Previous = $ErrorActionPreference
    try { $ErrorActionPreference = 'Continue'; $Output = @(& $Git @Arguments 2>&1); $Code = $LASTEXITCODE }
    finally { $ErrorActionPreference = $Previous }
    if ($Code -ne 0) { throw "Fixture git failed: $($Output -join [Environment]::NewLine)" }
    return ($Output -join [Environment]::NewLine)
}
function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}
function Assert-Fails {
    param([scriptblock]$Action, [string]$MessagePattern)
    $Failure = $null
    try { & $Action | Out-Null } catch { $Failure = $_.Exception.Message }
    Assert-True ($null -ne $Failure -and $Failure -match $MessagePattern) "Expected failure '$MessagePattern'; got '$Failure'"
}

try {
    $null = New-Item -ItemType Directory -Path $Root
    $env:GIT_ALLOW_PROTOCOL = 'file'
    $Source = Join-Path $Root 'source'
    $Bare = Join-Path $Root 'remote.git'
    $null = Invoke-TestGit @('init', '--initial-branch=main', $Source)
    $null = Invoke-TestGit @('-C', $Source, 'config', 'user.name', 'Bootstrap test')
    $null = Invoke-TestGit @('-C', $Source, 'config', 'user.email', 'bootstrap@example.invalid')
    Set-Content -LiteralPath (Join-Path $Source 'payload.txt') -Value 'fixture' -Encoding UTF8
    $null = Invoke-TestGit @('-C', $Source, 'add', 'payload.txt')
    $null = Invoke-TestGit @('-C', $Source, 'commit', '-m', 'fixture')
    $null = Invoke-TestGit @('clone', '--bare', $Source, $Bare)
    $Workspace = Join-Path $Root 'workspace with spaces'
    $null = New-Item -ItemType Directory -Path $Workspace
    $Engine = Join-Path $Workspace 'helengine'
    $null = Invoke-TestGit @('clone', $Bare, $Engine)
    $Catalog = Join-Path $Root 'catalog.json'
    @{schemaVersion=1; repositories=@(
        @{id='ps1';name='PS1 fixture';directory='helengine-ps1';url=$Bare},
        @{id='n64';name='N64 fixture';directory='helengine-n64';url=$Bare}
    )} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Catalog -Encoding UTF8
    $Common = @{EngineRoot=$Engine;CatalogPath=$Catalog;SkipSubmodules=$true}

    # Exercise the actual entry point with default paths, not only injected fixture roots.
    if ($PSVersionTable.PSEdition -eq 'Desktop') { $Shell = Join-Path $PSHOME 'powershell.exe' }
    elseif ($IsWindowsHost) { $Shell = Join-Path $PSHOME 'pwsh.exe' }
    else { $Shell = Join-Path $PSHOME 'pwsh' }
    $Previous = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $DefaultOutput = @(& $Shell -NoProfile -ExecutionPolicy Bypass -File $Script -List 2>&1)
        $DefaultExit = $LASTEXITCODE
    } finally { $ErrorActionPreference = $Previous }
    Assert-True ($DefaultExit -eq 0) "Default CLI paths failed: $($DefaultOutput -join [Environment]::NewLine)"
    Assert-True (($DefaultOutput -join ' ') -match 'windows') 'Default CLI did not read the product repository catalog.'
    ++$Passed
    & $Script @Common -List | Out-Null
    Assert-True (-not (Test-Path (Join-Path $Workspace 'helengine-ps1'))) 'Listing unexpectedly cloned a repository.'
    ++$Passed
    & $Script @Common -Platforms 'ps1' -WhatIf | Out-Null
    Assert-True (-not (Test-Path (Join-Path $Workspace 'helengine-ps1'))) 'WhatIf unexpectedly cloned a repository.'
    ++$Passed
    # Unix PATH commonly resolves /usr/bin/git and /bin/git; invoke only the first executable.
    function Get-Command {
        param([string]$Name, [string]$CommandType, [string]$ErrorAction)
        if ($Name -eq 'git') { return @([PSCustomObject]@{ Source = $Git }, [PSCustomObject]@{ Source = $Git }) }
        return Microsoft.PowerShell.Core\Get-Command @PSBoundParameters
    }
    try { & $Script @Common -Platforms 'ps1' -WhatIf | Out-Null }
    finally { Remove-Item Function:\Get-Command }
    Assert-True (-not (Test-Path (Join-Path $Workspace 'helengine-ps1'))) 'Duplicate Git lookup unexpectedly cloned a repository.'
    ++$Passed
    & $Script @Common -Platforms 'ps1' | Out-Null
    $Ps1 = Join-Path $Workspace 'helengine-ps1'
    Assert-True (Test-Path (Join-Path $Ps1 'payload.txt')) 'Selected repository payload is missing.'
    Assert-True (-not (Test-Path (Join-Path $Workspace 'helengine-n64'))) 'Unselected repository was cloned.'
    ++$Passed
    Set-Content -LiteralPath (Join-Path $Ps1 'payload.txt') -Value 'local edit' -Encoding UTF8
    $Before = (Get-FileHash (Join-Path $Ps1 'payload.txt')).Hash
    & $Script @Common -Platforms 'ps1' | Out-Null
    Assert-True ((Get-FileHash (Join-Path $Ps1 'payload.txt')).Hash -eq $Before) 'Rerun changed existing local edits.'
    ++$Passed
    function Read-Host { param([string]$Prompt) return 'ps1' }
    try { & $Script @Common | Out-Null } finally { Remove-Item Function:\Read-Host }
    Assert-True (-not (Test-Path (Join-Path $Workspace 'helengine-n64'))) 'Interactive selection cloned an unselected repository.'
    ++$Passed
    Assert-Fails { & $Script @Common -Platforms 'ps1,unknown' } 'Unknown'
    ++$Passed
    $null = New-Item -ItemType Directory -Path (Join-Path $Workspace 'helengine-n64')
    Set-Content -LiteralPath (Join-Path $Workspace 'helengine-n64\keep.txt') -Value 'keep'
    Assert-Fails { & $Script @Common -All } 'repository'
    Assert-True (Test-Path (Join-Path $Workspace 'helengine-n64\keep.txt')) 'Existing directory was modified.'
    ++$Passed
    Remove-Item -LiteralPath (Join-Path $Workspace 'helengine-n64') -Recurse -Force
    & $Script @Common -All | Out-Null
    Assert-True (Test-Path (Join-Path $Workspace 'helengine-n64\payload.txt')) 'All did not clone the remaining repository.'
    ++$Passed
    $null = Invoke-TestGit @('-C', $Ps1, 'remote', 'set-url', 'origin', 'https://example.invalid/wrong.git')
    Assert-Fails { & $Script @Common -Platforms 'ps1' } 'origin'
    ++$Passed
    $null = Invoke-TestGit @('-C', $Ps1, 'remote', 'set-url', 'origin', $Bare)

    # Initialized submodules can have local work; only missing checkouts may be initialized.
    $null = Invoke-TestGit @('-C', $Engine, 'submodule', 'add', $Bare, 'vendor/tool')
    $Module = Join-Path $Engine 'vendor\tool'
    Set-Content -LiteralPath (Join-Path $Module 'payload.txt') -Value 'submodule local edit' -Encoding UTF8
    & $Script -EngineRoot $Engine -CatalogPath $Catalog -Platforms ps1 | Out-Null
    Assert-True ((Get-Content (Join-Path $Module 'payload.txt') -Raw) -match 'submodule local edit') 'Initialized submodule edits were reset.'
    ++$Passed
    $null = Invoke-TestGit @('-C', $Engine, 'submodule', 'deinit', '--force', '--', 'vendor/tool')
    & $Script -EngineRoot $Engine -CatalogPath $Catalog -Platforms ps1 -WhatIf | Out-Null
    Assert-True (-not (Test-Path (Join-Path $Module 'payload.txt'))) 'WhatIf initialized a missing submodule.'
    ++$Passed
    $Trace = Join-Path $Root 'git-trace.jsonl'
    $env:GIT_TRACE2_EVENT = $Trace.Replace('\', '/')
    try { & $Script -EngineRoot $Engine -CatalogPath $Catalog -Platforms ps1 -Protocol Https | Out-Null }
    finally { $env:GIT_TRACE2_EVENT = $OriginalTrace }
    Assert-True ((Get-Content -LiteralPath $Trace -Raw) -match [regex]::Escape('url.https://github.com/.insteadOf=git@github.com:')) 'HTTPS selection was not propagated to the real submodule Git process.'
    ++$Passed
    Assert-True (Test-Path (Join-Path $Module 'payload.txt')) 'Missing submodule checkout was not initialized.'
    ++$Passed
    # Vendor commits can retain .gitmodules declarations after removing the actual gitlink.
    $null = Invoke-TestGit @('-C', $Engine, 'config', '--file', '.gitmodules', 'submodule.obsolete.path', 'vendor/obsolete')
    $null = Invoke-TestGit @('-C', $Engine, 'config', '--file', '.gitmodules', 'submodule.obsolete.url', $Bare)
    $StaleWarnings = @()
    & $Script -EngineRoot $Engine -CatalogPath $Catalog -Platforms ps1 -WarningVariable StaleWarnings | Out-Null
    Assert-True (($StaleWarnings -join ' ') -match 'not a tracked gitlink') 'Stale submodule declaration was not reported.'
    Assert-True (-not (Test-Path (Join-Path $Engine 'vendor/obsolete'))) 'Stale submodule declaration created a checkout.'
    ++$Passed
    $Broken = Join-Path $Root 'broken-catalog.json'
    @{schemaVersion=1; repositories=@(@{id='broken';name='Missing fixture';directory='helengine-broken';url=(Join-Path $Root 'missing.git')})} |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Broken -Encoding UTF8
    Assert-Fails { & $Script -EngineRoot $Engine -CatalogPath $Broken -Platforms broken -SkipSubmodules } 'Git failed'
    ++$Passed
    Write-Host "PASS $Passed/17 bootstrap integration checks (real local Git, no network)."
} finally {
    $env:GIT_ALLOW_PROTOCOL = $OriginalProtocols
    $env:GIT_TRACE2_EVENT = $OriginalTrace
    if (Test-Path -LiteralPath $Root) {
        $Resolved = (Resolve-Path -LiteralPath $Root).Path
        if ($Resolved -ne $ExpectedRoot) { throw 'Unexpected fixture cleanup path.' }
        Remove-Item -LiteralPath $Resolved -Recurse -Force
    }
}

# Verifies byte-preserving assembly, repeat setup, and rejection of incomplete/corrupt downloads.
param([string]$TestRoot = 'C:\dev\helworks\builds\helengine\split-tests')
$ErrorActionPreference = 'Stop'
$CaseRoot = Join-Path $TestRoot ([Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $CaseRoot
$Helper = Join-Path $PSScriptRoot 'portable\Join-Toolchains.ps1'
if (-not (Test-Path $Helper)) { throw 'FAIL: split toolchain assembly is not implemented.' }
$Source = Join-Path $PSScriptRoot 'portable\README.txt'
Copy-Item $Source (Join-Path $CaseRoot 'console-sdk-images.tar.part01')
$Hash = (Get-FileHash $Source -Algorithm SHA256).Hash
@{sha256=$Hash; parts=@('console-sdk-images.tar.part01','console-sdk-images.tar.part02')} | ConvertTo-Json | Set-Content (Join-Path $CaseRoot 'console-sdk-images.json')
$Rejected = $false
try { & $Helper -ToolchainRoot $CaseRoot } catch { $Rejected = $_ -match 'Missing toolchain part' }
if (-not $Rejected) { throw 'FAIL: missing part not rejected.' }
@{sha256=$Hash; parts=@('console-sdk-images.tar.part01')} | ConvertTo-Json | Set-Content (Join-Path $CaseRoot 'console-sdk-images.json')
& $Helper -ToolchainRoot $CaseRoot
& $Helper -ToolchainRoot $CaseRoot
if ((Get-FileHash (Join-Path $CaseRoot 'console-sdk-images.tar')).Hash -ne $Hash) { throw 'FAIL: reconstructed bytes differ.' }
@{sha256=('0' * 64); parts=@('console-sdk-images.tar.part01')} | ConvertTo-Json | Set-Content (Join-Path $CaseRoot 'console-sdk-images.json')
$Rejected = $false
try { & $Helper -ToolchainRoot $CaseRoot } catch { $Rejected = $_ -match 'checksum' }
if (-not $Rejected) { throw 'FAIL: corrupt existing archive not rejected.' }
$CorruptRoot = Join-Path $CaseRoot 'corrupt'
$null = New-Item -ItemType Directory -Path $CorruptRoot
Copy-Item (Join-Path $CaseRoot 'console-sdk-images.json') $CorruptRoot
Copy-Item (Join-Path $CaseRoot 'console-sdk-images.tar.part01') $CorruptRoot
$Rejected = $false
try { & $Helper -ToolchainRoot $CorruptRoot } catch { $Rejected = $_ -match 'checksum' }
if (-not $Rejected -or (Test-Path (Join-Path $CorruptRoot 'console-sdk-images.tar'))) { throw 'FAIL: corrupt parts published an archive.' }
Write-Host 'PASS: assembly, repeat setup, missing parts, corrupt archive, corrupt parts.'

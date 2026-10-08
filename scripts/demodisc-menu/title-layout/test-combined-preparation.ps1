$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$stage=Join-Path $root 'fixtures/n64/staged-project'
$target=Join-Path $stage 'assets/codebase/menu'
New-Item -ItemType Directory -Path $target -Force | Out-Null
$names=@('MenuComponent.cs','MenuItemRuntime.cs','MenuPanelRuntime.cs')
foreach($name in $names){[IO.File]::Copy((Join-Path 'C:/dev/helprojs/demodisc/assets/codebase/menu' $name),(Join-Path $target $name),$true)}
$helper=Join-Path $target 'DemoDiscTitleLayout.cs'
if(Test-Path -LiteralPath $helper){
    $expected=Get-Content -LiteralPath (Join-Path $root 'source-patch-receipt.json') -Raw | ConvertFrom-Json
    if((Get-FileHash $helper).Hash.ToLowerInvariant() -notin (@($expected.helperSha256)+@($expected.acceptedPreviousHelperSha256))){throw 'Fixed fixture contains an unexpected helper; it was preserved.'}
}
$prepare=Join-Path $root 'prepare-staged-menu.ps1'
& $prepare -Platform n64 -StagedProjectRoot $stage -ValidateOnly
& $prepare -Platform n64 -StagedProjectRoot $stage
$sourceNames=$names+@('DemoDiscTitleLayout.cs')
$first=@{}
foreach($name in $sourceNames){$first[$name]=(Get-FileHash (Join-Path $target $name)).Hash}
& $prepare -Platform n64 -StagedProjectRoot $stage
foreach($name in $sourceNames){if((Get-FileHash (Join-Path $target $name)).Hash -ne $first[$name]){throw 'Idempotent preparation changed a final source.'}}
[IO.File]::WriteAllText((Join-Path $target 'MenuItemRuntime.cs'),'Unexpected fixture source',[Text.UTF8Encoding]::new($false))
$before=@{}
foreach($name in $sourceNames){$before[$name]=(Get-FileHash (Join-Path $target $name)).Hash}
$rejected=$false
try{& $prepare -Platform n64 -StagedProjectRoot $stage}catch{
    if(-not $_.Exception.Message.Contains('Staged MenuItemRuntime.cs differs')){throw}
    $rejected=$true
}
if(-not $rejected){throw 'Unexpected staged source was accepted.'}
foreach($name in $sourceNames){if((Get-FileHash (Join-Path $target $name)).Hash -ne $before[$name]){throw 'Rejection wrote a source before every guard passed.'}}
[IO.File]::Copy('C:/dev/helworks/builds/helengine-n64/menu-20261007/menu-availability-hidden/MenuItemRuntime.cs',(Join-Path $target 'MenuItemRuntime.cs'),$true)
[ordered]@{passed=$true;checks=@('Original source states accepted and converted directly to final sources','Final source preparation idempotent','Unexpected input rejected before any source write');fixture=$stage;files=$sourceNames;scriptSha256=(Get-FileHash $prepare).Hash.ToLowerInvariant();updatedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $root 'combined-preparation-test-receipt.json')
Write-Output 'PASS: combined source preparation original/idempotence/all-guards-before-write checks.'

param([Parameter(Mandatory=$true)][ValidateSet('dc','ps1','n64','ps3','x360','xbox','windows')][string]$Platform,[Parameter(Mandatory=$true)][string]$StagedProjectRoot,[switch]$ValidateOnly)
$ErrorActionPreference='Stop'
$stage=[IO.Path]::GetFullPath($StagedProjectRoot)
$allowed=if($Platform -eq 'windows'){[IO.Path]::GetFullPath('C:/dev/helworks/builds/demodisc/windows/')}else{[IO.Path]::GetFullPath("C:/dev/helworks/builds/helengine-$Platform/menu-20261007/")}
$fixture=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot "fixtures/$Platform/staged-project/"))
$isFixture=$stage.TrimEnd([IO.Path]::DirectorySeparatorChar) -eq $fixture.TrimEnd([IO.Path]::DirectorySeparatorChar)
if(-not $isFixture -and -not $stage.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Combined menu preparation only applies to this platform staging root or its fixed validation fixture.'}
$hideRoot='C:/dev/helworks/builds/helengine-n64/menu-20261007/menu-availability-hidden'
$hideReceiptPath=Join-Path $hideRoot 'source-patch-receipt.json'
$hideReceipt=Get-Content -LiteralPath $hideReceiptPath -Raw | ConvertFrom-Json
if($hideReceipt.policy -ne 'hide-unavailable'){throw 'The reviewed availability policy is not HIDE.'}
$titleReceiptPath=Join-Path $PSScriptRoot 'source-patch-receipt.json'
$titleReceipt=Get-Content -LiteralPath $titleReceiptPath -Raw | ConvertFrom-Json
if(@($hideReceipt.files).Count -ne 3){throw 'The availability receipt must declare exactly three reviewed menu files.'}
$requiredNames=@('MenuComponent.cs','MenuItemRuntime.cs','MenuPanelRuntime.cs')
$sourceRoot=Join-Path $stage 'assets/codebase/menu'
$changes=@()
foreach($name in $requiredNames){
    $entries=@($hideReceipt.files | Where-Object name -eq $name)
    if($entries.Count -ne 1){throw "Availability receipt does not uniquely declare $name"}
    $entry=$entries[0]
    $hideSource=Join-Path $hideRoot $name
    $hideHash=(Get-FileHash -LiteralPath $hideSource).Hash.ToLowerInvariant()
    if($hideHash -ne $entry.patchedSha256){throw "Reviewed HIDE source changed: $name"}
    $finalSource=$hideSource
    $finalHash=$hideHash
    $accepted=@($entry.originalSha256,$entry.patchedSha256)
    if($entry.PSObject.Properties.Name -contains 'previousHiddenSha256'){$accepted+=@($entry.previousHiddenSha256)}
    if($entry.PSObject.Properties.Name -contains 'acceptedPreviousSha256'){$accepted+=@($entry.acceptedPreviousSha256)}
    if($name -eq 'MenuComponent.cs'){
        if($hideHash -ne $titleReceipt.menuBaselineSha256){throw 'The title overlay does not describe the current reviewed HIDE baseline.'}
        $finalSource=Join-Path $PSScriptRoot $name
        $finalHash=(Get-FileHash -LiteralPath $finalSource).Hash.ToLowerInvariant()
        if($finalHash -ne $titleReceipt.menuPatchedSha256){throw 'Reviewed fitted menu source changed.'}
        $accepted+=@($titleReceipt.menuPatchedSha256)
        $accepted+=@($titleReceipt.acceptedPreviousMenuSha256)
        if($Platform -eq 'xbox'){$accepted+=@($titleReceipt.acceptedXboxMenuSha256)}
    }
    $destination=Join-Path $sourceRoot $name
    if(-not(Test-Path -LiteralPath $destination -PathType Leaf)){throw "Staged menu source is absent: $destination"}
    $before=(Get-FileHash -LiteralPath $destination).Hash.ToLowerInvariant()
    if($before -notin $accepted){throw "Staged $name differs from every reviewed original, HIDE or fitted state; no source was written."}
    $changes+=@{name=$name;source=$finalSource;destination=$destination;beforeSha256=$before;afterSha256=$finalHash}
}
$helperSource=Join-Path $PSScriptRoot 'DemoDiscTitleLayout.cs'
$helperHash=(Get-FileHash -LiteralPath $helperSource).Hash.ToLowerInvariant()
if($helperHash -ne $titleReceipt.helperSha256){throw 'Reviewed heading helper changed; no source was written.'}
$helperDestination=Join-Path $sourceRoot 'DemoDiscTitleLayout.cs'
$helperBefore=$null
if(Test-Path -LiteralPath $helperDestination){
    $helperBefore=(Get-FileHash -LiteralPath $helperDestination).Hash.ToLowerInvariant()
    if($helperBefore -notin (@($helperHash)+@($titleReceipt.acceptedPreviousHelperSha256))){throw 'Staged title helper is unreviewed; no source was written.'}
}
$changes+=@{name='DemoDiscTitleLayout.cs';source=$helperSource;destination=$helperDestination;beforeSha256=$helperBefore;afterSha256=$helperHash}
$obsoleteHelper=Join-Path $sourceRoot 'DemoDiscXboxTitleLayout.cs'
$removeObsoleteHelper=$false
if(Test-Path -LiteralPath $obsoleteHelper){
    if($Platform -ne 'xbox' -or (Get-FileHash -LiteralPath $obsoleteHelper).Hash.ToLowerInvariant() -ne $titleReceipt.obsoleteXboxHelperSha256){throw 'An unreviewed obsolete Xbox title helper exists; no source was written.'}
    $removeObsoleteHelper=$true
}
if($ValidateOnly){Write-Output "Validated combined HIDE and title inputs for $Platform`: $stage";return}
# Every proposal and destination has passed its guard. Write only the final sources, never an intermediate HIDE-only menu.
foreach($change in $changes){
    if($change.beforeSha256 -ne $change.afterSha256){[IO.File]::Copy($change.source,$change.destination,$true)}
}
if($removeObsoleteHelper){Remove-Item -LiteralPath $obsoleteHelper}
$now=[DateTime]::UtcNow.ToString('o')
[ordered]@{appliedUtc=$now;platform=$Platform;stagedProject=$stage;fixture=$isFixture;policy='hide-unavailable-and-align-painted-title-right-edges';files=$changes;removedReviewedXboxHelper=$removeObsoleteHelper;availabilitySourceReceipt=$hideReceiptPath;availabilitySourceReceiptSha256=(Get-FileHash $hideReceiptPath).Hash.ToLowerInvariant();titleSourceReceipt=$titleReceiptPath;titleSourceReceiptSha256=(Get-FileHash $titleReceiptPath).Hash.ToLowerInvariant();generatedFilesModified=$false;originalProjectModified=$false;intermediateMenuWritten=$false} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $stage 'menu-preparation-staging-receipt.json')
# Existing consumers can keep reading the established per-policy receipt paths.
[ordered]@{appliedUtc=$now;stagedProject=$stage;policy='hide-unavailable';sourcePatchReceipt=$hideReceiptPath;sourcePatchReceiptSha256=(Get-FileHash $hideReceiptPath).Hash.ToLowerInvariant();files=$changes;generatedFilesModified=$false;combinedPreparationReceipt='menu-preparation-staging-receipt.json'} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $stage 'menu-availability-staging-receipt.json')
[ordered]@{appliedUtc=$now;platform=$Platform;stagedProject=$stage;afterMenuSha256=$titleReceipt.menuPatchedSha256;helperSha256=$helperHash;sourcePatchReceipt=$titleReceiptPath;generatedFilesModified=$false;originalProjectModified=$false;policy=$titleReceipt.policy;combinedPreparationReceipt='menu-preparation-staging-receipt.json'} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $stage 'menu-title-fit-staging-receipt.json')
Write-Output "Prepared final HIDE and title sources for $Platform`: $stage"

$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$baseline='C:/dev/helworks/builds/helengine-n64/menu-20261007/menu-availability-hidden/MenuComponent.cs'
$expected='c7a28b38c136ae9e83d0393a07078ac3204a4f3b1b47f59924a928741050a6ad'
if((Get-FileHash -LiteralPath $baseline).Hash.ToLowerInvariant() -ne $expected){throw 'The reviewed HIDE menu baseline changed.'}
$code=[IO.File]::ReadAllText($baseline).Replace("`r`n","`n")
$constructor='        public MenuComponent() {'
$fields=@'
        /// <summary>Borrowed HELENGINE heading cached while this menu hierarchy remains bound.</summary>
        TextComponent FittedHelengineTitle;

        /// <summary>Borrowed DEMO DISC heading cached while this menu hierarchy remains bound.</summary>
        TextComponent FittedDemoDiscTitle;

        /// <summary>Initializes baked menu navigation and borrowed heading references.</summary>
        public MenuComponent() {
            FittedHelengineTitle = null;
            FittedDemoDiscTitle = null;
'@
if([regex]::Matches($code,[regex]::Escape($constructor)).Count -ne 1){throw 'Unexpected menu constructor count.'}
# Replace its existing constructor summary together, preserving field/member order.
$oldSummary="        /// <summary>`n        /// Initializes a new baked city menu root component.`n        /// </summary>`n"+$constructor
if(-not $code.Contains($oldSummary)){throw 'Reviewed constructor documentation differs.'}
$code=$code.Replace($oldSummary,$fields)
$update='            if (StartupInputGate.IsBlocked) {'
if([regex]::Matches($code,[regex]::Escape($update)).Count -ne 1){throw 'Unexpected menu update gate count.'}
$code=$code.Replace($update,@'
            // Viewport snapshots restore authored styles before this update; refit only the two headings.
            DemoDiscTitleLayout.FitPair(FittedHelengineTitle, FittedDemoDiscTitle);

            if (StartupInputGate.IsBlocked) {
'@)
$bind="            ActivatePanel(InitialPanelIdValue, false);`n            IsInitialized = true;"
if([regex]::Matches($code,[regex]::Escape($bind)).Count -ne 1){throw 'Unexpected menu initialization count.'}
$code=$code.Replace($bind,@'
            ActivatePanel(InitialPanelIdValue, false);
            FittedHelengineTitle = DemoDiscTitleLayout.Find(Parent, "HELENGINE");
            FittedDemoDiscTitle = DemoDiscTitleLayout.Find(Parent, "DEMO DISC");
            IsInitialized = true;
'@)
foreach($signature in @('        public override void ComponentRemoved(Entity entity) {','        public override void Dispose() {')){
    if([regex]::Matches($code,[regex]::Escape($signature)).Count -ne 1){throw "Unexpected lifecycle method count: $signature"}
    $code=$code.Replace($signature,$signature+"`n            FittedHelengineTitle = null;`n            FittedDemoDiscTitle = null;")
}
$menu=Join-Path $root 'MenuComponent.cs'
[IO.File]::WriteAllText($menu,$code,[Text.UTF8Encoding]::new($false))
$helper=Join-Path $root 'DemoDiscTitleLayout.cs'
[ordered]@{policy='fit-and-align-two-painted-right-edges-one-pixel-inside-overlay';platforms=@('dc','ps1','n64','ps3','x360','xbox','windows');menuBaseline=$baseline;menuBaselineSha256=$expected;menuPatchedSha256=(Get-FileHash $menu).Hash.ToLowerInvariant();acceptedPreviousMenuSha256=@('c0810919c6d3e380a37923f6067b2ec9bfe727c0f132423b23c505654718b7d8');helperSha256=(Get-FileHash $helper).Hash.ToLowerInvariant();acceptedPreviousHelperSha256=@('2fda5f183d0bd7070d414837591d41b49970e08ac94e0838e6356b2c559e9b4c','abd3d7444dc953c107978dc2c9548884cfe0809d91a514d77722bffa7f3f7fc3');acceptedXboxMenuSha256='bb1dae3d7d7aa5fc9bb154789e1ce5ab80141b6381ff622334e111cc1d106168';obsoleteXboxHelperSha256='1062b1112ab475ab420ed4f7ad1427bd66f451d4330ed2d7f6968fdd1b9f7d76';paintedRightContract='shared-overlay-right-minus-one-framebuffer-pixel';nativeOriginRoundingTolerancePixels=0.5;commonHeadingReducingRatio=$true;generatedFilesModified=$false;originalProjectModified=$false} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $root 'source-patch-receipt.json')
Write-Output 'Prepared reviewed HIDE menu title overlay in the fixed common directory.'

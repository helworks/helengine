$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$baseline='C:/dev/helworks/builds/helengine-n64/menu-20261007/menu-availability-hidden/MenuComponent.cs'
$receipt=Get-Content -LiteralPath (Join-Path $root 'source-patch-receipt.json') -Raw | ConvertFrom-Json
$expected=$receipt.menuBaselineSha256
if((Get-FileHash -LiteralPath $baseline).Hash.ToLowerInvariant() -ne $expected){throw 'The reviewed complete menu baseline changed.'}
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
$prepared='            base.ScenePrepared(entity);'
if([regex]::Matches($code,[regex]::Escape($prepared)).Count -ne 1){throw 'Unexpected menu scene preparation count.'}
$preparedEnd='The prepared menu requires its complete authored hierarchy.'
$preparedBlock="                throw new InvalidOperationException(`"$preparedEnd`");`n            }"
if([regex]::Matches($code,[regex]::Escape($preparedBlock)).Count -ne 1){throw 'Unexpected prepared menu hierarchy guard.'}
$code=$code.Replace($preparedBlock,$preparedBlock+"`n            DemoDiscTitleLayout.FitPair(FittedHelengineTitle, FittedDemoDiscTitle);")
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
$receipt.menuPatchedSha256=(Get-FileHash $menu).Hash.ToLowerInvariant()
$receipt.helperSha256=(Get-FileHash $helper).Hash.ToLowerInvariant()
$receipt | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $root 'source-patch-receipt.json')
Write-Output 'Prepared the complete authored menu with scene-ready fitted titles.'

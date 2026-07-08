namespace helengine.editor.tests;

/// <summary>
/// Verifies the committed city demo-disc menu scene asset set contains the new handheld scene and no longer keeps the obsolete DS companion asset.
/// </summary>
public sealed class CityHandheldMainMenuSceneAssetPresenceTests {
    /// <summary>
    /// Ensures the generated menu asset set includes the standard and handheld menu scenes and excludes the obsolete DS companion scene.
    /// </summary>
    [Fact]
    public void City_handheld_main_menu_asset_set_contains_handheld_scene_and_no_ds_companion_scene() {
        Assert.True(File.Exists(@"C:\dev\helprojs\city\assets\Scenes\DemoDiscMainMenu.helen"));
        Assert.True(File.Exists(@"C:\dev\helprojs\city\assets\Scenes\DemoDiscMainMenuHandheld.helen"));
        Assert.False(File.Exists(@"C:\dev\helprojs\city\assets\Scenes\DemoDiscMainMenuDs.helen"));
    }
}

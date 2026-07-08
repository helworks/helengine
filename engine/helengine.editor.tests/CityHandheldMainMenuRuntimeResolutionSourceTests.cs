namespace helengine.editor.tests;

/// <summary>
/// Verifies demo-disc runtime return-to-menu flows resolve the handheld main menu through one shared runtime helper.
/// </summary>
public sealed class CityHandheldMainMenuRuntimeResolutionSourceTests {
    /// <summary>
    /// Ensures both return-to-menu components route through one handheld-aware runtime resolver instead of duplicating direct scene-map lookups.
    /// </summary>
    [Fact]
    public void City_handheld_main_menu_runtime_resolution_source_uses_shared_menu_scene_resolver() {
        string resolverSource = File.ReadAllText(@"C:\dev\helprojs\city\assets\codebase\menu\DemoDiscMainMenuSceneResolver.cs");
        string returnSource = File.ReadAllText(@"C:\dev\helprojs\city\assets\codebase\menu\DemoDiscReturnToMenuComponent.cs");
        string dsReturnSource = File.ReadAllText(@"C:\dev\helprojs\city\assets\codebase\menu\NintendoDsReturnOverlayComponent.cs");

        Assert.Contains("public static string ResolveRuntimeSceneId()", resolverSource, StringComparison.Ordinal);
        Assert.Contains("PlatformMenuSceneResolver.NintendoHandheldMainMenuSceneId", resolverSource, StringComparison.Ordinal);
        Assert.Contains("DemoDiscMainMenuSceneResolver.ResolveRuntimeSceneId()", returnSource, StringComparison.Ordinal);
        Assert.Contains("DemoDiscMainMenuSceneResolver.ResolveRuntimeSceneId()", dsReturnSource, StringComparison.Ordinal);
    }
}

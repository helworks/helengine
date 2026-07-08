namespace helengine.editor.tests;

/// <summary>
/// Verifies the city demo-disc menu generator emits dedicated handheld scene authoring instead of relying on Nintendo DS scene augmentation.
/// </summary>
public sealed class CityHandheldMainMenuSourceTests {
    /// <summary>
    /// Ensures the handheld menu authoring path defines a first-class handheld scene id and owns the dual-screen menu helpers directly.
    /// </summary>
    [Fact]
    public void City_handheld_main_menu_source_defines_dedicated_handheld_scene_id() {
        string source = File.ReadAllText(@"C:\dev\helprojs\city\assets\codebase\menu.tools\DemoDiscHandheldMainMenuSceneFactory.cs");

        Assert.Contains("public const string SceneId = \"Scenes/DemoDiscMainMenuHandheld.helen\";", source, StringComparison.Ordinal);
        Assert.Contains("CreateNintendoDsTopScreenLogoEntity", source, StringComparison.Ordinal);
        Assert.Contains("CreateNintendoDsBottomScreenCameraEntity", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the scene generator writes both the standard and handheld menu scenes through separate factory entry points.
    /// </summary>
    [Fact]
    public void City_demo_disc_scene_generator_writes_standard_and_handheld_menu_scenes() {
        string source = File.ReadAllText(@"C:\dev\helprojs\city\assets\codebase\menu.tools\DemoDiscSceneGenerator.cs");

        Assert.Contains("SceneFactory.CreateStandardSceneDefinition(providerTypeName, definition)", source, StringComparison.Ordinal);
        Assert.Contains("SceneFactory.CreateHandheldSceneDefinition(providerTypeName, definition)", source, StringComparison.Ordinal);
        Assert.Contains("SceneWriteService.WriteScene(projectRootPath, standardSceneDefinition);", source, StringComparison.Ordinal);
        Assert.Contains("SceneWriteService.WriteScene(projectRootPath, handheldSceneDefinition);", source, StringComparison.Ordinal);
    }
}

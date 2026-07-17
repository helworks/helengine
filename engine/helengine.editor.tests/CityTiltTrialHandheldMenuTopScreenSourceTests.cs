namespace helengine.editor.tests;

/// <summary>
/// Verifies the Nintendo DS Tilt Trial menu owns its complete handheld layout instead of using the demo scaffold.
/// </summary>
public sealed class CityTiltTrialHandheldMenuTopScreenSourceTests {
    /// <summary>
    /// Ensures the handheld menu supplies game-owned roots for both screens and leaves the bottom screen empty.
    /// </summary>
    [Fact]
    public void Handheld_menu_top_screen_emits_title_without_hint() {
        string sourcePath = @"C:\dev\helprojs\demodisc\assets\codebase\game.tools\GameSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);
        int sceneMethodStart = source.IndexOf("public GeneratedAuthoringSceneDefinition CreateTiltTrialHandheldLevelSelectScene()", StringComparison.Ordinal);
        int sceneMethodEnd = source.IndexOf("        /// <summary>", sceneMethodStart + 1, StringComparison.Ordinal);

        Assert.True(sceneMethodStart >= 0, "The handheld menu scene authoring method must remain present.");
        Assert.True(sceneMethodEnd > sceneMethodStart, "The handheld menu scene authoring method must have a bounded source region.");

        string sceneMethodSource = source.Substring(sceneMethodStart, sceneMethodEnd - sceneMethodStart);
        Assert.Contains("RootEntities = []", sceneMethodSource, StringComparison.Ordinal);
        Assert.Contains("RootEntities = CreateTiltTrialHandheldLevelSelectSceneRoots()", sceneMethodSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BottomScreenRootEntities", sceneMethodSource, StringComparison.Ordinal);
        Assert.DoesNotContain("MoveTopScreen2DRootsToBottomScreen", sceneMethodSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateHandheldLevelSelectUiEntity()", sceneMethodSource, StringComparison.Ordinal);

        int sceneRootsMethodStart = source.IndexOf("Entity[] CreateTiltTrialHandheldLevelSelectSceneRoots()", StringComparison.Ordinal);
        int sceneRootsMethodEnd = source.IndexOf("        /// <summary>", sceneRootsMethodStart + 1, StringComparison.Ordinal);
        Assert.True(sceneRootsMethodStart >= 0, "The handheld menu root authoring method must remain present.");
        Assert.True(sceneRootsMethodEnd > sceneRootsMethodStart, "The handheld menu root authoring method must have a bounded source region.");

        string sceneRootsMethodSource = source.Substring(sceneRootsMethodStart, sceneRootsMethodEnd - sceneRootsMethodStart);
        Assert.Contains("CreateTiltTrialHandheldLevelSelectTopCameraEntity()", sceneRootsMethodSource, StringComparison.Ordinal);
        Assert.Contains("CreateHandheldLevelSelectTopInfoEntity()", sceneRootsMethodSource, StringComparison.Ordinal);
        Assert.Contains("CreateTiltTrialHandheldLevelSelectBottomScreenCameraEntity()", sceneRootsMethodSource, StringComparison.Ordinal);

        int topInfoMethodStart = source.IndexOf("EditorEntity CreateHandheldLevelSelectTopInfoEntity()", StringComparison.Ordinal);
        int topInfoMethodEnd = source.IndexOf("        /// <summary>", topInfoMethodStart + 1, StringComparison.Ordinal);
        Assert.True(topInfoMethodStart >= 0, "The handheld menu top-screen information method must remain present.");
        Assert.True(topInfoMethodEnd > topInfoMethodStart, "The handheld menu top-screen information method must have a bounded source region.");

        string topInfoMethodSource = source.Substring(topInfoMethodStart, topInfoMethodEnd - topInfoMethodStart);
        Assert.Contains("CreateUiTextEntity(entity, \"TiltTrialHandheldLevelSelectTitle\"", topInfoMethodSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TiltTrialHandheldLevelSelectHint", topInfoMethodSource, StringComparison.Ordinal);
    }
}

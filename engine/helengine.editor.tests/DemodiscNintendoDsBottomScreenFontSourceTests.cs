namespace helengine.editor.tests;

/// <summary>
/// Verifies the demodisc Nintendo DS bottom-screen scaffold uses the normal authored body-font pipeline instead of one editor-only generated font hook.
/// </summary>
public sealed class DemodiscNintendoDsBottomScreenFontSourceTests {
    /// <summary>
    /// Ensures the demodisc Nintendo DS scaffold persists bottom-screen text through the shared authored font reference path.
    /// </summary>
    [Fact]
    public void Demodisc_ds_scaffold_source_uses_authored_body_font_references() {
        string sourcePath = @"C:\dev\helprojs\demodisc\assets\codebase\rendering.tools\NintendoDsRenderingSceneScaffoldFactory.cs";
        Assert.True(File.Exists(sourcePath), $"Expected source file '{sourcePath}' to exist.");

        string source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("CreateNintendoDsDebugFontReference()", source, StringComparison.Ordinal);
        Assert.Contains("ApplyFontReference(fpsEntity, bottomScreenFpsComponent);", source, StringComparison.Ordinal);
        Assert.Contains("ApplyFontReference(lightButtonLabelEntity, labelComponent);", source, StringComparison.Ordinal);
        Assert.Contains("ApplyFontReference(backButtonLabelEntity, labelComponent);", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures generated demodisc scene writing resolves the live bottom-overlay font through the shared authored font import path.
    /// </summary>
    [Fact]
    public void Demodisc_generated_scene_writer_source_avoids_editor_only_ds_font_factory() {
        string sourcePath = @"C:\dev\helprojs\demodisc\assets\codebase\rendering.tools\GeneratedAuthoringSceneWriteService.cs";
        Assert.True(File.Exists(sourcePath), $"Expected source file '{sourcePath}' to exist.");

        string source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("ResolveRequiredNintendoDsDebugFont()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NintendoDsDebugFontFactory", source, StringComparison.Ordinal);
        Assert.Contains("DemoDiscSceneComponentRecordFactory.CreateEditorFontReference()", source, StringComparison.Ordinal);
        Assert.Contains("EditorFileSystemFontResolver", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures playable physics Nintendo DS scenes persist bottom-screen overlay fonts through the shared authored font reference path.
    /// </summary>
    [Fact]
    public void Demodisc_physics_scene_factory_source_uses_authored_body_font_references() {
        string sourcePath = @"C:\dev\helprojs\demodisc\assets\codebase\physics.tools\PhysicsSceneFactory.cs";
        Assert.True(File.Exists(sourcePath), $"Expected source file '{sourcePath}' to exist.");

        string source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("DemoDiscSceneComponentRecordFactory.CreateEditorUiFontReference()", source, StringComparison.Ordinal);
        Assert.Contains("saveComponent.SetAssetReference(component, \"Font\", DemoDiscSceneComponentRecordFactory.CreateEditorFontReference());", source, StringComparison.Ordinal);
        Assert.Contains("saveState.SetAssetReference(\"Font\", DemoDiscSceneComponentRecordFactory.CreateEditorFontReference());", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the physics DS generator keeps the matrix-render scene on the bespoke overlay path instead of layering the default DS footer over its authored overlay.
    /// </summary>
    [Fact]
    public void Demodisc_physics_ds_generator_source_keeps_matrix_render_on_custom_overlay_path() {
        string sourcePath = @"C:\dev\helprojs\demodisc\assets\codebase\physics.tools\PhysicsNintendoDsSceneGenerator.cs";
        Assert.True(File.Exists(sourcePath), $"Expected source file '{sourcePath}' to exist.");

        string source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("|| string.Equals(sceneEntry.SceneId, \"test_scene_matrix_render\", StringComparison.Ordinal)", source, StringComparison.Ordinal);
        Assert.Contains("if (string.Equals(sceneEntry.SceneId, PhysicsSceneCatalog.MatrixRenderSceneId, StringComparison.Ordinal))", source, StringComparison.Ordinal);
        Assert.Contains("authoredSceneAsset = CreateFreshPhysicsSceneAssetWithoutSharedMusic(sceneEntry.SceneId);", source, StringComparison.Ordinal);
        Assert.Contains("authoredSceneAsset.RootEntities = RemoveNintendoHandheldOnlyEntities(authoredSceneAsset.RootEntities, supportedPlatformIds);", source, StringComparison.Ordinal);
        Assert.Contains("UseDefaultBottomOverlay = true", source, StringComparison.Ordinal);
        Assert.Contains("bool IsNintendoHandheldOnlyEntity(", source, StringComparison.Ordinal);
    }
}

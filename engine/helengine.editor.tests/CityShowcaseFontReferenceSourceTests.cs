namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored city showcase source keeps distinct font-reference paths for FPS overlays and demo-disc body text.
/// </summary>
public sealed class CityShowcaseFontReferenceSourceTests {
    /// <summary>
    /// Ensures the shared showcase component-record factory keeps separate helpers for the body-font reference and the generated editor UI-font reference.
    /// </summary>
    [Fact]
    public void City_showcase_font_reference_factory_source_separates_body_font_and_editor_ui_font_helpers() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\rendering.tools\DemoDiscSceneComponentRecordFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("const string BodyFontRelativePath = \"Fonts/DemoDiscBody.ttf\";", source, StringComparison.Ordinal);
        Assert.Contains("public static SceneAssetReference CreateEditorFontReference()", source, StringComparison.Ordinal);
        Assert.Contains("SourceKind = SceneAssetReferenceSourceKind.FileSystem,", source, StringComparison.Ordinal);
        Assert.Contains("RelativePath = BodyFontRelativePath", source, StringComparison.Ordinal);
        Assert.Contains("public static SceneAssetReference CreateEditorUiFontReference()", source, StringComparison.Ordinal);
        Assert.Contains("RelativePath = EditorFontRelativePath,", source, StringComparison.Ordinal);
        Assert.Contains("ProviderId = EditorGeneratedProviderId,", source, StringComparison.Ordinal);
        Assert.Contains("AssetId = EditorFontAssetId", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the shared FPS serialization path and the authored showcase FPS entities use the generated editor UI-font reference instead of the demo-disc body-font reference.
    /// </summary>
    [Fact]
    public void City_showcase_fps_source_uses_editor_ui_font_reference() {
        string[] sourcePaths = new[] {
            @"C:\dev\helprojs\city\assets\codebase\rendering.tools\DemoDiscSceneComponentRecordFactory.cs",
            @"C:\dev\helprojs\city\assets\codebase\rendering.tools\DirectionalShadowPlazaSceneFactory.cs",
            @"C:\dev\helprojs\city\assets\codebase\rendering.tools\SpotlightStreetSliceSceneFactory.cs",
            @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs"
        };

        for (int index = 0; index < sourcePaths.Length; index++) {
            string source = File.ReadAllText(sourcePaths[index]);
            Assert.Contains("CreateEditorUiFontReference()", source, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Ensures the shared non-FPS showcase overlays keep using the demo-disc body-font reference so their label sizing stays consistent with the authored look.
    /// </summary>
    [Fact]
    public void City_showcase_non_fps_overlay_source_keeps_body_font_reference() {
        string[] sourcePaths = new[] {
            @"C:\dev\helprojs\city\assets\codebase\rendering.tools\DemoDiscLightIndicatorOverlayFactory.cs",
            @"C:\dev\helprojs\city\assets\codebase\rendering.tools\DemoSceneInstructionOverlayFactory.cs"
        };

        for (int index = 0; index < sourcePaths.Length; index++) {
            string source = File.ReadAllText(sourcePaths[index]);
            Assert.Contains("CreateEditorFontReference()", source, StringComparison.Ordinal);
        }
    }
}

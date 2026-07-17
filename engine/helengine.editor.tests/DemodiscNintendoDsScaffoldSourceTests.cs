namespace helengine.editor.tests;

/// <summary>
/// Verifies that the demodisc Nintendo DS scaffold routes shared authored 2D roots to the bottom-screen viewport.
/// </summary>
public sealed class DemodiscNintendoDsScaffoldSourceTests {
    /// <summary>
    /// Ensures generated handheld scenes use one bottom viewport for FPS and remaining authored 2D roots.
    /// </summary>
    [Fact]
    public void Demodisc_ds_scaffold_moves_remaining_2d_roots_to_the_bottom_viewport() {
        string sourcePath = @"C:\dev\helprojs\demodisc\assets\codebase\rendering.tools\NintendoDsRenderingSceneScaffoldFactory.cs";
        Assert.True(File.Exists(sourcePath), $"Expected source file '{sourcePath}' to exist.");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("Move2DRootsUnderBottomScreenViewport", source, StringComparison.Ordinal);
        Assert.Contains("bottomScreenViewportRoot.AddChild(rootEntity);", source, StringComparison.Ordinal);
        Assert.Contains("NormalizeBottomScreenCoordinatesRecursive(rootEntity);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateChild(topScreenCameraEntity, \"DemoDiscTopScreenRoot\")", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures scenes that opt out of the default bottom overlay do not receive scaffold-owned diagnostics or controls.
    /// </summary>
    [Fact]
    public void Demodisc_ds_scaffold_gates_default_bottom_controls() {
        string sourcePath = @"C:\dev\helprojs\demodisc\assets\codebase\rendering.tools\NintendoDsRenderingSceneScaffoldFactory.cs";
        Assert.True(File.Exists(sourcePath), $"Expected source file '{sourcePath}' to exist.");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("if (useDefaultBottomOverlay) {", source, StringComparison.Ordinal);
        Assert.Contains("RelocateFpsComponentsToBottomScreen(filteredTopScreenRoots, bottomScreenViewportRoot, bottomOverlayFont);", source, StringComparison.Ordinal);
        Assert.Contains("CreateBottomScreenLightButton(bottomScreenViewportRoot, bottomOverlayFont);", source, StringComparison.Ordinal);
        Assert.Contains("CreateBottomScreenBackButton(bottomScreenViewportRoot, bottomOverlayFont);", source, StringComparison.Ordinal);
    }
}

namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored city demo-disc menu source keeps viewport-anchored platform info for non-widescreen targets.
/// </summary>
public sealed class CityMenuSourceTests {
    /// <summary>
    /// Ensures the platform info overlay anchors against the resolved camera viewport instead of the authored reference canvas.
    /// </summary>
    [Fact]
    public void City_demo_disc_menu_source_anchors_platform_info_overlay_to_camera_viewport() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\menu.tools\DemoDiscMainMenuSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("anchorComponent.LayoutSpace = LayoutComponent.CameraViewportLayoutSpace;", source, StringComparison.Ordinal);
        Assert.Contains("anchorComponent.SetAnchorDistances(right: platformInfoOverlay.RightMargin, top: platformInfoOverlay.TopMargin);", source, StringComparison.Ordinal);
    }
}

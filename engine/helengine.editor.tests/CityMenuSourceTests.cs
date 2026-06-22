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

    /// <summary>
    /// Ensures the desktop and Nintendo DS menu cameras render the authored scene-object layer so generated menu drawables register with their queues.
    /// </summary>
    [Fact]
    public void City_demo_disc_menu_source_uses_scene_object_layer_for_all_menu_cameras() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\menu.tools\DemoDiscMainMenuSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Equal(3, CountOccurrences(source, "LayerMask = EditorLayerMasks.SceneObjects,"));
        Assert.DoesNotContain("LayerMask = 1,", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Counts exact substring occurrences inside the supplied source text so source-shape regressions can be asserted directly.
    /// </summary>
    /// <param name="source">Full source text under test.</param>
    /// <param name="value">Exact substring that should be counted.</param>
    /// <returns>Total number of exact substring occurrences.</returns>
    static int CountOccurrences(string source, string value) {
        if (source == null) {
            throw new ArgumentNullException(nameof(source));
        } else if (string.IsNullOrEmpty(value)) {
            throw new ArgumentException("Search value must be provided.", nameof(value));
        }

        int count = 0;
        int startIndex = 0;
        while (true) {
            int matchIndex = source.IndexOf(value, startIndex, StringComparison.Ordinal);
            if (matchIndex < 0) {
                return count;
            }

            count++;
            startIndex = matchIndex + value.Length;
        }
    }
}

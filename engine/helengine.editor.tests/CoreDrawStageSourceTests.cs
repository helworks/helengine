using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies authored draw-stage markers that platform hosts consume after normal code generation.
/// </summary>
public sealed class CoreDrawStageSourceTests {
    /// <summary>
    /// Ensures FPS and debug frame counters each have a distinct transition marker without patching generated output.
    /// </summary>
    [Fact]
    public void Draw_RecordsDistinctMarkersBeforeFpsAndDebugFrameCounters() {
        string engineRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string coreSource = File.ReadAllText(Path.Combine(engineRootPath, "helengine.core", "Core.cs"));

        int fpsMarkerIndex = coreSource.IndexOf("LastSceneTransitionStage = \"BeforeFpsRenderFrame\";", StringComparison.Ordinal);
        int fpsCallIndex = coreSource.IndexOf("FPSComponent.RecordRenderFrame();", StringComparison.Ordinal);
        int debugMarkerIndex = coreSource.IndexOf("LastSceneTransitionStage = \"BeforeDebugRenderFrame\";", StringComparison.Ordinal);
        int debugCallIndex = coreSource.IndexOf("DebugComponent.RecordRenderFrame();", StringComparison.Ordinal);

        Assert.True(fpsMarkerIndex >= 0 && fpsCallIndex > fpsMarkerIndex);
        Assert.True(debugMarkerIndex > fpsCallIndex && debugCallIndex > debugMarkerIndex);
    }
}

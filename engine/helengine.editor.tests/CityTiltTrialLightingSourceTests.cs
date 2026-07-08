namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored Tilt Trial lighting source keeps the requested two-light setup for the marble sphere.
/// </summary>
public sealed class CityTiltTrialLightingSourceTests {
    /// <summary>
    /// Ensures the Tilt Trial scene authors one weaker directional fill light so the sphere's unlit side does not fall fully into shadow.
    /// </summary>
    [Fact]
    public void City_tilt_trial_scene_source_authors_weaker_shadowless_fill_light() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\game.tools\GameSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("CreateDirectionalLightEntity(),", source, StringComparison.Ordinal);
        Assert.Contains("CreateDirectionalFillLightEntity(),", source, StringComparison.Ordinal);
        Assert.Contains("Entity entity = Core.Instance.EntityFactory.Create(\"TiltTrialFill\");", source, StringComparison.Ordinal);
        Assert.Contains("Intensity = 0.6f,", source, StringComparison.Ordinal);
        Assert.Contains("ShadowsEnabled = false,", source, StringComparison.Ordinal);
    }
}

namespace helengine.editor.tests;

/// <summary>
/// Verifies the Nintendo DS 3D renderer keeps textured geometry on the immediate submission path until the textured display-list path is proven correct.
/// </summary>
public sealed class NintendoDsRenderManager3DSourceTests {
    /// <summary>
    /// Ensures textured DS draws do not route through the cached static display-list path, which regresses textured cube scenes to flat white output.
    /// </summary>
    [Fact]
    public void Nintendo_ds_textured_draws_bypass_static_display_lists() {
        string sourcePath = @"C:\dev\helworks\helengine-ds\src\platform\ds\NintendoDsRenderManager3D.cpp";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("} else if (useHardwareTexture) {", source, StringComparison.Ordinal);
        Assert.Contains("            return false;", source, StringComparison.Ordinal);
    }
}

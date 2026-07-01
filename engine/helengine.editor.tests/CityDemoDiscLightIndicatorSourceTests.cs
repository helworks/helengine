namespace helengine.editor.tests;

/// <summary>
/// Verifies the shared city demo-disc light indicator source stays wired to the intended runtime and scene-authoring shape.
/// </summary>
public sealed class CityDemoDiscLightIndicatorSourceTests {
    /// <summary>
    /// Ensures the shared indicator overlay factory authors the Light label and preview square.
    /// </summary>
    [Fact]
    public void City_light_indicator_overlay_factory_authors_label_and_preview_square() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\rendering.tools\DemoDiscLightIndicatorOverlayFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("Text = \"Light\"", source, StringComparison.Ordinal);
        Assert.Contains("new RoundedRectComponent", source, StringComparison.Ordinal);
        Assert.Contains("DemoDiscLightIndicatorSwatch", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ScalingMode = ViewportComponent.ReferenceCanvasScalingMode", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the demo-disc light toggle uses the requested six-state color cycle and white startup normalization.
    /// </summary>
    [Fact]
    public void City_light_toggle_source_uses_requested_cycle_order() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\rendering\DemoDiscLightToggleComponent.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("CurrentLightStateIndex = 0", source, StringComparison.Ordinal);
        Assert.Contains("new float4(1f, 1f, 1f, 1f)", source, StringComparison.Ordinal);
        Assert.Contains("new float4(1f, 1f, 0f, 1f)", source, StringComparison.Ordinal);
        Assert.Contains("new float4(1f, 0f, 0f, 1f)", source, StringComparison.Ordinal);
        Assert.Contains("new float4(0f, 0f, 1f, 1f)", source, StringComparison.Ordinal);
        Assert.Contains("new float4(0f, 1f, 0f, 1f)", source, StringComparison.Ordinal);
        Assert.Contains("ApplyCurrentLightState();", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures every supported rendering demo scene factory attaches the shared light indicator helper to its scene UI root.
    /// </summary>
    [Fact]
    public void City_rendering_scene_factories_attach_shared_light_indicator_helper() {
        string[] sourcePaths = new[] {
            @"C:\dev\helprojs\city\assets\codebase\rendering.tools\AxisTestSceneFactory.cs",
            @"C:\dev\helprojs\city\assets\codebase\rendering.tools\AxisTest2SceneFactory.cs",
            @"C:\dev\helprojs\city\assets\codebase\rendering.tools\ColoredCubeGridSceneFactory.cs",
            @"C:\dev\helprojs\city\assets\codebase\rendering.tools\CubeTestSceneFactory.cs",
            @"C:\dev\helprojs\city\assets\codebase\rendering.tools\DirectionalShadowPlazaSceneFactory.cs",
            @"C:\dev\helprojs\city\assets\codebase\rendering.tools\ScaledCubeSceneFactory.cs",
            @"C:\dev\helprojs\city\assets\codebase\rendering.tools\TexturedCubeGridSceneFactory.cs"
        };

        for (int index = 0; index < sourcePaths.Length; index++) {
            string source = File.ReadAllText(sourcePaths[index]);
            Assert.Contains("DemoDiscLightIndicatorOverlayFactory", source, StringComparison.Ordinal);
            Assert.Contains("AttachToSceneUi", source, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Ensures the textured cube-grid authored material payload keeps the diffuse texture id on the top-level shader material asset so generated sidecars cannot collapse it back to empty.
    /// </summary>
    [Fact]
    public void City_textured_cube_grid_authored_material_source_preserves_diffuse_texture_id() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\rendering.tools\TexturedCubeGridSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("CreateAuthoredMaterialAsset(int cubeIndex)", source, StringComparison.Ordinal);
        Assert.Contains("Id = CreateMaterialAssetId(cubeIndex),", source, StringComparison.Ordinal);
        Assert.Contains("ShaderAssetId = StandardShaderAssetId,", source, StringComparison.Ordinal);
        Assert.Contains("VertexProgram = StandardVertexProgramName,", source, StringComparison.Ordinal);
        Assert.Contains("PixelProgram = StandardPixelProgramName,", source, StringComparison.Ordinal);
        Assert.Contains("Variant = MeshVariantName,", source, StringComparison.Ordinal);
        Assert.Contains("DiffuseTextureAssetId = CubeTextureAssetIds[cubeIndex],", source, StringComparison.Ordinal);
    }
}

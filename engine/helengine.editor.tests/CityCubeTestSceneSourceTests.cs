namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored city cube-test scene stays a minimal static ground-and-cube diagnostic scene.
/// </summary>
public sealed class CityCubeTestSceneSourceTests {
    /// <summary>
    /// Ensures the authored cube-test scene mirrors the minimal falling-cube layout without attaching runtime rotation to the test cube.
    /// </summary>
    [Fact]
    public void City_cube_test_scene_source_uses_ground_cube_and_elevated_static_cube() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\rendering.tools\CubeTestSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("CreateGroundEntity(cubeModel, standardMaterial)", source, StringComparison.Ordinal);
        Assert.Contains("entity.LocalPosition = new float3(0f, -0.5f, 0f);", source, StringComparison.Ordinal);
        Assert.Contains("entity.LocalScale = new float3(14f, 1f, 14f);", source, StringComparison.Ordinal);
        Assert.Contains("entity.LocalPosition = new float3(0f, 5f, 0f);", source, StringComparison.Ordinal);
        Assert.Contains("entity.LocalScale = float3.One;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("entity.AddComponent(new AxisRotationComponent", source, StringComparison.Ordinal);
        Assert.DoesNotContain("entity.AddComponent(new DemoDiscReturnToMenuComponent", source, StringComparison.Ordinal);
        Assert.DoesNotContain("entity.AddComponent(new city.rendering.DemoDiscOrbitCameraComponent", source, StringComparison.Ordinal);
    }
}

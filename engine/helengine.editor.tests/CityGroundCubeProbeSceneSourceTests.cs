namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored city ground-cube probe scene keeps the exact minimal layout requested for render-path isolation.
/// </summary>
public sealed class CityGroundCubeProbeSceneSourceTests {
    /// <summary>
    /// Ensures the authored probe scene contains only the large ground cube and the elevated unit cube without orbit-camera or return-to-menu behavior.
    /// </summary>
    [Fact]
    public void City_ground_cube_probe_scene_source_uses_requested_ground_and_cube_layout() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\rendering.tools\GroundCubeProbeSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("public const string SceneId = RenderingSceneGenerator.GroundCubeProbeSceneId;", source, StringComparison.Ordinal);
        Assert.Contains("entity.LocalPosition = new float3(0f, -0.5f, 0f);", source, StringComparison.Ordinal);
        Assert.Contains("entity.LocalScale = new float3(15f, 1f, 15f);", source, StringComparison.Ordinal);
        Assert.Contains("entity.LayerMask = EditorLayerMasks.SceneObjects;", source, StringComparison.Ordinal);
        Assert.Contains("BodyKind = BodyKind3D.Static", source, StringComparison.Ordinal);
        Assert.Contains("entity.AddComponent(CreateBoxColliderComponent(new float3(15f, 1f, 15f)));", source, StringComparison.Ordinal);
        Assert.Contains("entity.LocalPosition = new float3(0f, 10f, 0f);", source, StringComparison.Ordinal);
        Assert.Contains("entity.LocalScale = float3.One;", source, StringComparison.Ordinal);
        Assert.Contains("BodyKind = BodyKind3D.Dynamic", source, StringComparison.Ordinal);
        Assert.Contains("UseGravity = true", source, StringComparison.Ordinal);
        Assert.Contains("Mass = 1d", source, StringComparison.Ordinal);
        Assert.Contains("entity.AddComponent(CreateBoxColliderComponent(float3.One));", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DemoDiscOrbitCameraComponent", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DemoDiscReturnToMenuComponent", source, StringComparison.Ordinal);
    }
}

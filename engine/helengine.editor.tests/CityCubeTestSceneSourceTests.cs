namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored city cube-test scene stays the intended one-cube rotating diagnostic scene.
/// </summary>
public sealed class CityCubeTestSceneSourceTests {
    /// <summary>
    /// Ensures the authored cube-test scene keeps one rotating cube at the origin while restoring the shared instruction and UI path.
    /// </summary>
    [Fact]
    public void City_cube_test_scene_source_uses_one_rotating_cube_with_shared_instruction_ui() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\rendering.tools\CubeTestSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("CreateCameraEntity()", source, StringComparison.Ordinal);
        Assert.Contains("DemoSceneInstructionOverlayFactory instructionOverlayFactory = new DemoSceneInstructionOverlayFactory();", source, StringComparison.Ordinal);
        Assert.Contains("Entity instructionOverlayEntity = instructionOverlayFactory.CreateDesktopInstructionOverlayRoot(instructionFont);", source, StringComparison.Ordinal);
        Assert.Contains("CreateUiEntity()", source, StringComparison.Ordinal);
        Assert.Contains("CreateDirectionalLightEntity()", source, StringComparison.Ordinal);
        Assert.Contains("CreateCubeEntity(cubeModel, solidColorMaterial)", source, StringComparison.Ordinal);
        Assert.Contains("entity.AddComponent(new city.rendering.DemoDiscOrbitCameraComponent", source, StringComparison.Ordinal);
        Assert.Contains("OrbitCenter = float3.Zero", source, StringComparison.Ordinal);
        Assert.Contains("AutoYawSpeedRadians = 0f", source, StringComparison.Ordinal);
        Assert.Contains("float4.CreateFromYawPitchRoll(0f, 0f, 0f, out orientation);", source, StringComparison.Ordinal);
        Assert.Contains("UseDefaultBottomOverlay = false", source, StringComparison.Ordinal);
        Assert.Contains("BottomScreenRootEntities = Array.Empty<Entity>()", source, StringComparison.Ordinal);
        Assert.Contains("entity.LocalPosition = new float3(0f, 0f, 5f);", source, StringComparison.Ordinal);
        Assert.Contains("entity.LocalPosition = new float3(0f, 0f, 0f);", source, StringComparison.Ordinal);
        Assert.Contains("entity.LocalScale = new float3(1f, 1f, 1f);", source, StringComparison.Ordinal);
        Assert.Contains("entity.AddComponent(new city.rendering.CubeTestSpinComponent", source, StringComparison.Ordinal);
        Assert.Contains("AngularSpeedRadians = CubeAngularSpeedRadians", source, StringComparison.Ordinal);
        Assert.Contains("entity.AddComponent(new FPSComponent", source, StringComparison.Ordinal);
        Assert.Contains("entity.AddComponent(new DemoDiscReturnToMenuComponent());", source, StringComparison.Ordinal);
        Assert.Contains("entity.AddComponent(new DemoDiscLightToggleComponent());", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateGroundEntity(cubeModel, standardMaterial)", source, StringComparison.Ordinal);
    }
}

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
        Assert.Contains("entity.LayerMask = EditorLayerMasks.SceneObjects;", source, StringComparison.Ordinal);
        Assert.Contains("entity.AddComponent(new city.rendering.CubeTestSpinComponent", source, StringComparison.Ordinal);
        Assert.Contains("AngularSpeedRadians = CubeAngularSpeedRadians", source, StringComparison.Ordinal);
        Assert.Contains("entity.AddComponent(new FPSComponent", source, StringComparison.Ordinal);
        Assert.Contains("DemoDiscLightIndicatorOverlayFactory lightIndicatorOverlayFactory = new DemoDiscLightIndicatorOverlayFactory();", source, StringComparison.Ordinal);
        Assert.Contains("lightIndicatorOverlayFactory.AttachToSceneUi(entity, ResolveRequiredEditorFont());", source, StringComparison.Ordinal);
        Assert.Contains("entity.AddComponent(new DemoDiscReturnToMenuComponent());", source, StringComparison.Ordinal);
        Assert.Contains("entity.AddComponent(new DemoDiscLightToggleComponent());", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateGroundEntity(cubeModel, standardMaterial)", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the authored cube-test material stays on the lit forward standard shader so the light-toggle overlay has a visible effect.
    /// </summary>
    [Fact]
    public void City_cube_test_material_source_uses_lit_forward_standard_shader() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\rendering.tools\ForwardSolidColorMaterialFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("const string SolidColorShaderAssetId = \"ForwardStandardShader\";", source, StringComparison.Ordinal);
        Assert.Contains("const string SolidColorVertexProgramName = \"ForwardStandardShader.vs\";", source, StringComparison.Ordinal);
        Assert.Contains("const string SolidColorPixelProgramName = \"ForwardStandardShader.ps\";", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ForwardSolidColorShader", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the demo-disc light toggle captures directional lights after full scene initialization so later root entities are included.
    /// </summary>
    [Fact]
    public void City_demo_disc_light_toggle_source_captures_lights_during_component_initialized() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\rendering\DemoDiscLightToggleComponent.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("public override void ComponentInitialized(Entity entity)", source, StringComparison.Ordinal);
        Assert.Contains("CaptureDirectionalLightStates();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public override void ComponentAdded(Entity entity)", source, StringComparison.Ordinal);
    }
}

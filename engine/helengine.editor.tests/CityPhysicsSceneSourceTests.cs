namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored city physics scene source keeps the intended validation-scene layouts.
/// </summary>
public sealed class CityPhysicsSceneSourceTests {
    /// <summary>
    /// Ensures the authored city stack-box scene offsets each higher cube slightly farther along positive X.
    /// </summary>
    [Fact]
    public void City_dynamic_stack_boxes_source_uses_incremental_positive_x_offsets() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("CreatePhysicsBoxMeshEntity(\"dynamic_stack_boxes.box01\", \"StackBox01\", new float3(0f, 0.5f, 0f)", source, StringComparison.Ordinal);
        Assert.Contains("CreatePhysicsBoxMeshEntity(\"dynamic_stack_boxes.box02\", \"StackBox02\", new float3(0.5f, 1.5f, 0f)", source, StringComparison.Ordinal);
        Assert.Contains("CreatePhysicsBoxMeshEntity(\"dynamic_stack_boxes.box03\", \"StackBox03\", new float3(1.0f, 2.5f, 0f)", source, StringComparison.Ordinal);
        Assert.Contains("CreatePhysicsBoxMeshEntity(\"dynamic_stack_boxes.box04\", \"StackBox04\", new float3(1.5f, 3.5f, 0f)", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the authored city falling-cube scene keeps one static ground box and one elevated dynamic cube for the minimal BEPU repro.
    /// </summary>
    [Fact]
    public void City_single_falling_cube_source_uses_ground_and_elevated_dynamic_cube() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("CreatePhysicsBoxMeshEntity(\"single_falling_cube.ground\", \"Ground\", new float3(0f, -0.5f, 0f), new float3(14f, 1f, 14f), float4.Identity, StaticBodyKindCode, false", source, StringComparison.Ordinal);
        Assert.Contains("CreatePhysicsBoxMeshEntity(\"single_falling_cube.box01\", \"FallingCube\", new float3(0f, 5f, 0f), new float3(1f, 1f, 1f), float4.Identity, DynamicBodyKindCode, true", source, StringComparison.Ordinal);
    }
}

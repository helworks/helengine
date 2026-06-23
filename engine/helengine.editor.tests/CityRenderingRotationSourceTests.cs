namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored city rendering rotation scripts use quaternion composition instead of component-wise float4 multiplication.
/// </summary>
public sealed class CityRenderingRotationSourceTests {
    /// <summary>
    /// Ensures the city axis-rotation gameplay script composes local orientation with <c>float4.Concatenate</c>.
    /// </summary>
    [Fact]
    public void City_axis_rotation_component_source_uses_quaternion_concatenation() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\rendering\AxisRotationComponent.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("float4.Concatenate(ref currentOrientation, ref deltaRotation, out orientation);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Parent.LocalOrientation * deltaRotation", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the Axis 1 showcase scene rotates the authored light arrow around a visible axis instead of an invisible roll around the arrow forward axis.
    /// </summary>
    [Fact]
    public void City_axis_test_scene_source_uses_axis_rotation_component_instead_of_z_roll() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\rendering.tools\AxisTestSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("new gameplay.rendering.AxisRotationComponent", source, StringComparison.Ordinal);
        Assert.Contains("Axis = new float3(1f, 0f, 0f)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new AxisTestZSpinComponent", source, StringComparison.Ordinal);
    }
}

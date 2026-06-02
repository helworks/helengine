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
}

using helengine.editor.tests.testing;

namespace helengine.editor.tests;

/// <summary>
/// Verifies entity world-transform composition across parented hierarchies.
/// </summary>
public sealed class EntityHierarchyTransformTests {
    /// <summary>
    /// Ensures child world position incorporates the parent scale before the parent rotation is applied.
    /// </summary>
    [Fact]
    public void Child_world_position_applies_parent_scale_before_parent_rotation() {
        InitializeCore();

        Entity parent = new Entity();
        parent.InitChildren();
        parent.LocalPosition = new float3(10f, 20f, 30f);
        parent.LocalScale = new float3(2f, 3f, 4f);
        float4.CreateFromYawPitchRoll((float)(Math.PI / 2d), 0f, 0f, out float4 parentOrientation);
        parent.LocalOrientation = parentOrientation;

        Entity child = new Entity();
        child.InitChildren();
        child.LocalPosition = new float3(1f, 2f, 3f);
        parent.AddChild(child);

        float3 expectedScaledLocal = new float3(2f, 6f, 12f);
        float3 expectedPosition = float4.RotateVector(expectedScaledLocal, parentOrientation) + parent.LocalPosition;
        AssertApproximately(expectedPosition, child.Position);
    }

    /// <summary>
    /// Initializes the minimal core services required by entity hierarchy tests.
    /// </summary>
    /// <returns>Initialized test core.</returns>
    static Core InitializeCore() {
        Core core = new Core();
        core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        return core;
    }

    /// <summary>
    /// Asserts two vectors match within a small transform tolerance.
    /// </summary>
    /// <param name="expected">Expected vector.</param>
    /// <param name="actual">Actual vector.</param>
    static void AssertApproximately(float3 expected, float3 actual) {
        Assert.True(Math.Abs(expected.X - actual.X) < 0.0001f);
        Assert.True(Math.Abs(expected.Y - actual.Y) < 0.0001f);
        Assert.True(Math.Abs(expected.Z - actual.Z) < 0.0001f);
    }
}

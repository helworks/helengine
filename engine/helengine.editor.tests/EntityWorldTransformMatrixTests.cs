using helengine.editor.tests.testing;

namespace helengine.editor.tests;

/// <summary>
/// Verifies exact world-matrix composition preserves parented transforms that cannot be reconstructed from decomposed world position, orientation, and scale values alone.
/// </summary>
public sealed class EntityWorldTransformMatrixTests {
    /// <summary>
    /// Ensures the exact world matrix matches recursive local-matrix composition and diverges from the lossy recomposed world transform under rotated non-uniform parents.
    /// </summary>
    [Fact]
    public void World_transform_matrix_preserves_parent_scale_rotation_composition() {
        InitializeCore();

        Entity parent = new Entity();
        parent.InitChildren();
        parent.LocalPosition = new float3(3f, 5f, 7f);
        parent.LocalScale = new float3(2f, 1.5f, 3f);
        float4.CreateFromYawPitchRoll((float)(Math.PI / 4d), 0f, (float)(Math.PI / 10d), out float4 parentOrientation);
        parent.LocalOrientation = parentOrientation;

        Entity child = new Entity();
        child.InitChildren();
        child.LocalPosition = new float3(1.25f, -0.75f, 2.5f);
        child.LocalScale = new float3(6f, 1f, 10f);
        float4.CreateFromYawPitchRoll(0f, (float)(Math.PI / 12d), (float)(Math.PI / 8d), out float4 childOrientation);
        child.LocalOrientation = childOrientation;
        parent.AddChild(child);

        float4x4 expectedWorld = Multiply(child.LocalTransformMatrix, parent.LocalTransformMatrix);
        AssertMatrixApproximately(expectedWorld, child.WorldTransformMatrix);

        float4x4 recomposedWorld = ComposeWorldFromDecomposedProperties(child);
        float3 localProbePoint = new float3(0.5f, 0.5f, 0.5f);
        float3 expectedWorldPoint = TransformPoint(localProbePoint, expectedWorld);
        float3 recomposedWorldPoint = TransformPoint(localProbePoint, recomposedWorld);

        AssertNotApproximately(expectedWorldPoint, recomposedWorldPoint);
    }

    /// <summary>
    /// Initializes the minimal core services required by world-transform matrix tests.
    /// </summary>
    /// <returns>Initialized test core.</returns>
    static Core InitializeCore() {
        Core core = new Core();
        core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        return core;
    }

    /// <summary>
    /// Composes one world matrix from the entity's decomposed world-space position, orientation, and scale values using the legacy renderer path.
    /// </summary>
    /// <param name="entity">Entity whose decomposed world properties should be recomposed.</param>
    /// <returns>Recomposed affine world matrix.</returns>
    static float4x4 ComposeWorldFromDecomposedProperties(Entity entity) {
        float4x4 rotation;
        float4 orientation = entity.Orientation;
        float4x4.CreateFromQuaternion(ref orientation, out rotation);
        float4x4 size;
        float3 scale = entity.Scale;
        float4x4.CreateScale(scale.X, scale.Y, scale.Z, out size);
        float4x4 scaleRotation;
        float4x4.Multiply(ref size, ref rotation, out scaleRotation);
        float4x4 translation;
        float3 position = entity.Position;
        float4x4.CreateTranslation(ref position, out translation);
        float4x4.Multiply(ref scaleRotation, ref translation, out float4x4 world);
        return world;
    }

    /// <summary>
    /// Multiplies two matrices using the engine row-vector convention.
    /// </summary>
    /// <param name="left">Left matrix operand.</param>
    /// <param name="right">Right matrix operand.</param>
    /// <returns>Matrix product.</returns>
    static float4x4 Multiply(float4x4 left, float4x4 right) {
        float4x4.Multiply(ref left, ref right, out float4x4 result);
        return result;
    }

    /// <summary>
    /// Multiplies one point by one affine matrix using the engine row-vector convention.
    /// </summary>
    /// <param name="position">Position to transform.</param>
    /// <param name="matrix">Matrix to apply.</param>
    /// <returns>Transformed position.</returns>
    static float3 TransformPoint(float3 position, float4x4 matrix) {
        return new float3(
            (position.X * matrix.M11) + (position.Y * matrix.M21) + (position.Z * matrix.M31) + matrix.M41,
            (position.X * matrix.M12) + (position.Y * matrix.M22) + (position.Z * matrix.M32) + matrix.M42,
            (position.X * matrix.M13) + (position.Y * matrix.M23) + (position.Z * matrix.M33) + matrix.M43);
    }

    /// <summary>
    /// Asserts two matrices match element-by-element within a small transform tolerance.
    /// </summary>
    /// <param name="expected">Expected matrix.</param>
    /// <param name="actual">Actual matrix.</param>
    static void AssertMatrixApproximately(float4x4 expected, float4x4 actual) {
        Assert.Equal(expected.M11, actual.M11, 5);
        Assert.Equal(expected.M12, actual.M12, 5);
        Assert.Equal(expected.M13, actual.M13, 5);
        Assert.Equal(expected.M14, actual.M14, 5);
        Assert.Equal(expected.M21, actual.M21, 5);
        Assert.Equal(expected.M22, actual.M22, 5);
        Assert.Equal(expected.M23, actual.M23, 5);
        Assert.Equal(expected.M24, actual.M24, 5);
        Assert.Equal(expected.M31, actual.M31, 5);
        Assert.Equal(expected.M32, actual.M32, 5);
        Assert.Equal(expected.M33, actual.M33, 5);
        Assert.Equal(expected.M34, actual.M34, 5);
        Assert.Equal(expected.M41, actual.M41, 5);
        Assert.Equal(expected.M42, actual.M42, 5);
        Assert.Equal(expected.M43, actual.M43, 5);
        Assert.Equal(expected.M44, actual.M44, 5);
    }

    /// <summary>
    /// Asserts two transformed positions differ enough to prove recomposing from decomposed world properties loses information.
    /// </summary>
    /// <param name="expected">Reference transformed position.</param>
    /// <param name="actual">Recomposed transformed position.</param>
    static void AssertNotApproximately(float3 expected, float3 actual) {
        bool xMatches = Math.Abs(expected.X - actual.X) < 0.0001f;
        bool yMatches = Math.Abs(expected.Y - actual.Y) < 0.0001f;
        bool zMatches = Math.Abs(expected.Z - actual.Z) < 0.0001f;

        Assert.False(xMatches && yMatches && zMatches);
    }
}

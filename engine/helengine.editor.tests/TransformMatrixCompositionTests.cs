namespace helengine.editor.tests;

/// <summary>
/// Verifies affine world matrices follow the engine row-vector transform convention.
/// </summary>
public sealed class TransformMatrixCompositionTests {
    /// <summary>
    /// Ensures composing scale, rotation, and translation into one world matrix matches sequential row-vector application.
    /// </summary>
    [Fact]
    public void World_matrix_composition_uses_scale_then_rotation_then_translation() {
        float3 scaleVector = new float3(2f, 1f, 3f);
        float3 translationVector = new float3(10f, 20f, 30f);
        float3 rotationAxis = new float3(0f, 1f, 0f);
        float3 localPoint = new float3(1f, 0.5f, 0.25f);

        float4.CreateFromAxisAngle(ref rotationAxis, MathF.PI / 2f, out float4 orientation);

        float4x4.CreateScale(scaleVector.X, scaleVector.Y, scaleVector.Z, out float4x4 scale);
        float4x4.CreateFromQuaternion(ref orientation, out float4x4 rotation);
        float4x4.CreateTranslation(ref translationVector, out float4x4 translation);

        float3 sequential = TransformPoint(localPoint, scale);
        sequential = TransformPoint(sequential, rotation);
        sequential = TransformPoint(sequential, translation);

        float4x4.Multiply(ref scale, ref rotation, out float4x4 scaleRotation);
        float4x4.Multiply(ref scaleRotation, ref translation, out float4x4 world);

        float4x4.Multiply(ref rotation, ref scale, out float4x4 rotationScale);
        float4x4.Multiply(ref rotationScale, ref translation, out float4x4 incorrectWorld);

        float3 composed = TransformPoint(localPoint, world);
        float3 incorrectComposed = TransformPoint(localPoint, incorrectWorld);

        AssertApproximately(sequential, composed);
        AssertNotApproximately(sequential, incorrectComposed);
    }

    /// <summary>
    /// Multiplies one position by one affine matrix using the engine row-vector convention.
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
    /// Asserts two transformed positions are approximately equal.
    /// </summary>
    /// <param name="expected">Expected transformed position.</param>
    /// <param name="actual">Actual transformed position.</param>
    static void AssertApproximately(float3 expected, float3 actual) {
        Assert.True(Math.Abs(expected.X - actual.X) < 0.0001f);
        Assert.True(Math.Abs(expected.Y - actual.Y) < 0.0001f);
        Assert.True(Math.Abs(expected.Z - actual.Z) < 0.0001f);
    }

    /// <summary>
    /// Asserts two transformed positions differ enough to prove the order matters for non-uniform scales.
    /// </summary>
    /// <param name="expected">Reference transformed position.</param>
    /// <param name="actual">Transformed position built with an incorrect matrix order.</param>
    static void AssertNotApproximately(float3 expected, float3 actual) {
        bool xMatches = Math.Abs(expected.X - actual.X) < 0.0001f;
        bool yMatches = Math.Abs(expected.Y - actual.Y) < 0.0001f;
        bool zMatches = Math.Abs(expected.Z - actual.Z) < 0.0001f;

        Assert.False(xMatches && yMatches && zMatches);
    }
}

using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the native matrix helpers used by generated runtime code.
    /// </summary>
    public sealed class Float4x4Tests {
        /// <summary>
        /// Ensures inverse-transpose math works for a simple non-uniform scale matrix.
        /// </summary>
        [Fact]
        public void InverseTranspose_WhenAppliedToScaleMatrix_ProducesReciprocalDiagonal() {
            float4x4 matrix;
            float4x4.CreateScale(2f, 3f, 4f, out matrix);

            float4x4.InverseTranspose(ref matrix, out float4x4 result);

            Assert.True(Math.Abs((double)(result.M11 - 0.5f)) < 0.0001f);
            Assert.True(Math.Abs((double)(result.M22 - (1f / 3f))) < 0.0001f);
            Assert.True(Math.Abs((double)(result.M33 - 0.25f)) < 0.0001f);
            Assert.True(Math.Abs((double)(result.M44 - 1f)) < 0.0001f);

            Assert.True(Math.Abs((double)(result.M12)) < 0.0001f);
            Assert.True(Math.Abs((double)(result.M13)) < 0.0001f);
            Assert.True(Math.Abs((double)(result.M14)) < 0.0001f);
            Assert.True(Math.Abs((double)(result.M21)) < 0.0001f);
            Assert.True(Math.Abs((double)(result.M23)) < 0.0001f);
            Assert.True(Math.Abs((double)(result.M24)) < 0.0001f);
            Assert.True(Math.Abs((double)(result.M31)) < 0.0001f);
            Assert.True(Math.Abs((double)(result.M32)) < 0.0001f);
            Assert.True(Math.Abs((double)(result.M34)) < 0.0001f);
            Assert.True(Math.Abs((double)(result.M41)) < 0.0001f);
            Assert.True(Math.Abs((double)(result.M42)) < 0.0001f);
            Assert.True(Math.Abs((double)(result.M43)) < 0.0001f);
        }

        /// <summary>
        /// Ensures inverse-transpose remains mathematically correct for affine transforms after removing the heap-backed augmented matrix.
        /// </summary>
        [Fact]
        public void InverseTranspose_WhenTransposedBackAndMultipliedWithAffineMatrix_ProducesIdentity() {
            float4x4 scale;
            float4x4 translation;
            float4x4 matrix;
            float4x4.CreateScale(2f, 3f, 4f, out scale);
            float4x4.CreateTranslation(5f, 6f, 7f, out translation);
            float4x4.Multiply(ref scale, ref translation, out matrix);

            float4x4.InverseTranspose(ref matrix, out float4x4 inverseTranspose);
            float4x4.Transpose(ref inverseTranspose, out float4x4 inverse);
            float4x4.Multiply(ref matrix, ref inverse, out float4x4 identity);

            AssertIdentity(identity);
        }

        /// <summary>
        /// Asserts that a matrix is close enough to the identity matrix for inverse tests.
        /// </summary>
        /// <param name="matrix">Matrix expected to be identity.</param>
        static void AssertIdentity(float4x4 matrix) {
            Assert.True(Math.Abs((double)(matrix.M11 - 1f)) < 0.0001f);
            Assert.True(Math.Abs((double)(matrix.M22 - 1f)) < 0.0001f);
            Assert.True(Math.Abs((double)(matrix.M33 - 1f)) < 0.0001f);
            Assert.True(Math.Abs((double)(matrix.M44 - 1f)) < 0.0001f);
            Assert.True(Math.Abs((double)(matrix.M12)) < 0.0001f);
            Assert.True(Math.Abs((double)(matrix.M13)) < 0.0001f);
            Assert.True(Math.Abs((double)(matrix.M14)) < 0.0001f);
            Assert.True(Math.Abs((double)(matrix.M21)) < 0.0001f);
            Assert.True(Math.Abs((double)(matrix.M23)) < 0.0001f);
            Assert.True(Math.Abs((double)(matrix.M24)) < 0.0001f);
            Assert.True(Math.Abs((double)(matrix.M31)) < 0.0001f);
            Assert.True(Math.Abs((double)(matrix.M32)) < 0.0001f);
            Assert.True(Math.Abs((double)(matrix.M34)) < 0.0001f);
            Assert.True(Math.Abs((double)(matrix.M41)) < 0.0001f);
            Assert.True(Math.Abs((double)(matrix.M42)) < 0.0001f);
            Assert.True(Math.Abs((double)(matrix.M43)) < 0.0001f);
        }
    }
}

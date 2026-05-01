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

            Assert.True(Math.Abs(result.M11 - 0.5f) < 0.0001f);
            Assert.True(Math.Abs(result.M22 - (1f / 3f)) < 0.0001f);
            Assert.True(Math.Abs(result.M33 - 0.25f) < 0.0001f);
            Assert.True(Math.Abs(result.M44 - 1f) < 0.0001f);

            Assert.True(Math.Abs(result.M12) < 0.0001f);
            Assert.True(Math.Abs(result.M13) < 0.0001f);
            Assert.True(Math.Abs(result.M14) < 0.0001f);
            Assert.True(Math.Abs(result.M21) < 0.0001f);
            Assert.True(Math.Abs(result.M23) < 0.0001f);
            Assert.True(Math.Abs(result.M24) < 0.0001f);
            Assert.True(Math.Abs(result.M31) < 0.0001f);
            Assert.True(Math.Abs(result.M32) < 0.0001f);
            Assert.True(Math.Abs(result.M34) < 0.0001f);
            Assert.True(Math.Abs(result.M41) < 0.0001f);
            Assert.True(Math.Abs(result.M42) < 0.0001f);
            Assert.True(Math.Abs(result.M43) < 0.0001f);
        }
    }
}

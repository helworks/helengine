using helengine;
using Xunit;

namespace helengine.core.tests.model {
    /// <summary>
    /// Verifies the authored camera basis used by platform view adapters.
    /// </summary>
    public sealed class Float4CameraForwardTests {
        /// <summary>
        /// Confirms an identity authored camera faces the canonical negative-Z direction.
        /// </summary>
        [Fact]
        public void RotateVector_identity_preserves_authored_negative_z_forward() {
            float3 forward = float4.RotateVector(new float3(0f, 0f, -1f), float4.Identity);

            Assert.Equal((double)(0f),(double)(forward.X), 5);
            Assert.Equal((double)(0f),(double)(forward.Y), 5);
            Assert.Equal((double)(-1f),(double)(forward.Z), 5);
        }

        /// <summary>
        /// Confirms a quarter positive yaw rotates the same canonical forward vector toward negative X.
        /// </summary>
        [Fact]
        public void RotateVector_positive_yaw_rotates_authored_forward_toward_negative_x() {
            float4.CreateFromYawPitchRoll(MathF.PI * 0.5f, 0f, 0f, out float4 orientation);
            float3 forward = float4.RotateVector(new float3(0f, 0f, -1f), orientation);

            Assert.Equal((double)(-1f),(double)(forward.X), 5);
            Assert.Equal((double)(0f),(double)(forward.Y), 5);
            Assert.Equal((double)(0f),(double)(forward.Z), 5);
        }
    }
}

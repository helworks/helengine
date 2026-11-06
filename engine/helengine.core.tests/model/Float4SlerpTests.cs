using helengine;
using Xunit;

namespace helengine.core.tests.model {
    /// <summary>
    /// Verifies <see cref="float4.Slerp"/>: constant angular speed, the shorter-arc flip for negative dot products and
    /// the normalized-lerp fallback for nearly parallel quaternions.
    /// </summary>
    public sealed class Float4SlerpTests {
        /// <summary>
        /// A quarter of the way from identity to a 90 degree turn about Z is a 22.5 degree turn.
        /// </summary>
        [Fact]
        public void Slerp_movesAtConstantAngularSpeed() {
            float4 result = float4.Slerp(float4.Identity, AboutZ(Math.PI / 2), 0.25);

            AssertQuaternion(AboutZ(Math.PI / 8), result);
        }

        /// <summary>
        /// The negated end quaternion describes the same rotation; slerp flips it and takes the shorter arc, so the
        /// result matches the unflipped interpolation.
        /// </summary>
        [Fact]
        public void Slerp_negativeDot_takesShorterArc() {
            float4 end = AboutZ(Math.PI / 2);
            float4 negatedEnd = new float4(-end.X, -end.Y, -end.Z, -end.W);

            float4 result = float4.Slerp(float4.Identity, negatedEnd, 0.5);

            AssertQuaternion(AboutZ(Math.PI / 4), result);
        }

        /// <summary>
        /// Nearly parallel quaternions use the normalized lerp, which stays finite and matches <see cref="float4.Lerp"/>.
        /// </summary>
        [Fact]
        public void Slerp_nearlyParallel_fallsBackToLerp() {
            float4 start = AboutZ(0.001);
            float4 end = AboutZ(0.002);

            float4 result = float4.Slerp(start, end, 0.5);

            AssertQuaternion(float4.Lerp(start, end, 0.5), result);
            AssertQuaternion(AboutZ(0.0015), result);
        }

        /// <summary>
        /// Returns the rotation of an angle about the Z axis.
        /// </summary>
        /// <param name="angle">Angle in radians.</param>
        /// <returns>The quaternion.</returns>
        static float4 AboutZ(double angle) {
            return new float4(0, 0, Math.Sin(angle / 2), Math.Cos(angle / 2));
        }

        /// <summary>
        /// Asserts two quaternions match component by component.
        /// </summary>
        /// <param name="expected">Expected quaternion.</param>
        /// <param name="actual">Actual quaternion.</param>
        static void AssertQuaternion(float4 expected, float4 actual) {
            Assert.Equal((double)expected.X, (double)actual.X, 6);
            Assert.Equal((double)expected.Y, (double)actual.Y, 6);
            Assert.Equal((double)expected.Z, (double)actual.Z, 6);
            Assert.Equal((double)expected.W, (double)actual.W, 6);
        }
    }
}

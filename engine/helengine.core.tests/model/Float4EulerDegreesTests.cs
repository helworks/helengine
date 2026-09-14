using helengine;
using Xunit;

namespace helengine.core.tests.model {
    /// <summary>
    /// Verifies that the quaternion-owned Euler conversion reproduces the pitch/yaw/roll decomposition the editor
    /// inspector previously hand-rolled, including the clamped gimbal-lock branch.
    /// </summary>
    public sealed class Float4EulerDegreesTests {
        /// <summary>
        /// Confirms the identity quaternion decomposes to zero pitch, yaw and roll.
        /// </summary>
        [Fact]
        public void ToEulerDegrees_for_identity_orientation_returns_zero_angles() {
            float4 identity = float4.Identity;

            identity.ToEulerDegrees(out double pitch, out double yaw, out double roll);

            Assert.Equal(0d, pitch, 6);
            Assert.Equal(0d, yaw, 6);
            Assert.Equal(0d, roll, 6);
        }

        /// <summary>
        /// Confirms the conversion matches the reference decomposition across a spread of authored orientations.
        /// </summary>
        /// <param name="yawDegrees">Yaw angle used to build the sample orientation.</param>
        /// <param name="pitchDegrees">Pitch angle used to build the sample orientation.</param>
        /// <param name="rollDegrees">Roll angle used to build the sample orientation.</param>
        [Theory]
        [InlineData(0d, 0d, 0d)]
        [InlineData(30d, 0d, 0d)]
        [InlineData(0d, 30d, 0d)]
        [InlineData(0d, 0d, 30d)]
        [InlineData(45d, -20d, 10d)]
        [InlineData(-135d, 12.5d, -70d)]
        [InlineData(179d, 44d, 91d)]
        [InlineData(-89d, -89d, 89d)]
        [InlineData(120d, 60d, -150d)]
        [InlineData(15d, 89.9d, 15d)]
        public void ToEulerDegrees_matches_reference_decomposition(double yawDegrees, double pitchDegrees, double rollDegrees) {
            float4 orientation = BuildOrientation(yawDegrees, pitchDegrees, rollDegrees);

            orientation.ToEulerDegrees(out double pitch, out double yaw, out double roll);
            ReferenceOrientationDegrees(orientation, out double expectedPitch, out double expectedYaw, out double expectedRoll);

            Assert.Equal(expectedPitch, pitch, 9);
            Assert.Equal(expectedYaw, yaw, 9);
            Assert.Equal(expectedRoll, roll, 9);
        }

        /// <summary>
        /// Confirms a saturated pitch term clamps to the gimbal-lock pole instead of feeding an out-of-range value
        /// to the arc sine and producing a NaN angle.
        /// </summary>
        /// <param name="x">X component of the saturated sample orientation.</param>
        /// <param name="w">W component of the saturated sample orientation.</param>
        /// <param name="expectedPitchDegrees">Pole pitch the conversion is expected to clamp to.</param>
        [Theory]
        [InlineData(1f, 1f, 90d)]
        [InlineData(-1f, 1f, -90d)]
        public void ToEulerDegrees_when_pitch_term_saturates_clamps_to_the_pole(float x, float w, double expectedPitchDegrees) {
            float4 orientation = new float4(x, 0f, 0f, w);

            orientation.ToEulerDegrees(out double pitch, out double yaw, out double roll);

            Assert.Equal(expectedPitchDegrees, pitch, 9);
            Assert.False(double.IsNaN(yaw));
            Assert.False(double.IsNaN(roll));
        }

        /// <summary>
        /// Confirms a single-axis rotation reports its angle on the matching axis and leaves the other two at zero.
        /// </summary>
        /// <param name="axis">Index of the rotation axis: 0 for pitch, 1 for yaw, 2 for roll.</param>
        /// <param name="angleDegrees">Rotation angle in degrees.</param>
        [Theory]
        [InlineData(0, 22d)]
        [InlineData(0, -37d)]
        [InlineData(1, 22d)]
        [InlineData(1, -37d)]
        [InlineData(2, 22d)]
        [InlineData(2, -37d)]
        public void ToEulerDegrees_for_single_axis_rotation_reports_only_that_axis(int axis, double angleDegrees) {
            double halfAngle = angleDegrees * (Math.PI / 180d) * 0.5d;
            float sin = (float)Math.Sin(halfAngle);
            float cos = (float)Math.Cos(halfAngle);
            float4 orientation = new float4(0f, 0f, 0f, cos);
            if (axis == 0) {
                orientation.X = sin;
            } else if (axis == 1) {
                orientation.Y = sin;
            } else {
                orientation.Z = sin;
            }

            orientation.ToEulerDegrees(out double pitch, out double yaw, out double roll);

            if (axis == 0) {
                Assert.Equal(angleDegrees, pitch, 4);
                Assert.Equal(0d, yaw, 4);
                Assert.Equal(0d, roll, 4);
            } else if (axis == 1) {
                Assert.Equal(0d, pitch, 4);
                Assert.Equal(angleDegrees, yaw, 4);
                Assert.Equal(0d, roll, 4);
            } else {
                Assert.Equal(0d, pitch, 4);
                Assert.Equal(0d, yaw, 4);
                Assert.Equal(angleDegrees, roll, 4);
            }
        }

        /// <summary>
        /// Builds a normalized quaternion from yaw/pitch/roll expressed in degrees.
        /// </summary>
        /// <param name="yawDegrees">Yaw angle in degrees.</param>
        /// <param name="pitchDegrees">Pitch angle in degrees.</param>
        /// <param name="rollDegrees">Roll angle in degrees.</param>
        /// <returns>Quaternion describing the requested orientation.</returns>
        static float4 BuildOrientation(double yawDegrees, double pitchDegrees, double rollDegrees) {
            double toRadians = Math.PI / 180d;
            float4.CreateFromYawPitchRoll(
                (float)(yawDegrees * toRadians),
                (float)(pitchDegrees * toRadians),
                (float)(rollDegrees * toRadians),
                out float4 orientation);
            orientation.Normalize();
            return orientation;
        }

        /// <summary>
        /// Reproduces the decomposition formula the editor inspector used before the conversion moved onto the quaternion type.
        /// </summary>
        /// <param name="orientation">Quaternion orientation to decompose.</param>
        /// <param name="pitch">Pitch angle in degrees.</param>
        /// <param name="yaw">Yaw angle in degrees.</param>
        /// <param name="roll">Roll angle in degrees.</param>
        static void ReferenceOrientationDegrees(float4 orientation, out double pitch, out double yaw, out double roll) {
            double x = orientation.X;
            double y = orientation.Y;
            double z = orientation.Z;
            double w = orientation.W;

            double sinPitch = 2.0 * (w * x - y * z);
            if (Math.Abs(sinPitch) >= 1.0) {
                pitch = Math.CopySign(Math.PI / 2.0, sinPitch);
            } else {
                pitch = Math.Asin(sinPitch);
            }

            double sinYaw = 2.0 * (w * y + x * z);
            double cosYaw = 1.0 - 2.0 * (x * x + y * y);
            yaw = Math.Atan2(sinYaw, cosYaw);

            double sinRoll = 2.0 * (w * z + x * y);
            double cosRoll = 1.0 - 2.0 * (y * y + z * z);
            roll = Math.Atan2(sinRoll, cosRoll);

            pitch = pitch * (180.0 / Math.PI);
            yaw = yaw * (180.0 / Math.PI);
            roll = roll * (180.0 / Math.PI);
        }
    }
}

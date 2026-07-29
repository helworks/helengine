namespace helengine {
    /// <summary>
    /// Verifies the observable vector and quaternion behavior used by the physics solver.
    /// </summary>
    public sealed class PhysicsMathTests {
        /// <summary>
        /// Verifies that the right-handed cross product of the positive X and Y unit axes is the positive Z unit axis.
        /// </summary>
        [Fact]
        public void Cross_WithUnitAxes_ReturnsPositiveZAxis() {
            PhysicsVector3 result = PhysicsVector3.Cross(PhysicsVector3.UnitX, PhysicsVector3.UnitY);

            Assert.Equal(0f, result.X.ToFloat());
            Assert.Equal(0f, result.Y.ToFloat());
            Assert.Equal(1f, result.Z.ToFloat());
        }

        /// <summary>
        /// Verifies that a positive quarter turn about the Z axis rotates the positive X unit axis onto the positive Y unit axis.
        /// </summary>
        [Fact]
        public void Rotate_WithQuarterTurnAroundZ_RotatesPositiveXToPositiveY() {
            PhysicsQuaternion rotation = PhysicsQuaternion.CreateFromAxisAngle(
                PhysicsVector3.UnitZ,
                PhysicsScalar.FromFloat((float)(Math.PI * 0.5d)));

            PhysicsVector3 result = rotation.Rotate(PhysicsVector3.UnitX);

            Assert.InRange(result.X.ToFloat(), -0.0001f, 0.0001f);
            Assert.InRange(result.Y.ToFloat(), 0.9999f, 1.0001f);
            Assert.InRange(result.Z.ToFloat(), -0.0001f, 0.0001f);
        }
    }
}

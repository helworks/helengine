namespace helengine.physics3d.tests {
    /// <summary>
    /// Verifies kinematic pose deltas can be converted into the world-space velocities required by runtime physics integrations.
    /// </summary>
    public sealed class KinematicPoseVelocityResolver3DTests {
        /// <summary>
        /// Ensures one pure translation resolves the expected linear velocity while leaving angular velocity at zero.
        /// </summary>
        [Fact]
        public void ResolveMotion_WithTranslatedPose_ReturnsExpectedLinearVelocityAndZeroAngularVelocity() {
            float3 previousPosition = new float3(1f, 2f, 3f);
            float3 currentPosition = new float3(3f, 1f, 7f);
            float4 previousOrientation = float4.Identity;
            float4 currentOrientation = float4.Identity;

            KinematicPoseVelocityResolver3D.ResolveMotion(
                previousPosition,
                previousOrientation,
                currentPosition,
                currentOrientation,
                0.5d,
                out float3 linearVelocity,
                out float3 angularVelocity);

            Assert.Equal(new float3(4f, -2f, 8f), linearVelocity);
            Assert.Equal(float3.Zero, angularVelocity);
        }

        /// <summary>
        /// Ensures one pure rotation resolves the expected angular velocity while leaving linear velocity at zero.
        /// </summary>
        [Fact]
        public void ResolveMotion_WithRotatedPose_ReturnsExpectedAngularVelocityAndZeroLinearVelocity() {
            float4 currentOrientation;
            float4.CreateFromAxisAngle(new float3(0f, 0f, 1f), (float)(Math.PI * 0.5d), out currentOrientation);

            KinematicPoseVelocityResolver3D.ResolveMotion(
                float3.Zero,
                float4.Identity,
                float3.Zero,
                currentOrientation,
                0.25d,
                out float3 linearVelocity,
                out float3 angularVelocity);

            Assert.Equal(float3.Zero, linearVelocity);
            Assert.InRange(angularVelocity.X, -0.0001f, 0.0001f);
            Assert.InRange(angularVelocity.Y, -0.0001f, 0.0001f);
            Assert.InRange(angularVelocity.Z, 6.2830f, 6.2834f);
        }
    }
}

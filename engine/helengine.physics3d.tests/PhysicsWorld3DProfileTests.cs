namespace helengine.physics3d.tests {
    /// <summary>
    /// Verifies the built-in 3D physics runtime profiles expose the expected default capabilities.
    /// </summary>
    public class PhysicsWorld3DProfileTests {
        /// <summary>
        /// Ensures the medium profile defaults to dynamic primitive bodies with cooked static mesh world collision enabled.
        /// </summary>
        [Fact]
        public void CreateMedium_DefaultsToPrimitiveBodiesAndStaticMeshQueries() {
            PhysicsWorld3DProfile profile = PhysicsWorld3DProfile.CreateMedium();

            Assert.Equal(BroadphaseKind3D.UniformGrid, profile.DefaultBroadphaseKind);
            Assert.True(profile.AllowStaticMeshCollision);
            Assert.True(profile.AllowDynamicBodies);
            Assert.False(profile.AllowJoints);
            Assert.False(profile.AllowContinuousCollisionDetection);
            Assert.Equal(8, profile.SolverIterations);
        }
    }
}

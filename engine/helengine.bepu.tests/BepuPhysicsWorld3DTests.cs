namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies the new BEPU-backed world package can be constructed by tests.
    /// </summary>
    public sealed class BepuPhysicsWorld3DTests {
        /// <summary>
        /// Ensures the BEPU-backed physics world type can be instantiated.
        /// </summary>
        [Fact]
        public void CreateDefault_ConstructsWorldInstance() {
            BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();

            Assert.NotNull(world);
        }
    }
}

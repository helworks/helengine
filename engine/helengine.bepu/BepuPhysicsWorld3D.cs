namespace helengine {
    /// <summary>
    /// Hosts the BEPU-backed rigid-body runtime used by supported Helengine scenes.
    /// </summary>
    public sealed class BepuPhysicsWorld3D {
        /// <summary>
        /// Creates one default BEPU-backed physics world.
        /// </summary>
        /// <returns>Constructed physics world instance.</returns>
        public static BepuPhysicsWorld3D CreateDefault() {
            return new BepuPhysicsWorld3D();
        }
    }
}

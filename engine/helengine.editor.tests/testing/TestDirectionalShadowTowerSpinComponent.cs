namespace helengine.editor.tests.testing {
    /// <summary>
    /// Minimal surrogate used to deserialize the city tower-spin scene component inside editor scene-load tests.
    /// </summary>
    internal sealed class TestDirectionalShadowTowerSpinComponent : UpdateComponent {
        /// <summary>
        /// Gets or sets the persisted base yaw offset.
        /// </summary>
        public float BaseYawRadians { get; set; }

        /// <summary>
        /// Gets or sets the persisted angular speed.
        /// </summary>
        public float AngularSpeedRadians { get; set; }
    }
}

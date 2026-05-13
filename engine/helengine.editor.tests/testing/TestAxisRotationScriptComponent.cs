namespace helengine.editor.tests.testing {
    /// <summary>
    /// Minimal surrogate used to exercise gameplay axis-rotation script packaging without depending on the external city assembly.
    /// </summary>
    public sealed class TestAxisRotationScriptComponent : UpdateComponent {
        /// <summary>
        /// Gets or sets the authored local rotation axis.
        /// </summary>
        public float3 Axis { get; set; }

        /// <summary>
        /// Gets or sets the authored angular speed in radians per second.
        /// </summary>
        public float AngularSpeedRadiansPerSecond { get; set; }
    }
}

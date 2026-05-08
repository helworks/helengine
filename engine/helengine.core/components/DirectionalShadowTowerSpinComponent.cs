namespace helengine {
    /// <summary>
    /// Rotates the parent entity around the local Y axis for directional-shadow showcase scenes.
    /// </summary>
    public sealed class DirectionalShadowTowerSpinComponent : UpdateComponent {
        /// <summary>
        /// Stable serialized component type id used by packaged runtime scenes.
        /// </summary>
        public const string SerializedComponentTypeId = "helengine.DirectionalShadowTowerSpinComponent";

        /// <summary>
        /// Gets or sets the base yaw offset in radians.
        /// </summary>
        public float BaseYawRadians { get; set; }

        /// <summary>
        /// Gets or sets the angular speed in radians per second.
        /// </summary>
        public float AngularSpeedRadians { get; set; }

        /// <summary>
        /// Updates the parent orientation from deterministic absolute runtime time.
        /// </summary>
        public override void Update() {
            base.Update();

            double yawRadians = BaseYawRadians + (AngularSpeedRadians * Core.Instance.TotalElapsedSeconds);
            float4 orientation;
            float4.CreateFromYawPitchRoll((float)yawRadians, 0f, 0f, out orientation);
            orientation.Normalize();
            Parent.LocalOrientation = orientation;
        }
    }
}

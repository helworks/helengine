namespace helengine {
    /// <summary>
    /// Rotates one showcase tower group around the local Y axis using deterministic absolute runtime time.
    /// </summary>
    public sealed class DirectionalShadowTowerSpinComponent : UpdateComponent {
        /// <summary>
        /// Stable serialized component type id used by packaged player scenes.
        /// </summary>
        public const string SerializedComponentTypeId = "helengine.DirectionalShadowTowerSpinComponent";

        /// <summary>
        /// Gets or sets the base yaw offset in radians applied before time-based rotation.
        /// </summary>
        public float BaseYawRadians { get; set; }

        /// <summary>
        /// Gets or sets the angular speed in radians per second.
        /// </summary>
        public float AngularSpeedRadians { get; set; }

        /// <summary>
        /// Evaluates the current orientation from total elapsed runtime time.
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

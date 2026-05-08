namespace helengine {
    /// <summary>
    /// Sweeps the parent entity through a narrow directional-light arc for showcase scenes.
    /// </summary>
    public sealed class DirectionalShadowSunSweepComponent : UpdateComponent {
        /// <summary>
        /// Stable serialized component type id used by packaged runtime scenes.
        /// </summary>
        public const string SerializedComponentTypeId = "helengine.DirectionalShadowSunSweepComponent";

        /// <summary>
        /// Gets or sets the minimum yaw reached by the sweep in radians.
        /// </summary>
        public float MinYawRadians { get; set; }

        /// <summary>
        /// Gets or sets the maximum yaw reached by the sweep in radians.
        /// </summary>
        public float MaxYawRadians { get; set; }

        /// <summary>
        /// Gets or sets the fixed pitch applied throughout the sweep in radians.
        /// </summary>
        public float PitchRadians { get; set; }

        /// <summary>
        /// Gets or sets the angular sweep rate in radians per second.
        /// </summary>
        public float SweepSpeedRadians { get; set; }

        /// <summary>
        /// Updates the parent orientation from deterministic absolute runtime time.
        /// </summary>
        public override void Update() {
            base.Update();

            double normalized = (Math.Sin(Core.Instance.TotalElapsedSeconds * SweepSpeedRadians) * 0.5d) + 0.5d;
            double yawRadians = MinYawRadians + ((MaxYawRadians - MinYawRadians) * normalized);
            float4 orientation;
            float4.CreateFromYawPitchRoll((float)yawRadians, PitchRadians, 0f, out orientation);
            orientation.Normalize();
            Parent.LocalOrientation = orientation;
        }
    }
}

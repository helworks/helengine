namespace gameplay.rendering {
    /// <summary>
    /// Sweeps one directional light through a narrow sun arc using a sine wave over absolute runtime time.
    /// </summary>
    public sealed class DirectionalShadowSunSweepComponent : UpdateComponent {
        /// <summary>
        /// Gets or sets the minimum yaw reached by the light sweep in radians.
        /// </summary>
        public float MinYawRadians { get; set; }

        /// <summary>
        /// Gets or sets the maximum yaw reached by the light sweep in radians.
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
        /// Evaluates the current light orientation from total elapsed runtime time.
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
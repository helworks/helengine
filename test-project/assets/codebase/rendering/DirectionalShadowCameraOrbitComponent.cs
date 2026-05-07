namespace gameplay.rendering {
    /// <summary>
    /// Keeps the showcase camera on a slow elevated orbit while always looking back toward the plaza center.
    /// </summary>
    public sealed class DirectionalShadowCameraOrbitComponent : UpdateComponent {
        /// <summary>
        /// Gets or sets the world-space orbit center.
        /// </summary>
        public float3 OrbitCenter { get; set; }

        /// <summary>
        /// Gets or sets the orbit radius in world units.
        /// </summary>
        public float OrbitRadius { get; set; }

        /// <summary>
        /// Gets or sets the vertical offset applied relative to the orbit center.
        /// </summary>
        public float OrbitHeight { get; set; }

        /// <summary>
        /// Gets or sets the base orbit angle in radians.
        /// </summary>
        public float BaseAngleRadians { get; set; }

        /// <summary>
        /// Gets or sets the angular speed in radians per second.
        /// </summary>
        public float AngularSpeedRadians { get; set; }

        /// <summary>
        /// Gets or sets the fixed downward camera pitch in radians.
        /// </summary>
        public float LookDownPitchRadians { get; set; }

        /// <summary>
        /// Evaluates the current camera orbit position and inward-facing orientation from total elapsed runtime time.
        /// </summary>
        public override void Update() {
            base.Update();

            double angleRadians = BaseAngleRadians + (AngularSpeedRadians * Core.Instance.TotalElapsedSeconds);
            double x = OrbitCenter.X + (Math.Sin(angleRadians) * OrbitRadius);
            double z = OrbitCenter.Z + (Math.Cos(angleRadians) * OrbitRadius);
            Parent.LocalPosition = new float3((float)x, OrbitCenter.Y + OrbitHeight, (float)z);

            double inwardYawRadians = Math.Atan2(OrbitCenter.X - x, -(OrbitCenter.Z - z));
            float4 orientation;
            float4.CreateFromYawPitchRoll((float)inwardYawRadians, LookDownPitchRadians, 0f, out orientation);
            orientation.Normalize();
            Parent.LocalOrientation = orientation;
        }
    }
}
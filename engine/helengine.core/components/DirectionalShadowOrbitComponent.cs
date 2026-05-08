namespace helengine {
    /// <summary>
    /// Moves one showcase entity around an authored orbit center using deterministic absolute runtime time.
    /// </summary>
    public sealed class DirectionalShadowOrbitComponent : UpdateComponent {
        /// <summary>
        /// Stable serialized component type id used by packaged player scenes.
        /// </summary>
        public const string SerializedComponentTypeId = "helengine.DirectionalShadowOrbitComponent";

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
        /// Evaluates the current orbit position and inward-facing orientation from total elapsed runtime time.
        /// </summary>
        public override void Update() {
            base.Update();

            double angleRadians = BaseAngleRadians + (AngularSpeedRadians * Core.Instance.TotalElapsedSeconds);
            double x = OrbitCenter.X + (Math.Sin(angleRadians) * OrbitRadius);
            double z = OrbitCenter.Z + (Math.Cos(angleRadians) * OrbitRadius);
            Parent.LocalPosition = new float3((float)x, OrbitCenter.Y + OrbitHeight, (float)z);

            double inwardYawRadians = Math.Atan2(OrbitCenter.X - x, -(OrbitCenter.Z - z));
            float4 orientation;
            float4.CreateFromYawPitchRoll((float)inwardYawRadians, 0f, 0f, out orientation);
            orientation.Normalize();
            Parent.LocalOrientation = orientation;
        }
    }
}

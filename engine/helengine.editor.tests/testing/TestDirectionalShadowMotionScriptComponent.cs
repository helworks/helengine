namespace helengine.editor.tests.testing {
    /// <summary>
    /// Deterministic scripted component used to verify directional-shadow packaging transforms from authored script payloads to built-in runtime components.
    /// </summary>
    public sealed class TestDirectionalShadowMotionScriptComponent : Component {
        /// <summary>
        /// Gets or sets the world-space orbit center used by orbit-based motion variants.
        /// </summary>
        public float3 OrbitCenter { get; set; }

        /// <summary>
        /// Gets or sets the orbit radius in world units used by orbit-based motion variants.
        /// </summary>
        public float OrbitRadius { get; set; }

        /// <summary>
        /// Gets or sets the orbit height offset used by orbit-based motion variants.
        /// </summary>
        public float OrbitHeight { get; set; }

        /// <summary>
        /// Gets or sets the base orbit angle in radians used by orbit-based motion variants.
        /// </summary>
        public float BaseAngleRadians { get; set; }

        /// <summary>
        /// Gets or sets the angular speed in radians per second used by time-driven motion variants.
        /// </summary>
        public float AngularSpeedRadians { get; set; }

        /// <summary>
        /// Gets or sets the fixed downward camera pitch in radians used by the camera-orbit variant.
        /// </summary>
        public float LookDownPitchRadians { get; set; }

        /// <summary>
        /// Gets or sets the minimum yaw in radians used by the sun-sweep variant.
        /// </summary>
        public float MinYawRadians { get; set; }

        /// <summary>
        /// Gets or sets the maximum yaw in radians used by the sun-sweep variant.
        /// </summary>
        public float MaxYawRadians { get; set; }

        /// <summary>
        /// Gets or sets the fixed pitch in radians used by the sun-sweep variant.
        /// </summary>
        public float PitchRadians { get; set; }

        /// <summary>
        /// Gets or sets the sweep speed in radians per second used by the sun-sweep variant.
        /// </summary>
        public float SweepSpeedRadians { get; set; }

        /// <summary>
        /// Gets or sets the base yaw offset in radians used by the tower-spin variant.
        /// </summary>
        public float BaseYawRadians { get; set; }
    }
}

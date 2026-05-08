namespace helengine.editor.tests.testing {
    /// <summary>
    /// Superset scripted component used to verify directional-shadow motion records can remap from project-authored script ids to built-in player component ids.
    /// </summary>
    public sealed class TestDirectionalShadowMotionScriptComponent : Component {
        /// <summary>
        /// Gets or sets the authored orbit center.
        /// </summary>
        public float3 OrbitCenter { get; set; }

        /// <summary>
        /// Gets or sets the authored orbit radius.
        /// </summary>
        public float OrbitRadius { get; set; }

        /// <summary>
        /// Gets or sets the authored orbit height.
        /// </summary>
        public float OrbitHeight { get; set; }

        /// <summary>
        /// Gets or sets the authored base orbit angle.
        /// </summary>
        public float BaseAngleRadians { get; set; }

        /// <summary>
        /// Gets or sets the authored orbit angular speed.
        /// </summary>
        public float AngularSpeedRadians { get; set; }

        /// <summary>
        /// Gets or sets the authored camera look-down pitch.
        /// </summary>
        public float LookDownPitchRadians { get; set; }

        /// <summary>
        /// Gets or sets the authored minimum sun yaw.
        /// </summary>
        public float MinYawRadians { get; set; }

        /// <summary>
        /// Gets or sets the authored maximum sun yaw.
        /// </summary>
        public float MaxYawRadians { get; set; }

        /// <summary>
        /// Gets or sets the authored sun pitch.
        /// </summary>
        public float PitchRadians { get; set; }

        /// <summary>
        /// Gets or sets the authored sun sweep speed.
        /// </summary>
        public float SweepSpeedRadians { get; set; }

        /// <summary>
        /// Gets or sets the authored base tower yaw.
        /// </summary>
        public float BaseYawRadians { get; set; }
    }
}

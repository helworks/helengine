namespace helengine {
    /// <summary>
    /// Represents one authored spot light in the scene.
    /// </summary>
    public class SpotLightComponent : LightComponent {
        /// <summary>
        /// Initializes one authored spot light with default cone and range values.
        /// </summary>
        public SpotLightComponent() : base(LightType.Spot) {
            Range = 10f;
            InnerConeAngleDegrees = 25f;
            OuterConeAngleDegrees = 45f;
        }

        /// <summary>
        /// Gets or sets the authored spot-light influence range in world units.
        /// </summary>
        public float Range { get; set; }

        /// <summary>
        /// Gets or sets the authored inner cone angle in degrees.
        /// </summary>
        public float InnerConeAngleDegrees { get; set; }

        /// <summary>
        /// Gets or sets the authored outer cone angle in degrees.
        /// </summary>
        public float OuterConeAngleDegrees { get; set; }
    }
}

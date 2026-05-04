namespace helengine {
    /// <summary>
    /// Represents one authored point light in the scene.
    /// </summary>
    public class PointLightComponent : LightComponent {
        /// <summary>
        /// Initializes one authored point light with default local-light parameters.
        /// </summary>
        public PointLightComponent() : base(LightType.Point) {
            Range = 10f;
        }

        /// <summary>
        /// Gets or sets the authored point-light influence range in world units.
        /// </summary>
        public float Range { get; set; }
    }
}

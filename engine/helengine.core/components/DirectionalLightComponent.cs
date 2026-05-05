namespace helengine {
    /// <summary>
    /// Represents one authored directional light in the scene.
    /// </summary>
    public class DirectionalLightComponent : LightComponent {
        /// <summary>
        /// Default directional shadow cutoff distance used by newly created lights.
        /// </summary>
        const float DefaultShadowDistance = 50f;

        /// <summary>
        /// Initializes one authored directional light with shadow-capable defaults.
        /// </summary>
        public DirectionalLightComponent() : base(LightType.Directional) {
            ShadowsEnabled = true;
            ShadowDistance = DefaultShadowDistance;
        }

        /// <summary>
        /// Gets or sets the farthest world distance that should receive shadows from this directional light.
        /// </summary>
        public float ShadowDistance { get; set; }
    }
}

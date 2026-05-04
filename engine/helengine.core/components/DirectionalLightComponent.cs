namespace helengine {
    /// <summary>
    /// Represents one authored directional light in the scene.
    /// </summary>
    public class DirectionalLightComponent : LightComponent {
        /// <summary>
        /// Initializes one authored directional light with shadow-capable defaults.
        /// </summary>
        public DirectionalLightComponent() : base(LightType.Directional) {
            ShadowsEnabled = true;
        }
    }
}

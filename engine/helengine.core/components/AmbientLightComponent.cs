namespace helengine {
    /// <summary>
    /// Represents one authored ambient light that contributes global 3D material illumination without directional falloff.
    /// </summary>
    public class AmbientLightComponent : LightComponent {
        /// <summary>
        /// Initializes one authored ambient light with the default non-shadowed global-light profile.
        /// </summary>
        public AmbientLightComponent() : base(LightType.Ambient) {
        }
    }
}

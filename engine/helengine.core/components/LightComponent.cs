namespace helengine {
    /// <summary>
    /// Shared base component for authored scene lights consumed by render extraction and backend planning.
    /// Directional and spot-light orientation is derived from the owning entity forward axis defined by <see cref="LightDirectionUtility"/>.
    /// </summary>
    public class LightComponent : Component {
        /// <summary>
        /// Initializes one authored light component for the supplied light family.
        /// </summary>
        /// <param name="lightType">Stable light family represented by the component.</param>
        protected LightComponent(LightType lightType) {
            LightType = lightType;
            Color = new float4(1f, 1f, 1f, 1f);
            Intensity = 1f;
            ShadowStrength = 1f;
            ShadowMapMode = ShadowMapMode.Auto;
        }

        /// <summary>
        /// Gets the stable light family represented by the component.
        /// </summary>
        public LightType LightType { get; }

        /// <summary>
        /// Gets or sets the light color stored as linear RGBA.
        /// </summary>
        public float4 Color { get; set; }

        /// <summary>
        /// Gets or sets the authored light intensity multiplier.
        /// </summary>
        public float Intensity { get; set; }

        /// <summary>
        /// Gets or sets whether the light currently requests shadow rendering.
        /// </summary>
        public bool ShadowsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the authored shadow-map participation mode for the light.
        /// </summary>
        public ShadowMapMode ShadowMapMode { get; set; }

        /// <summary>
        /// Gets or sets the authored shadow-strength multiplier applied by the backend.
        /// </summary>
        public float ShadowStrength { get; set; }
    }
}

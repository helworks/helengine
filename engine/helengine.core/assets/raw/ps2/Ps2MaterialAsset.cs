namespace helengine {
    /// <summary>
    /// Stores one PS2-native cooked material payload selected by the PS2 builder.
    /// </summary>
    public class Ps2MaterialAsset : Asset {
        /// <summary>
        /// Gets or sets the PS2 renderer family that owns this material payload.
        /// </summary>
        public string RendererFamilyId;

        /// <summary>
        /// Gets or sets the lighting mode selected by the PS2 schema.
        /// </summary>
        public Ps2MaterialLightingMode LightingMode;

        /// <summary>
        /// Gets or sets the alpha behavior selected by the PS2 schema.
        /// </summary>
        public Ps2MaterialAlphaMode AlphaMode;

        /// <summary>
        /// Gets or sets the coarse PS2 render class used for frame routing.
        /// </summary>
        public Ps2RenderClass RenderClass;

        /// <summary>
        /// Gets or sets the cooked texture path consumed by the PS2 runtime.
        /// </summary>
        public string TextureRelativePath;

        /// <summary>
        /// Gets or sets whether the material should render two-sided geometry.
        /// </summary>
        public bool DoubleSided;

        /// <summary>
        /// Gets or sets whether the material should contribute to PS2 shadow passes.
        /// </summary>
        public bool CastShadows;

        /// <summary>
        /// Gets or sets whether vertex color should modulate the final output.
        /// </summary>
        public bool UseVertexColor;

        /// <summary>
        /// Gets or sets whether the author explicitly allowed an expensive path for this material.
        /// </summary>
        public bool ExpensiveModeAllowed;

        /// <summary>
        /// Gets or sets the roughness parameter used by the fixed PS2 lit shaders.
        /// </summary>
        public float Roughness;

        /// <summary>
        /// Gets or sets the specular strength parameter used by the fixed PS2 lit shaders.
        /// </summary>
        public float SpecularStrength;

        /// <summary>
        /// Gets or sets the emissive contribution used by the fixed PS2 showcase shader.
        /// </summary>
        public float EmissiveStrength;
    }
}

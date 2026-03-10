namespace helengine {
    /// <summary>
    /// Base type for runtime material resources.
    /// </summary>
    public abstract class RuntimeMaterial : RuntimeData {
        /// <summary>
        /// Gets or sets the texture sampled by the material when the active shader expects one.
        /// </summary>
        public RuntimeTexture Texture { get; set; }
    }
}

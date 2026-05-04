namespace helengine {
    /// <summary>
    /// Represents a GPU render target that cameras can draw into and later sample as a texture.
    /// </summary>
    public abstract class RenderTarget : RuntimeTexture {
        /// <summary>
        /// Gets or sets whether the render target can be sampled as a texture by later passes.
        /// </summary>
        public bool CanSampleAsTexture { get; set; }

        /// <summary>
        /// Gets or sets whether the render target owns a depth buffer that later passes may depend on.
        /// </summary>
        public bool HasDepthBuffer { get; set; }
    }
}

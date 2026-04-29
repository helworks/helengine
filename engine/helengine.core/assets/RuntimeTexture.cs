namespace helengine {
    /// <summary>
    /// Represents a GPU-resident texture with dimensions.
    /// </summary>
    public abstract class RuntimeTexture : RuntimeData {
        /// <summary>
        /// Gets or sets the width of the texture in pixels.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the height of the texture in pixels.
        /// </summary>
        public int Height { get; set; }
    }
}

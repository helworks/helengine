namespace helengine {
    /// <summary>
    /// Represents a GPU-resident texture with dimensions.
    /// </summary>
    public abstract class RuntimeTexture : RuntimeData, IDisposable {
        /// <summary>
        /// Gets whether this runtime texture has already released its renderer-owned resources.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Gets or sets the width of the texture in pixels.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the height of the texture in pixels.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets whether this runtime texture is owned by engine infrastructure instead of one loaded scene.
        /// </summary>
        public bool IsEngineOwned { get; set; }

        /// <summary>
        /// Gets or sets whether this runtime font atlas stores independent RGB subpixel coverage.
        /// Its alpha channel retains grayscale coverage for scaled text and transparent render targets.
        /// This flag belongs to runtime UI fonts and is not part of the cooked texture format.
        /// </summary>
        public bool UsesRgbFontCoverage { get; set; }

        /// <summary>
        /// Releases renderer-owned resources associated with this runtime texture.
        /// </summary>
        public virtual void Dispose() {
            IsDisposed = true;
        }
    }
}

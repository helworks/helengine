namespace helengine {
    /// <summary>
    /// Describes one decorative menu overlay image baked into the generated demo-disc scene.
    /// </summary>
    public sealed class MenuOverlayImageDefinition {
        /// <summary>
        /// Initializes one decorative menu overlay image definition.
        /// </summary>
        /// <param name="texturePath">Project-relative texture source path.</param>
        /// <param name="width">Authored overlay width in canvas pixels.</param>
        /// <param name="height">Authored overlay height in canvas pixels.</param>
        /// <param name="topMargin">Top margin from the reference canvas edge in pixels.</param>
        /// <param name="rightMargin">Right margin from the reference canvas edge in pixels.</param>
        public MenuOverlayImageDefinition(string texturePath, int width, int height, int topMargin, int rightMargin) {
            if (string.IsNullOrWhiteSpace(texturePath)) {
                throw new ArgumentException("Texture path must be provided.", nameof(texturePath));
            }
            if (width < 1) {
                throw new ArgumentOutOfRangeException(nameof(width), "Overlay width must be positive.");
            }
            if (height < 1) {
                throw new ArgumentOutOfRangeException(nameof(height), "Overlay height must be positive.");
            }
            if (topMargin < 0) {
                throw new ArgumentOutOfRangeException(nameof(topMargin), "Top margin must not be negative.");
            }
            if (rightMargin < 0) {
                throw new ArgumentOutOfRangeException(nameof(rightMargin), "Right margin must not be negative.");
            }

            TexturePath = texturePath;
            Width = width;
            Height = height;
            TopMargin = topMargin;
            RightMargin = rightMargin;
        }

        /// <summary>
        /// Gets the project-relative texture source path.
        /// </summary>
        public string TexturePath { get; }

        /// <summary>
        /// Gets the authored overlay width in canvas pixels.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the authored overlay height in canvas pixels.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the authored top margin from the reference canvas edge in pixels.
        /// </summary>
        public int TopMargin { get; }

        /// <summary>
        /// Gets the authored right margin from the reference canvas edge in pixels.
        /// </summary>
        public int RightMargin { get; }
    }
}

namespace helengine {
    /// <summary>
    /// Describes the authored logical canvas resolution used for one scene.
    /// </summary>
    public class SceneCanvasProfile {
        /// <summary>
        /// Default authored scene canvas width in logical pixels.
        /// </summary>
        public const int DefaultWidth = 1280;

        /// <summary>
        /// Default authored scene canvas height in logical pixels.
        /// </summary>
        public const int DefaultHeight = 720;

        /// <summary>
        /// Gets or sets the authored logical canvas width in pixels.
        /// </summary>
        public int Width { get; set; } = DefaultWidth;

        /// <summary>
        /// Gets or sets the authored logical canvas height in pixels.
        /// </summary>
        public int Height { get; set; } = DefaultHeight;
    }
}

namespace helengine.editor {
    /// <summary>
    /// Identifies the bounds overlay rendered around the active model preview.
    /// </summary>
    public enum ModelPreviewBoundsDisplayMode {
        /// <summary>
        /// Renders no bounds overlay.
        /// </summary>
        None = 0,
        /// <summary>
        /// Renders the model's axis-aligned bounding box as line segments.
        /// </summary>
        Box = 1,
        /// <summary>
        /// Renders the model's enclosing bounding sphere as line segments.
        /// </summary>
        Sphere = 2
    }
}

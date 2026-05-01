namespace helengine {
    /// <summary>
    /// Describes a 2D element that exposes its own size for anchor calculations.
    /// </summary>
    public interface IAnchorSizeProvider {
        /// <summary>
        /// Gets the local size of the anchored element in pixels.
        /// </summary>
        int2 AnchorSize { get; }
    }
}

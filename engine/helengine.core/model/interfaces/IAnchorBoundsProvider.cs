namespace helengine {
    /// <summary>
    /// Describes a layout container that can tell anchored children how large its available bounds are.
    /// </summary>
    public interface IAnchorBoundsProvider {
        /// <summary>
        /// Gets the size of the anchor space in local pixels.
        /// </summary>
        int2 AnchorBounds { get; }

        /// <summary>
        /// Raised when the anchor bounds change and dependent children should refresh their positions.
        /// </summary>
        event Action AnchorBoundsChanged;
    }
}

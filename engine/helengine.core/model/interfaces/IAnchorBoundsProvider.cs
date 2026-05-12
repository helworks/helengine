namespace helengine {
    /// <summary>
    /// Describes a layout container that can tell anchored children how large its available bounds are.
    /// </summary>
    public interface IAnchorBoundsProvider {
        /// <summary>
        /// Gets the resolved anchor space in local pixels.
        /// </summary>
        AnchorSpace AnchorSpace { get; }

        /// <summary>
        /// Raised when the anchor bounds change and dependent children should refresh their positions.
        /// </summary>
        event Action AnchorBoundsChanged;
    }
}

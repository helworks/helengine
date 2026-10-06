namespace helengine {

    /// <summary>
    /// Describes the pointer cursor a hovered interactable wants the host to display.
    /// </summary>
    public enum PointerCursorKind {
        /// <summary>
        /// Uses the host's default pointer cursor.
        /// </summary>
        Default,
        /// <summary>
        /// Uses a hand cursor for clickable affordances.
        /// </summary>
        Hand,
        /// <summary>
        /// Uses a text-editing cursor for text entry controls.
        /// </summary>
        Text,
        /// <summary>
        /// Uses the diagonal resize cursor for top-left and bottom-right corner grips.
        /// </summary>
        ResizeNorthWestSouthEast,
        /// <summary>
        /// Uses the diagonal resize cursor for top-right and bottom-left corner grips.
        /// </summary>
        ResizeNorthEastSouthWest,
        /// <summary>
        /// Signals that the editor should show its vertical autoscroll indicator at the activation point.
        /// </summary>
        AutoScrollVertical,
        /// <summary>
        /// Signals that the editor should show its horizontal autoscroll indicator at the activation point.
        /// </summary>
        AutoScrollHorizontal
    }
}

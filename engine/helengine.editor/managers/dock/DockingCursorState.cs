namespace helengine.editor {
    /// <summary>
    /// Represents the cursor state requested by the docking system.
    /// </summary>
    public enum DockingCursorState {
        /// <summary>
        /// Default cursor with no resize affordance.
        /// </summary>
        Default,
        /// <summary>
        /// Horizontal split cursor for vertical split handles.
        /// </summary>
        VerticalSplit,
        /// <summary>
        /// Vertical split cursor for horizontal split handles.
        /// </summary>
        HorizontalSplit
    }
}

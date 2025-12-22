namespace helengine.editor {
    /// <summary>
    /// Describes how a new dockable should be inserted relative to an existing docked area.
    /// </summary>
    public enum DockInsertDirection {
        /// <summary>
        /// Replaces or fills the current target area.
        /// </summary>
        Fill,
        /// <summary>
        /// Splits the target area vertically and inserts to the left.
        /// </summary>
        Left,
        /// <summary>
        /// Splits the target area vertically and inserts to the right.
        /// </summary>
        Right,
        /// <summary>
        /// Splits the target area horizontally and inserts above.
        /// </summary>
        Top,
        /// <summary>
        /// Splits the target area horizontally and inserts below.
        /// </summary>
        Bottom
    }
}

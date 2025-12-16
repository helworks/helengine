namespace helengine.editor {
    /// <summary>
    /// Defines the possible docking regions for editor windows.
    /// </summary>
    public enum DockRegion {
        /// <summary>
        /// Window floats freely and is not docked.
        /// </summary>
        Floating,
        /// <summary>
        /// Dock the window to the left side of the host.
        /// </summary>
        Left,
        /// <summary>
        /// Dock the window to the right side of the host.
        /// </summary>
        Right,
        /// <summary>
        /// Dock the window to the top of the host.
        /// </summary>
        Top,
        /// <summary>
        /// Dock the window to the bottom of the host.
        /// </summary>
        Bottom,
        /// <summary>
        /// Fill the remaining space in the host.
        /// </summary>
        Fill
    }
}

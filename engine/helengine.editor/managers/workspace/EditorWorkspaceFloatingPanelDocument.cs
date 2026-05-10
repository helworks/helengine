namespace helengine.editor {
    /// <summary>
    /// Represents one persisted floating panel rectangle inside a saved workspace slot.
    /// </summary>
    public sealed class EditorWorkspaceFloatingPanelDocument {
        /// <summary>
        /// Stable workspace instance identifier for the floating panel.
        /// </summary>
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>
        /// Left screen position of the floating panel.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Top screen position of the floating panel.
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Content width of the floating panel.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Content height of the floating panel.
        /// </summary>
        public int Height { get; set; }
    }
}

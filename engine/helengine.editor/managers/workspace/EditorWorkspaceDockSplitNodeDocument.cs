namespace helengine.editor {
    /// <summary>
    /// Represents one persisted split node inside the saved dock tree.
    /// </summary>
    public sealed class EditorWorkspaceDockSplitNodeDocument : EditorWorkspaceDockNodeDocument {
        /// <summary>
        /// Indicates whether the split divides left and right children.
        /// </summary>
        public bool IsVertical { get; set; }

        /// <summary>
        /// Fraction allocated to the first child.
        /// </summary>
        public float SplitFraction { get; set; }

        /// <summary>
        /// First child node inside the split.
        /// </summary>
        public EditorWorkspaceDockNodeDocument First { get; set; }

        /// <summary>
        /// Second child node inside the split.
        /// </summary>
        public EditorWorkspaceDockNodeDocument Second { get; set; }
    }
}

namespace helengine.editor {
    /// <summary>
    /// Represents one captured split node inside the dock tree.
    /// </summary>
    public sealed class EditorWorkspaceDockSplitSnapshot : EditorWorkspaceDockNodeSnapshot {
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
        public EditorWorkspaceDockNodeSnapshot First { get; set; }

        /// <summary>
        /// Second child node inside the split.
        /// </summary>
        public EditorWorkspaceDockNodeSnapshot Second { get; set; }
    }
}

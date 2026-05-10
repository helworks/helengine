namespace helengine.editor {
    /// <summary>
    /// Represents one captured dock layout tree.
    /// </summary>
    public sealed class EditorWorkspaceDockSnapshot {
        /// <summary>
        /// Root node of the captured dock tree.
        /// </summary>
        public EditorWorkspaceDockNodeSnapshot Root { get; set; }
    }
}

namespace helengine.editor {
    /// <summary>
    /// Represents one persisted tabbed dock leaf inside the saved dock tree.
    /// </summary>
    public sealed class EditorWorkspaceDockLeafNodeDocument : EditorWorkspaceDockNodeDocument {
        /// <summary>
        /// Ordered panel instance identifiers that belong to the leaf tab group.
        /// </summary>
        public List<string> InstanceIds { get; set; } = new List<string>();

        /// <summary>
        /// Active panel instance identifier inside the tab group.
        /// </summary>
        public string ActiveInstanceId { get; set; } = string.Empty;
    }
}

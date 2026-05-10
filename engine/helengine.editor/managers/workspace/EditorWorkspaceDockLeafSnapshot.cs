namespace helengine.editor {
    /// <summary>
    /// Represents one captured tabbed leaf inside the dock tree.
    /// </summary>
    public sealed class EditorWorkspaceDockLeafSnapshot : EditorWorkspaceDockNodeSnapshot {
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

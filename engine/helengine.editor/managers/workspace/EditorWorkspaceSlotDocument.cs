namespace helengine.editor {
    /// <summary>
    /// Represents one persisted workspace slot containing panel, dock, and floating state.
    /// </summary>
    public sealed class EditorWorkspaceSlotDocument {
        /// <summary>
        /// Persistence schema version for the current slot payload.
        /// </summary>
        public int SchemaVersion { get; set; }

        /// <summary>
        /// Persisted panel instances that belong to the saved workspace.
        /// </summary>
        public List<EditorWorkspacePanelDocument> Panels { get; set; } = new List<EditorWorkspacePanelDocument>();

        /// <summary>
        /// Persisted floating-panel records for the saved workspace.
        /// </summary>
        public List<EditorWorkspaceFloatingPanelDocument> FloatingPanels { get; set; } = new List<EditorWorkspaceFloatingPanelDocument>();

        /// <summary>
        /// Persisted dock tree root for the saved workspace.
        /// </summary>
        public EditorWorkspaceDockNodeDocument DockRoot { get; set; }
    }
}

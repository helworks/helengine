namespace helengine.editor {
    /// <summary>
    /// Represents one persisted panel instance inside a saved workspace slot.
    /// </summary>
    public sealed class EditorWorkspacePanelDocument {
        /// <summary>
        /// Stable workspace instance identifier for the panel.
        /// </summary>
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>
        /// Stable panel type identifier used to recreate the panel.
        /// </summary>
        public string PanelTypeId { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the panel was docked when the slot was saved.
        /// </summary>
        public bool IsDocked { get; set; }

        /// <summary>
        /// Optional display title metadata stored with the panel instance.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Optional serialized panel-specific state payload.
        /// </summary>
        public object State { get; set; }
    }
}

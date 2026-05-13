namespace helengine.editor {
    /// <summary>
    /// Represents one persisted preview-panel binding payload.
    /// </summary>
    public sealed class PreviewPanelStateDocument {
        /// <summary>
        /// True when the preview panel should ignore later latest-click updates.
        /// </summary>
        public bool IsLocked { get; set; }

        /// <summary>
        /// Gets or sets the kind of target the preview panel is currently bound to.
        /// </summary>
        public PreviewPanelBindingKind BindingKind { get; set; }

        /// <summary>
        /// Gets or sets the relative asset path used when the preview panel is bound to one asset.
        /// </summary>
        public string AssetRelativePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the stable scene entity id used when the preview panel is bound to one camera.
        /// </summary>
        public uint SceneEntityId { get; set; }
    }
}

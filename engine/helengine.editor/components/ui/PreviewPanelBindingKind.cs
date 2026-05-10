namespace helengine.editor {
    /// <summary>
    /// Describes what kind of target one preview panel is currently bound to.
    /// </summary>
    public enum PreviewPanelBindingKind {
        /// <summary>
        /// No preview target is currently bound.
        /// </summary>
        None,
        /// <summary>
        /// The preview panel is currently bound to one asset.
        /// </summary>
        Asset,
        /// <summary>
        /// The preview panel is currently bound to one scene camera entity.
        /// </summary>
        Camera
    }
}

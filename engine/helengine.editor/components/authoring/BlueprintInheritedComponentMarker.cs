namespace helengine.editor {
    /// <summary>
    /// Stores read-only source identity for one inherited live component expanded from a blueprint asset.
    /// </summary>
    public sealed class BlueprintInheritedComponentMarker : Component, IEditorHiddenComponent {
        /// <summary>
        /// Gets or sets the project-relative blueprint asset path that produced the inherited component.
        /// </summary>
        public string BlueprintAssetPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the stable source entity id authored inside the blueprint asset.
        /// </summary>
        public uint SourceEntityId { get; set; }

        /// <summary>
        /// Gets or sets the stable source component key authored inside the blueprint asset.
        /// </summary>
        public string SourceComponentKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the concrete runtime component type id that this marker describes.
        /// </summary>
        public string TargetComponentTypeId { get; set; } = string.Empty;
    }
}

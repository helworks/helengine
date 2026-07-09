namespace helengine.editor {
    /// <summary>
    /// Marks one expanded entity as inherited from a blueprint instance source.
    /// </summary>
    public sealed class BlueprintInheritedEntityComponent : Component, IEditorHiddenComponent {
        /// <summary>
        /// Gets or sets the project-relative blueprint asset path that produced this inherited entity.
        /// </summary>
        public string BlueprintAssetPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the stable source entity id authored inside the blueprint asset.
        /// </summary>
        public uint SourceEntityId { get; set; }
    }
}

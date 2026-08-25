namespace helengine.editor {
    /// <summary>
    /// Marks one expanded entity as inherited from a blueprint instance source.
    /// </summary>
    public sealed class BlueprintInheritedEntityComponent : Component, IEditorHiddenComponent {
        /// <summary>
        /// Gets or sets the project-relative blueprint asset path that produced this inherited entity.
        /// </summary>
        /// <summary>Gets or sets the canonical stable blueprint source reference.</summary>
        [ScenePersistenceIgnore]
        public SceneAssetReference BlueprintAssetReference { get; set; }

        /// <summary>
        /// Gets or sets the stable source entity id authored inside the blueprint asset.
        /// </summary>
        public uint SourceEntityId { get; set; }
    }
}

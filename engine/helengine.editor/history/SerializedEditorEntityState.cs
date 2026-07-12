namespace helengine.editor {
    /// <summary>
    /// Stores one detached serialized editor entity snapshot for undo/redo operations that remove and restore scene entities.
    /// </summary>
    public class SerializedEditorEntityState {
        /// <summary>
        /// Gets or sets the stable scene entity id owned by the serialized entity snapshot.
        /// </summary>
        public uint EntityId { get; set; }

        /// <summary>
        /// Gets or sets the stable scene entity id of the serialized parent entity, or zero when the entity belongs at the scene root.
        /// </summary>
        public uint ParentEntityId { get; set; }

        /// <summary>
        /// Gets or sets the serialized entity payload captured from the live editor scene.
        /// </summary>
        public SceneEntityAsset EntityAsset { get; set; }

        /// <summary>
        /// Gets or sets the serialized asset references required to materialize the captured entity payload.
        /// </summary>
        public SceneAssetReference[] AssetReferences { get; set; } = Array.Empty<SceneAssetReference>();
    }
}

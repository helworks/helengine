namespace helengine.editor {
    /// <summary>
    /// Stores one scene-owned override that assigns a scene entity id to an entity-reference property on a cloned blueprint component.
    /// </summary>
    public sealed class BlueprintEntityReferenceOverrideAsset {
        /// <summary>
        /// Gets or sets the source entity id inside the blueprint whose component should receive the override.
        /// </summary>
        public uint SourceEntityId { get; set; }

        /// <summary>
        /// Gets or sets the stable persisted component key inside the source blueprint entity.
        /// </summary>
        public string ComponentKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the writable component property that stores the scene entity reference.
        /// </summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the scene entity id assigned to the cloned component property.
        /// </summary>
        public uint TargetEntityId { get; set; }
    }
}

namespace helengine {
    /// <summary>
    /// Stores the stable serialized scene-entity id on one live runtime entity so runtime-authored systems can resolve scene references without editor-only metadata.
    /// </summary>
#if !HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
    [RunInEditor]
#endif
    public sealed class SceneEntityRuntimeIdComponent : Component {
        /// <summary>
        /// Gets or sets the stable serialized scene-entity id restored for the owning runtime entity.
        /// </summary>
        public uint SceneEntityId { get; set; }
    }
}

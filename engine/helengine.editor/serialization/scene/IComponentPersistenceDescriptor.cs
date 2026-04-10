namespace helengine.editor {
    /// <summary>
    /// Defines the explicit save and load contract for one persisted component type.
    /// </summary>
    public interface IComponentPersistenceDescriptor {
        /// <summary>
        /// Gets the concrete runtime component type handled by the descriptor.
        /// </summary>
        Type ComponentType { get; }

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        string ComponentTypeId { get; }

        /// <summary>
        /// Serializes one live component into a scene component record.
        /// </summary>
        /// <param name="component">Live component instance to serialize.</param>
        /// <param name="componentIndex">Entity-local index used to preserve component ordering.</param>
        /// <param name="saveState">Editor-time save metadata associated with the component.</param>
        /// <returns>Serialized scene component record.</returns>
        SceneComponentAssetRecord SerializeComponent(Component component, int componentIndex, EntityComponentSaveState saveState);

        /// <summary>
        /// Deserializes one scene component record back into a live component instance.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <param name="saveComponent">Hidden entity save component that should receive restored metadata.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime asset references.</param>
        /// <returns>Live component instance reconstructed from the scene record.</returns>
        Component DeserializeComponent(
            SceneComponentAssetRecord record,
            EntitySaveComponent saveComponent,
            ISceneAssetReferenceResolver referenceResolver);
    }
}

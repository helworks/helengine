namespace helengine {
    /// <summary>
    /// Deserializes one runtime component type from a packaged scene record.
    /// </summary>
    public interface IRuntimeComponentDeserializer {
        /// <summary>
        /// Gets the stable serialized component type id handled by this deserializer.
        /// </summary>
        string ComponentTypeId { get; }

        /// <summary>
        /// Materializes one runtime component from its packaged scene record.
        /// </summary>
        /// <param name="record">Packaged scene record to deserialize.</param>
        /// <param name="referenceResolver">Resolver used to rebuild packaged asset references.</param>
        /// <returns>A newly materialized component whose cleanup responsibility transfers to the caller.</returns>
        [NativeOwnedReturn]
        Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver);
    }
}

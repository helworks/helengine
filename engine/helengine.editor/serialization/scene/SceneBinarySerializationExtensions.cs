namespace helengine.editor {
    /// <summary>
    /// Provides scene-specific binary helpers for serializing live entities as stable references.
    /// </summary>
    public static class SceneBinarySerializationExtensions {
        /// <summary>
        /// Writes one live entity as a stable scene entity reference.
        /// </summary>
        /// <param name="writer">Destination writer receiving the reference.</param>
        /// <param name="entity">Live entity to serialize.</param>
        /// <param name="referenceTable">Table used to assign and track stable entity ids.</param>
        public static void WriteEntityReference(this EngineBinaryWriter writer, Entity entity, SceneEntityReferenceTable referenceTable) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }
            if (referenceTable == null) {
                throw new ArgumentNullException(nameof(referenceTable));
            }
            if (entity == null) {
                writer.WriteSceneEntityReference(null);
                return;
            }

            writer.WriteSceneEntityReference(referenceTable.GetOrCreateReference(entity));
        }
    }
}

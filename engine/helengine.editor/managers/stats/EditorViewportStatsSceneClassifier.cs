namespace helengine.editor {
    /// <summary>
    /// Classifies entities into authored game-scene content versus editor-internal content for the stats overlay.
    /// </summary>
    public static class EditorViewportStatsSceneClassifier {
        /// <summary>
        /// Returns whether one entity belongs to the authored game scene rather than editor-internal machinery.
        /// </summary>
        /// <param name="entity">Entity to classify; null classifies as editor content.</param>
        /// <returns>True when the entity is authored scene content.</returns>
        public static bool IsSceneEntity(Entity entity) {
            return entity is EditorEntity editorEntity
                && editorEntity.IsSceneOwned
                && !editorEntity.InternalEntity;
        }
    }
}

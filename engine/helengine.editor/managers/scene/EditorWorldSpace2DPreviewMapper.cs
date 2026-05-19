namespace helengine.editor {
    /// <summary>
    /// Centralizes editor-only decisions about which entities participate in 2D world-preview proxy rendering and how preview selections resolve back to authored scene entities.
    /// </summary>
    public static class EditorWorldSpace2DPreviewMapper {
        /// <summary>
        /// Determines whether one entity is currently acting as an internal 2D world-preview proxy.
        /// </summary>
        /// <param name="entity">Entity to evaluate.</param>
        /// <returns>True when the entity is an editor preview proxy with a registered source mapping.</returns>
        public static bool IsPreviewProxyEntity(Entity entity) {
            if (entity is not EditorEntity previewEntity) {
                return false;
            }

            return EditorWorldSpace2DPreviewRegistry.ResolveSourceEntity(previewEntity) != null;
        }

        /// <summary>
        /// Resolves the authored source entity that should be selected when a preview proxy is clicked.
        /// </summary>
        /// <param name="entity">Preview proxy candidate.</param>
        /// <returns>Authored source entity when the supplied entity is a registered preview proxy; otherwise null.</returns>
        public static Entity ResolveSourceSelectionEntity(Entity entity) {
            if (entity is not EditorEntity previewEntity) {
                return null;
            }

            return EditorWorldSpace2DPreviewRegistry.ResolveSourceEntity(previewEntity);
        }
    }
}

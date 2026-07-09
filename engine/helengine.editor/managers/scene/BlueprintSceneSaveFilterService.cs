namespace helengine.editor {
    /// <summary>
    /// Filters expanded inherited blueprint entities from scene serialization.
    /// </summary>
    public static class BlueprintSceneSaveFilterService {
        /// <summary>
        /// Returns whether one editor entity should be serialized as scene-owned content.
        /// </summary>
        /// <param name="entity">Entity to evaluate.</param>
        /// <returns>True when the entity is not an inherited blueprint expansion node.</returns>
        public static bool ShouldSerializeEntity(EditorEntity entity) {
            if (entity == null) {
                return false;
            }

            return !HasInheritedMarker(entity);
        }

        /// <summary>
        /// Returns whether one entity is an inherited blueprint expansion node.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>True when the entity has an inherited blueprint marker.</returns>
        static bool HasInheritedMarker(EditorEntity entity) {
            if (entity?.Components == null) {
                return false;
            }

            for (int i = 0; i < entity.Components.Count; i++) {
                if (entity.Components[i] is BlueprintInheritedEntityComponent) {
                    return true;
                }
            }

            return false;
        }
    }
}

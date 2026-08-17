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

            return !IsInheritedEntity(entity);
        }

        /// <summary>
        /// Returns whether one entity is a read-only inherited blueprint expansion node.
        /// </summary>
        /// <param name="entity">Entity to inspect; null classifies as not inherited.</param>
        /// <returns>True when the entity has an inherited blueprint marker.</returns>
        public static bool IsInheritedEntity(Entity entity) {
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

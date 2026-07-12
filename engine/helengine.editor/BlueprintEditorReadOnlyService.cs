namespace helengine.editor {
    /// <summary>
    /// Resolves whether live editor entities originate from expanded blueprint content.
    /// </summary>
    public static class BlueprintEditorReadOnlyService {
        /// <summary>
        /// Returns whether the supplied entity is inherited from a blueprint expansion and should remain read-only in the scene editor.
        /// </summary>
        /// <param name="entity">Entity to classify.</param>
        /// <returns>True when the entity carries blueprint-inherited editor state.</returns>
        public static bool IsInheritedEntity(Entity entity) {
            if (entity == null || entity.IsDisposed || entity.Components == null) {
                return false;
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is BlueprintInheritedEntityComponent) {
                    return true;
                }
            }

            return false;
        }
    }
}

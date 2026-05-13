namespace helengine {
    /// <summary>
    /// Resolves whether one component should execute runtime behavior while the editor is authoring user scene entities.
    /// </summary>
    public static class ComponentExecutionPolicy {
        /// <summary>
        /// Returns whether the supplied component should execute its lifecycle callbacks in the current mode.
        /// </summary>
        /// <param name="component">Component whose behavior is being evaluated.</param>
        /// <param name="entity">Entity that owns the component.</param>
        /// <returns>True when the component should run its lifecycle callbacks; otherwise false.</returns>
        public static bool ShouldRunComponentLifecycle(Component component, Entity entity) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (ComponentExecutionContext.CurrentMode != ComponentExecutionMode.Editor) {
                return true;
            }
            if (!HasEditorUpdateExecutionSuppressionMarker(entity)) {
                return true;
            }
#if HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
            return false;
#else
            if (component is not UpdateComponent) {
                return true;
            }

            return Attribute.IsDefined(component.GetType(), typeof(RunInEditorAttribute), true);
#endif
        }

        /// <summary>
        /// Returns whether the supplied entity carries the editor-only marker that suppresses gameplay update execution during authoring.
        /// </summary>
        /// <param name="entity">Entity whose component collection should be inspected.</param>
        /// <returns>True when the marker exists; otherwise false.</returns>
        static bool HasEditorUpdateExecutionSuppressionMarker(Entity entity) {
            if (entity.Components == null) {
                return false;
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                Component component = entity.Components[index];
                if (component != null && component.IsEditorUpdateExecutionSuppressionMarker) {
                    return true;
                }
            }

            return false;
        }
    }
}

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
            if (entity.OwnerCore.ExecutionMode != ComponentExecutionMode.Editor
                && ComponentExecutionContext.CurrentMode != ComponentExecutionMode.Editor) {
                return true;
            }
#if HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
            return false;
#else
            return Attribute.IsDefined(component.GetType(), typeof(RunInEditorAttribute), false);
#endif
        }
    }
}

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
            IComponentExecutionPolicy policy = ComponentExecutionContext.CurrentPolicy;
            if (entity.OwnerCore.ExecutionMode != ComponentExecutionMode.Editor && policy == null) {
                return true;
            }

            if (policy == null) {
                policy = entity.OwnerCore.InitializationOptions.EditorComponentExecutionPolicy;
            }
            if (policy == null) {
                throw new InvalidOperationException("Editor component execution requires an explicit execution policy.");
            }

            return policy.ShouldRunComponentLifecycle(component, entity);
        }
    }
}

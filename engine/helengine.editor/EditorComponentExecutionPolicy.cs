namespace helengine.editor {
    /// <summary>
    /// Restricts editor lifecycle execution to component types that explicitly declare RunInEditor without inheriting permission from a base type.
    /// </summary>
    public sealed class EditorComponentExecutionPolicy : IComponentExecutionPolicy {
        /// <summary>
        /// Gets the stateless policy shared by editor hosts and authoring scopes.
        /// </summary>
        public static EditorComponentExecutionPolicy Instance { get; } = new EditorComponentExecutionPolicy();

        /// <summary>
        /// Creates the shared attribute-based authoring policy.
        /// </summary>
        EditorComponentExecutionPolicy() {
        }

        /// <inheritdoc />
        public bool ShouldRunComponentLifecycle(Component component, Entity entity) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            return Attribute.IsDefined(component.GetType(), typeof(RunInEditorAttribute), inherit: false);
        }
    }
}

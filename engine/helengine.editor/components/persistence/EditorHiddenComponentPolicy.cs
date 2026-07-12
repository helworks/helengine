namespace helengine {
    /// <summary>
    /// Centralizes editor visibility rules for runtime and editor-only components that must stay out of authoring surfaces.
    /// </summary>
    public static class EditorHiddenComponentPolicy {
        /// <summary>
        /// Component types that participate in runtime bookkeeping but must never appear in editor authoring UI.
        /// </summary>
        static readonly HashSet<Type> HiddenRuntimeComponentTypes = new HashSet<Type> {
            typeof(SceneEntityRuntimeIdComponent)
        };

        /// <summary>
        /// Determines whether one component instance should be hidden from editor authoring UI.
        /// </summary>
        /// <param name="component">Component instance to evaluate.</param>
        /// <returns>True when the component should stay hidden from authoring surfaces.</returns>
        public static bool IsHidden(Component component) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            return IsHidden(component.GetType());
        }

        /// <summary>
        /// Determines whether one component type should be hidden from editor authoring UI.
        /// </summary>
        /// <param name="componentType">Component type to evaluate.</param>
        /// <returns>True when the component type should stay hidden from authoring surfaces.</returns>
        public static bool IsHidden(Type componentType) {
            if (componentType == null) {
                throw new ArgumentNullException(nameof(componentType));
            }

            if (typeof(IEditorHiddenComponent).IsAssignableFrom(componentType)) {
                return true;
            }

            return HiddenRuntimeComponentTypes.Contains(componentType);
        }
    }
}

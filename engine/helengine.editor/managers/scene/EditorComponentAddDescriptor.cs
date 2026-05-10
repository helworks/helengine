namespace helengine.editor {
    /// <summary>
    /// Describes one component that can be added from the properties panel.
    /// </summary>
    public sealed class EditorComponentAddDescriptor {
        /// <summary>
        /// Initializes a new component-add descriptor.
        /// </summary>
        /// <param name="displayName">Menu label shown to the user.</param>
        /// <param name="componentType">Concrete component type added by this option.</param>
        /// <param name="singleInstance">True when the option should only be shown if the entity does not already own the component type.</param>
        /// <param name="addAction">Callback that attaches the component to the selected entity.</param>
        public EditorComponentAddDescriptor(string displayName, Type componentType, bool singleInstance, Action<Entity> addAction) {
            if (string.IsNullOrWhiteSpace(displayName)) {
                throw new ArgumentException("Display name must be provided.", nameof(displayName));
            }
            if (componentType == null) {
                throw new ArgumentNullException(nameof(componentType));
            }
            if (addAction == null) {
                throw new ArgumentNullException(nameof(addAction));
            }

            DisplayName = displayName;
            ComponentType = componentType;
            SingleInstance = singleInstance;
            AddAction = addAction;
        }

        /// <summary>
        /// Gets the user-facing menu label.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the concrete component type added by this option.
        /// </summary>
        public Type ComponentType { get; }

        /// <summary>
        /// Gets a value indicating whether the option should be hidden once the entity already owns one instance.
        /// </summary>
        public bool SingleInstance { get; }

        /// <summary>
        /// Gets the callback that attaches the component to an entity.
        /// </summary>
        public Action<Entity> AddAction { get; }

        /// <summary>
        /// Creates a detached component instance that matches this add descriptor.
        /// </summary>
        /// <returns>Detached component instance created from the descriptor component type.</returns>
        public Component CreateComponentInstance() {
            object instance = Activator.CreateInstance(ComponentType);
            if (instance is Component component) {
                return component;
            }

            throw new InvalidOperationException($"Component descriptor '{DisplayName}' could not create an instance of '{ComponentType.FullName}'.");
        }
    }
}

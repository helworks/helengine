namespace helengine.editor {
    /// <summary>
    /// Resolves component-scoped history adapters so custom components can provide specialized undo/redo operations.
    /// </summary>
    public class ComponentHistoryAdapterRegistry {
        /// <summary>
        /// Registered adapters keyed by the component type they own.
        /// </summary>
        readonly Dictionary<Type, IComponentHistoryAdapter> AdaptersByComponentType;

        /// <summary>
        /// Fallback adapter used when no specialized registration exists for the mutated component type.
        /// </summary>
        readonly IComponentHistoryAdapter DefaultAdapter;

        /// <summary>
        /// Initializes one component history adapter registry with the built-in default adapter.
        /// </summary>
        public ComponentHistoryAdapterRegistry() {
            AdaptersByComponentType = new Dictionary<Type, IComponentHistoryAdapter>();
            DefaultAdapter = new DefaultComponentHistoryAdapter();
        }

        /// <summary>
        /// Registers one component history adapter for the supplied component type.
        /// </summary>
        /// <param name="componentType">Component type that should resolve to the supplied adapter.</param>
        /// <param name="adapter">Adapter that should create history operations for the component type.</param>
        public void Register(Type componentType, IComponentHistoryAdapter adapter) {
            if (componentType == null) {
                throw new ArgumentNullException(nameof(componentType));
            }
            if (adapter == null) {
                throw new ArgumentNullException(nameof(adapter));
            }
            if (!typeof(Component).IsAssignableFrom(componentType)) {
                throw new InvalidOperationException("Component history adapters can only be registered for component types.");
            }

            AdaptersByComponentType[componentType] = adapter;
        }

        /// <summary>
        /// Registers one component history adapter for the supplied component type parameter.
        /// </summary>
        /// <typeparam name="TComponent">Component type that should resolve to the supplied adapter.</typeparam>
        /// <param name="adapter">Adapter that should create history operations for the component type.</param>
        public void Register<TComponent>(IComponentHistoryAdapter adapter) where TComponent : Component {
            if (adapter == null) {
                throw new ArgumentNullException(nameof(adapter));
            }

            Register(typeof(TComponent), adapter);
        }

        /// <summary>
        /// Resolves the adapter that should record history for the supplied component instance.
        /// </summary>
        /// <param name="component">Component whose history adapter should be resolved.</param>
        /// <returns>Registered adapter when one matches; otherwise the built-in default adapter.</returns>
        public IComponentHistoryAdapter Resolve(Component component) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            Type componentType = component.GetType();
            if (AdaptersByComponentType.TryGetValue(componentType, out IComponentHistoryAdapter exactAdapter)) {
                return exactAdapter;
            }

            foreach (KeyValuePair<Type, IComponentHistoryAdapter> pair in AdaptersByComponentType) {
                if (pair.Key.IsAssignableFrom(componentType)) {
                    return pair.Value;
                }
            }

            return DefaultAdapter;
        }
    }
}

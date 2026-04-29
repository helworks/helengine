namespace helengine.editor {
    /// <summary>
    /// Stores explicit scene persistence descriptors keyed by runtime component type and serialized type id.
    /// </summary>
    public class ComponentPersistenceRegistry {
        /// <summary>
        /// Descriptors keyed by runtime component type.
        /// </summary>
        readonly Dictionary<Type, IComponentPersistenceDescriptor> DescriptorsByComponentType;

        /// <summary>
        /// Descriptors keyed by stable serialized type id.
        /// </summary>
        readonly Dictionary<string, IComponentPersistenceDescriptor> DescriptorsByTypeId;

        /// <summary>
        /// Initializes empty descriptor lookup tables.
        /// </summary>
        public ComponentPersistenceRegistry() {
            DescriptorsByComponentType = new Dictionary<Type, IComponentPersistenceDescriptor>();
            DescriptorsByTypeId = new Dictionary<string, IComponentPersistenceDescriptor>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Registers one component persistence descriptor.
        /// </summary>
        /// <param name="descriptor">Descriptor to register.</param>
        public void Register(IComponentPersistenceDescriptor descriptor) {
            if (descriptor == null) {
                throw new ArgumentNullException(nameof(descriptor));
            }
            if (descriptor.ComponentType == null) {
                throw new InvalidOperationException("Persistence descriptors must expose a component type.");
            }
            if (string.IsNullOrWhiteSpace(descriptor.ComponentTypeId)) {
                throw new InvalidOperationException("Persistence descriptors must expose a serialized component type id.");
            }
            if (DescriptorsByComponentType.ContainsKey(descriptor.ComponentType)) {
                throw new InvalidOperationException($"A persistence descriptor is already registered for '{descriptor.ComponentType.Name}'.");
            }
            if (DescriptorsByTypeId.ContainsKey(descriptor.ComponentTypeId)) {
                throw new InvalidOperationException($"A persistence descriptor is already registered for '{descriptor.ComponentTypeId}'.");
            }

            DescriptorsByComponentType.Add(descriptor.ComponentType, descriptor);
            DescriptorsByTypeId.Add(descriptor.ComponentTypeId, descriptor);
        }

        /// <summary>
        /// Resolves the descriptor that handles one live component instance.
        /// </summary>
        /// <param name="component">Live component instance whose descriptor is required.</param>
        /// <returns>Descriptor registered for the component type.</returns>
        public IComponentPersistenceDescriptor GetDescriptor(Component component) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            Type componentType = component.GetType();
            if (!DescriptorsByComponentType.TryGetValue(componentType, out IComponentPersistenceDescriptor descriptor)) {
                throw new InvalidOperationException($"No scene persistence descriptor is registered for '{componentType.Name}'.");
            }

            return descriptor;
        }

        /// <summary>
        /// Resolves the descriptor that handles one serialized component type id.
        /// </summary>
        /// <param name="componentTypeId">Serialized component type id whose descriptor is required.</param>
        /// <returns>Descriptor registered for the type id.</returns>
        public IComponentPersistenceDescriptor GetDescriptor(string componentTypeId) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                throw new ArgumentException("Component type id must be provided.", nameof(componentTypeId));
            }

            if (!DescriptorsByTypeId.TryGetValue(componentTypeId, out IComponentPersistenceDescriptor descriptor)) {
                throw new InvalidOperationException($"No scene persistence descriptor is registered for '{componentTypeId}'.");
            }

            return descriptor;
        }
    }
}

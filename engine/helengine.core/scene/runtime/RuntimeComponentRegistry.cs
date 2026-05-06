namespace helengine {
    /// <summary>
    /// Stores runtime component deserializers keyed by serialized type id.
    /// </summary>
    public sealed class RuntimeComponentRegistry {
        /// <summary>
        /// Deserializers keyed by stable serialized component type id.
        /// </summary>
        readonly Dictionary<string, IRuntimeComponentDeserializer> DeserializersByTypeId;

        /// <summary>
        /// Initializes an empty runtime component registry.
        /// </summary>
        public RuntimeComponentRegistry() {
            DeserializersByTypeId = new Dictionary<string, IRuntimeComponentDeserializer>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds the default registry used by player builds.
        /// </summary>
        /// <returns>Registry populated with the built-in runtime component deserializers.</returns>
        public static RuntimeComponentRegistry CreateDefault() {
            RuntimeComponentRegistry registry = new RuntimeComponentRegistry();
            registry.Register(new RuntimeMeshComponentDeserializer());
            registry.Register(new RuntimeCameraComponentDeserializer());
            registry.Register(new RuntimeFPSComponentDeserializer());
            registry.Register(new RuntimeTextComponentDeserializer());
            registry.Register(new RuntimeRoundedRectComponentDeserializer());
            registry.Register(new RuntimeDirectionalLightComponentDeserializer());
            registry.Register(new RuntimePointLightComponentDeserializer());
            registry.Register(new RuntimeSpotLightComponentDeserializer());
            registry.Register(new RuntimeMenuComponentDeserializer());
            registry.Register(new RuntimeMenuPanelComponentDeserializer());
            registry.Register(new RuntimeMenuItemComponentDeserializer());
            registry.Register(new RuntimeMenuSelectedDescriptionComponentDeserializer());
            return registry;
        }

        /// <summary>
        /// Registers one runtime component deserializer.
        /// </summary>
        /// <param name="deserializer">Deserializer to register.</param>
        public void Register(IRuntimeComponentDeserializer deserializer) {
            if (deserializer == null) {
                throw new ArgumentNullException(nameof(deserializer));
            }
            if (string.IsNullOrWhiteSpace(deserializer.ComponentTypeId)) {
                throw new InvalidOperationException("Runtime component deserializers must expose a serialized type id.");
            }
            if (DeserializersByTypeId.ContainsKey(deserializer.ComponentTypeId)) {
                throw new InvalidOperationException($"A runtime component deserializer is already registered for '{deserializer.ComponentTypeId}'.");
            }

            DeserializersByTypeId.Add(deserializer.ComponentTypeId, deserializer);
        }

        /// <summary>
        /// Attempts to resolve one runtime component deserializer by serialized component type id.
        /// </summary>
        /// <param name="componentTypeId">Serialized component type id.</param>
        /// <param name="deserializer">Resolved deserializer when found.</param>
        /// <returns>True when a matching deserializer is registered; otherwise false.</returns>
        public bool TryGet(string componentTypeId, out IRuntimeComponentDeserializer deserializer) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                deserializer = null;
                return false;
            }

            return DeserializersByTypeId.TryGetValue(componentTypeId, out deserializer);
        }

        /// <summary>
        /// Resolves one runtime component deserializer by serialized component type id.
        /// </summary>
        /// <param name="componentTypeId">Serialized component type id.</param>
        /// <returns>Matching deserializer.</returns>
        public IRuntimeComponentDeserializer GetDeserializer(string componentTypeId) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                throw new ArgumentException("Component type id must be provided.", nameof(componentTypeId));
            }

            if (!DeserializersByTypeId.TryGetValue(componentTypeId, out IRuntimeComponentDeserializer deserializer)) {
                deserializer = TryCreateAutomaticScriptComponentDeserializer(componentTypeId);
                if (deserializer == null) {
                    throw new InvalidOperationException($"Player builds do not support serialized component type '{componentTypeId}' yet.");
                }

                DeserializersByTypeId.Add(componentTypeId, deserializer);
            }

            return deserializer;
        }

        /// <summary>
        /// Creates one automatic scripted runtime deserializer when the serialized type id resolves to an eligible scripted component type.
        /// </summary>
        /// <param name="componentTypeId">Serialized component type id to inspect.</param>
        /// <returns>Automatic scripted runtime deserializer when the type id is eligible; otherwise null.</returns>
        IRuntimeComponentDeserializer TryCreateAutomaticScriptComponentDeserializer(string componentTypeId) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                return null;
            }

            Type componentType = Type.GetType(componentTypeId, false);
            if (componentType == null) {
                return null;
            }
            if (!typeof(Component).IsAssignableFrom(componentType)) {
                return null;
            }
            if (componentType.Assembly == typeof(Component).Assembly) {
                return null;
            }

            return new AutomaticScriptComponentRuntimeDeserializer(componentTypeId, componentType);
        }
    }
}

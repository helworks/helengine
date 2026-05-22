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
            registry.Register(new RuntimeDebugComponentDeserializer());
            registry.Register(new RuntimeTextComponentDeserializer());
            registry.Register(new RuntimeSpriteComponentDeserializer());
            registry.Register(new RuntimeRoundedRectComponentDeserializer());
            registry.Register(new RuntimeDirectionalLightComponentDeserializer());
            registry.Register(new RuntimeAmbientLightComponentDeserializer());
            registry.Register(new RuntimePointLightComponentDeserializer());
            registry.Register(new RuntimeSpotLightComponentDeserializer());
            registry.Register(new RuntimeDirectionalShadowCameraOrbitComponentDeserializer());
            registry.Register(new RuntimeDirectionalShadowOrbitComponentDeserializer());
            registry.Register(new RuntimeDirectionalShadowSunSweepComponentDeserializer());
            registry.Register(new RuntimeDirectionalShadowTowerSpinComponentDeserializer());
            registry.Register(new RuntimeSceneMapComponentDeserializer());
            RegisterGeneratedRuntimeComponentDeserializers(registry);
            return registry;
        }

        /// <summary>
        /// Registers any generated cooked-scene runtime component deserializers that are supplied by native build generation.
        /// </summary>
        /// <param name="registry">Registry that should receive generated runtime deserializers.</param>
        [NativeFreeFunction("RegisterGeneratedRuntimeComponentDeserializers", "GeneratedRuntimeComponentDeserializerRegistration.hpp")]
        static void RegisterGeneratedRuntimeComponentDeserializers(RuntimeComponentRegistry registry) {
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
#if HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
                throw new InvalidOperationException($"Player builds do not support serialized component type '{componentTypeId}' yet.");
#else
                deserializer = TryCreateAutomaticComponentDeserializer(componentTypeId);
                if (deserializer == null) {
                    throw new InvalidOperationException($"Player builds do not support serialized component type '{componentTypeId}' yet.");
                }

                DeserializersByTypeId.Add(componentTypeId, deserializer);
#endif
            }

            return deserializer;
        }

#if !HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
        /// <summary>
        /// Creates one automatic reflected runtime deserializer when the serialized type id resolves to an eligible component type.
        /// </summary>
        /// <param name="componentTypeId">Serialized component type id to inspect.</param>
        /// <returns>Automatic reflected runtime deserializer when the type id is eligible; otherwise null.</returns>
        IRuntimeComponentDeserializer TryCreateAutomaticComponentDeserializer(string componentTypeId) {
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

            return new AutomaticScriptComponentRuntimeDeserializer(componentTypeId, componentType);
        }
#endif
    }
}

namespace helengine {
    /// <summary>
    /// Stores runtime component deserializers keyed by serialized type id.
    /// </summary>
    public sealed class RuntimeComponentRegistry : IDisposable {
        /// <summary>
        /// Deserializers keyed by stable serialized component type id.
        /// </summary>
        [NativeOwnedMember]
        Dictionary<string, IRuntimeComponentDeserializer> DeserializersByTypeId;

        /// <summary>Associates overlay factories with their owning module, leaving resident factories untagged.</summary>
        [NativeOwnedMember]
        Dictionary<string, string> ModuleOwnersByTypeId;

        /// <summary>
        /// Initializes an empty runtime component registry.
        /// </summary>
        public RuntimeComponentRegistry() {
            DeserializersByTypeId = new Dictionary<string, IRuntimeComponentDeserializer>(StringComparer.Ordinal);
            ModuleOwnersByTypeId = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Builds the default registry used by player builds.
        /// </summary>
        /// <returns>Registry populated with the built-in runtime component deserializers.</returns>
        public static RuntimeComponentRegistry CreateDefault() {
            RuntimeComponentRegistry registry = new RuntimeComponentRegistry();
            RegisterBuiltInComponentDeserializers(registry);
            RegisterGeneratedRuntimeComponentDeserializers(registry);
            return registry;
        }

        /// <summary>
        /// Returns the serialized component type identifiers claimed by the built-in hand-authored runtime deserializers.
        /// </summary>
        /// <returns>Stable serialized component type ids registered before generated deserializers are added.</returns>
        public static IReadOnlyList<string> GetBuiltInComponentTypeIds() {
            RuntimeComponentRegistry registry = new RuntimeComponentRegistry();
            RegisterBuiltInComponentDeserializers(registry);
            List<string> builtInComponentTypeIds = new List<string>(registry.DeserializersByTypeId.Count);
            foreach (string componentTypeId in registry.DeserializersByTypeId.Keys) {
                builtInComponentTypeIds.Add(componentTypeId);
            }

            return builtInComponentTypeIds;
        }

        /// <summary>
        /// Registers any generated cooked-scene runtime component deserializers that are supplied by native build generation.
        /// </summary>
        /// <param name="registry">Registry that should receive generated runtime deserializers.</param>
        [NativeFreeFunction("RegisterGeneratedRuntimeComponentDeserializers", "GeneratedRuntimeComponentDeserializerRegistration.hpp")]
        static void RegisterGeneratedRuntimeComponentDeserializers(RuntimeComponentRegistry registry) {
        }

        /// <summary>
        /// Registers the built-in hand-authored runtime deserializers that player builds must own explicitly.
        /// </summary>
        /// <param name="registry">Registry receiving the built-in runtime deserializer set.</param>
        static void RegisterBuiltInComponentDeserializers(RuntimeComponentRegistry registry) {
            if (registry == null) {
                throw new ArgumentNullException(nameof(registry));
            }
        }

        /// <summary>
        /// Registers one runtime component deserializer.
        /// </summary>
        /// <param name="deserializer">Owned deserializer transferred into the registry's native-owning dictionary.</param>
        public void Register([NativeTakesOwnership] IRuntimeComponentDeserializer deserializer) {
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

        /// <summary>Transfers a factory to one module's ownership; failed duplicate registration deletes the incoming factory.</summary>
        /// <param name="moduleId">Stable native module owning the factory implementation.</param>
        /// <param name="deserializer">Owned factory transferred by the module registration entry.</param>
        public void RegisterForModule(string moduleId, [NativeTakesOwnership] IRuntimeComponentDeserializer deserializer) {
            bool registered = false;
            bool installed = false;
            try {
                if (string.IsNullOrWhiteSpace(moduleId)) { throw new ArgumentException("Factory module identity is required.", nameof(moduleId)); }
                Register(deserializer);
                installed = true;
                ModuleOwnersByTypeId.Add(deserializer.ComponentTypeId, moduleId);
                registered = true;
            } finally {
                if (!registered) {
                    if (installed) { DeserializersByTypeId.Remove(deserializer.ComponentTypeId); }
                    else { NativeOwnership.Delete(deserializer); }
                }
            }
        }

        /// <summary>Removes and destroys every factory implemented by a module before its executable allocation is released.</summary>
        /// <param name="moduleId">Stable owning module identity.</param>
        public void UnregisterModule(string moduleId) {
            if (string.IsNullOrWhiteSpace(moduleId)) { throw new ArgumentException("Factory module identity is required.", nameof(moduleId)); }
            List<string> removedIds = new List<string>();
            try {
                foreach (string typeId in ModuleOwnersByTypeId.Keys) {
                    if (string.Equals(ModuleOwnersByTypeId[typeId], moduleId, StringComparison.OrdinalIgnoreCase)) { removedIds.Add(typeId); }
                }
                for (int index = 0; index < removedIds.Count; index++) {
                    string typeId = removedIds[index];
                    DeserializersByTypeId.Remove(typeId);
                    ModuleOwnersByTypeId.Remove(typeId);
                }
            } finally { NativeOwnership.Delete(removedIds); }
        }

        /// <summary>Destroys remaining resident factories after scene and module teardown has completed.</summary>
        public void Dispose() {
            NativeOwnership.Release(ref DeserializersByTypeId);
            NativeOwnership.Release(ref ModuleOwnersByTypeId);
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
        /// <returns>A registry-owned deserializer borrowed by the caller.</returns>
        [NativeBorrowedReturn]
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

            Type componentType = PersistedComponentTypeResolver.TryResolve(componentTypeId);
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

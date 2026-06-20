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
            return registry.DeserializersByTypeId.Keys.ToArray();
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

            registry.Register(new RuntimeMeshComponentDeserializer());
            registry.Register(new RuntimeCameraComponentDeserializer());
            registry.Register(new RuntimeFPSComponentDeserializer());
            registry.Register(new RuntimeDebugComponentDeserializer());
            registry.Register(new RuntimeTextComponentDeserializer());
            registry.Register(new RuntimeSpriteComponentDeserializer());
            registry.Register(new RuntimeRoundedRectComponentDeserializer());
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
                string normalizedEngineComponentTypeId = NormalizeLegacyEngineComponentTypeId(componentTypeId);
                if (!string.Equals(normalizedEngineComponentTypeId, componentTypeId, StringComparison.Ordinal)
                    && DeserializersByTypeId.TryGetValue(normalizedEngineComponentTypeId, out deserializer)) {
                    return deserializer;
                }
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

        /// <summary>
        /// Normalizes legacy assembly-qualified engine component ids back to their short engine ids so packaged scenes saved before the reflected-persistence migration can still resolve generated runtime deserializers.
        /// </summary>
        /// <param name="componentTypeId">Serialized component type id under evaluation.</param>
        /// <returns>Short engine component type id when the supplied identifier uses the legacy engine assembly-qualified form; otherwise the original identifier.</returns>
        static string NormalizeLegacyEngineComponentTypeId(string componentTypeId) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                return componentTypeId;
            }

            switch (componentTypeId) {
                case "helengine.AmbientLightComponent, helengine.core":
                    return "helengine.AmbientLightComponent";
                case "helengine.AnchorComponent":
                    return "helengine.LayoutComponent";
                case "helengine.AnchorComponent, helengine.core":
                    return "helengine.LayoutComponent";
                case "helengine.AnimationPlayerComponent, helengine.core":
                    return "helengine.AnimationPlayerComponent";
                case "helengine.CameraComponent, helengine.core":
                    return "helengine.CameraComponent";
                case "helengine.ClipRectComponent, helengine.core":
                    return "helengine.ClipRectComponent";
                case "helengine.DebugComponent, helengine.core":
                    return "helengine.DebugComponent";
                case "helengine.DirectionalLightComponent, helengine.core":
                    return "helengine.DirectionalLightComponent";
                case "helengine.FPSComponent, helengine.core":
                    return "helengine.FPSComponent";
                case "helengine.InteractableComponent, helengine.core":
                    return "helengine.InteractableComponent";
                case "helengine.LineRendererComponent, helengine.core":
                    return "helengine.LineRendererComponent";
                case "helengine.MeshComponent, helengine.core":
                    return "helengine.MeshComponent";
                case "helengine.PointLightComponent, helengine.core":
                    return "helengine.PointLightComponent";
                case "helengine.ReferenceCanvasFitComponent, helengine.core":
                    return "helengine.ReferenceCanvasFitComponent";
                case "helengine.RoundedRectComponent, helengine.core":
                    return "helengine.RoundedRectComponent";
                case "helengine.SceneMapComponent, helengine.core":
                    return "helengine.SceneMapComponent";
                case "helengine.SceneMemoryProbeComponent, helengine.core":
                    return "helengine.SceneMemoryProbeComponent";
                case "helengine.ScrollComponent, helengine.core":
                    return "helengine.ScrollComponent";
                case "helengine.SpotLightComponent, helengine.core":
                    return "helengine.SpotLightComponent";
                case "helengine.SpriteComponent, helengine.core":
                    return "helengine.SpriteComponent";
                case "helengine.TextComponent, helengine.core":
                    return "helengine.TextComponent";
                case "helengine.ViewportComponent, helengine.core":
                    return "helengine.ViewportComponent";
                case "helengine.BoxCollider3DComponent, helengine.physics3d":
                    return "helengine.BoxCollider3DComponent";
                case "helengine.CharacterController3DComponent, helengine.physics3d":
                    return "helengine.CharacterController3DComponent";
                case "helengine.KinematicMotion3DComponent, helengine.physics3d":
                    return "helengine.KinematicMotion3DComponent";
                case "helengine.RigidBody3DComponent, helengine.physics3d":
                    return "helengine.RigidBody3DComponent";
                default:
                    return componentTypeId;
            }
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

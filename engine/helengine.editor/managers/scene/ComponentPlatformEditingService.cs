using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Builds and persists editor-only component overrides for one selected target platform.
    /// </summary>
    public sealed class ComponentPlatformEditingService {
        /// <summary>
        /// Stable platform id used by the common shared component state.
        /// </summary>
        public const string CommonPlatformId = "common";

        /// <summary>
        /// Registered component persistence descriptors used to serialize override payloads.
        /// </summary>
        readonly ComponentPersistenceRegistry PersistenceRegistry;
        /// <summary>
        /// Cached editable override component instances keyed by their common component and platform id.
        /// </summary>
        readonly Dictionary<Component, Dictionary<string, Component>> OverrideComponentsByCommonComponent;

        /// <summary>
        /// Initializes a new platform editing service using the default editor persistence registry.
        /// </summary>
        public ComponentPlatformEditingService() {
            PersistenceRegistry = CreatePersistenceRegistry();
            OverrideComponentsByCommonComponent = new Dictionary<Component, Dictionary<string, Component>>();
        }

        /// <summary>
        /// Resolves the effective component instance that should be edited for the supplied platform.
        /// </summary>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="platformId">Target platform being edited.</param>
        /// <returns>The effective editable component for the supplied platform.</returns>
        public Component ResolveEditableComponent(Component commonComponent, EntitySaveComponent saveComponent, string platformId) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (string.Equals(platformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return commonComponent;
            }

            Component cachedOverrideComponent = GetCachedOverrideComponent(commonComponent, platformId);
            if (cachedOverrideComponent != null) {
                return cachedOverrideComponent;
            }

            if (!saveComponent.TryGetComponentState(commonComponent, out EntityComponentSaveState saveState)) {
                return commonComponent;
            }

            if (!saveState.TryGetPlatformOverride(platformId, out EntityComponentPlatformOverrideState overrideState)) {
                return commonComponent;
            }

            Component deserializedOverrideComponent = DeserializeOverrideComponent(commonComponent, overrideState);
            if (deserializedOverrideComponent == null) {
                return commonComponent;
            }

            CacheOverrideComponent(commonComponent, platformId, deserializedOverrideComponent);
            return deserializedOverrideComponent;
        }

        /// <summary>
        /// Ensures an editable override component exists for the supplied platform.
        /// </summary>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="platformId">Target platform being edited.</param>
        /// <returns>Editable override component for the supplied platform.</returns>
        public Component EnsurePlatformOverrideComponent(Component commonComponent, EntitySaveComponent saveComponent, string platformId) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (string.Equals(platformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return commonComponent;
            }

            Component editableComponent = ResolveEditableComponent(commonComponent, saveComponent, platformId);
            if (!ReferenceEquals(editableComponent, commonComponent)) {
                return editableComponent;
            }

            Component clonedComponent = CloneComponent(commonComponent);
            CacheOverrideComponent(commonComponent, platformId, clonedComponent);
            PersistPlatformOverride(commonComponent, clonedComponent, saveComponent, platformId);
            return clonedComponent;
        }

        /// <summary>
        /// Persists the current editable override component payload for the supplied platform.
        /// </summary>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="overrideComponent">Editable override component that should be persisted.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="platformId">Target platform being edited.</param>
        public void PersistPlatformOverride(Component commonComponent, Component overrideComponent, EntitySaveComponent saveComponent, string platformId) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            } else if (overrideComponent == null) {
                throw new ArgumentNullException(nameof(overrideComponent));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (string.Equals(platformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Common component state should not be persisted as a platform override.");
            }

            EntityComponentSaveState componentSaveState = saveComponent.GetOrCreateComponentState(commonComponent);
            EntityComponentSaveState effectiveOverrideSaveState = BuildEffectiveOverrideSaveState(componentSaveState, platformId);
            IComponentPersistenceDescriptor descriptor = PersistenceRegistry.GetDescriptor(overrideComponent);
            SceneComponentAssetRecord record = descriptor.SerializeComponent(overrideComponent, 0, effectiveOverrideSaveState);

            EntityComponentPlatformOverrideState overrideState = new EntityComponentPlatformOverrideState {
                PlatformId = platformId,
                Payload = record.Payload
            };

            foreach (KeyValuePair<string, SceneAssetReference> assetReferenceEntry in effectiveOverrideSaveState.EnumerateNamedAssetReferences()) {
                overrideState.SetAssetReference(assetReferenceEntry.Key, assetReferenceEntry.Value);
            }

            componentSaveState.SetPlatformOverride(platformId, overrideState);
            CacheOverrideComponent(commonComponent, platformId, overrideComponent);
        }

        /// <summary>
        /// Stores a stable asset reference in the effective component state for the supplied platform.
        /// </summary>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="editableComponent">Editable component that owns the updated property.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="platformId">Target platform being edited.</param>
        /// <param name="referenceName">Stable property reference slot name.</param>
        /// <param name="assetReference">Stable asset reference assigned to the property.</param>
        public void StoreAssetReference(
            Component commonComponent,
            Component editableComponent,
            EntitySaveComponent saveComponent,
            string platformId,
            string referenceName,
            SceneAssetReference assetReference) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            } else if (editableComponent == null) {
                throw new ArgumentNullException(nameof(editableComponent));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (string.IsNullOrWhiteSpace(referenceName)) {
                throw new ArgumentException("Reference name must be provided.", nameof(referenceName));
            } else if (assetReference == null) {
                throw new ArgumentNullException(nameof(assetReference));
            }

            if (string.Equals(platformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                saveComponent.SetAssetReference(commonComponent, referenceName, assetReference);
                return;
            }

            EntityComponentSaveState componentSaveState = saveComponent.GetOrCreateComponentState(commonComponent);
            EntityComponentPlatformOverrideState overrideState = componentSaveState.GetOrCreatePlatformOverride(platformId);
            overrideState.SetAssetReference(referenceName, assetReference);
            PersistPlatformOverride(commonComponent, editableComponent, saveComponent, platformId);
        }

        /// <summary>
        /// Creates the standard editor component persistence registry used by scene save and load.
        /// </summary>
        /// <returns>Initialized persistence registry.</returns>
        ComponentPersistenceRegistry CreatePersistenceRegistry() {
            ComponentPersistenceRegistry persistenceRegistry = new ComponentPersistenceRegistry();
            persistenceRegistry.Register(new MeshComponentPersistenceDescriptor());
            persistenceRegistry.Register(new CameraComponentPersistenceDescriptor());
            persistenceRegistry.Register(new TextComponentPersistenceDescriptor());
            persistenceRegistry.Register(new RoundedRectComponentPersistenceDescriptor());
            persistenceRegistry.Register(new FPSComponentPersistenceDescriptor());
            persistenceRegistry.Register(new DirectionalLightComponentPersistenceDescriptor());
            persistenceRegistry.Register(new PointLightComponentPersistenceDescriptor());
            persistenceRegistry.Register(new SpotLightComponentPersistenceDescriptor());
            persistenceRegistry.Register(new MenuComponentPersistenceDescriptor());
            persistenceRegistry.Register(new MenuPanelComponentPersistenceDescriptor());
            persistenceRegistry.Register(new MenuItemComponentPersistenceDescriptor());
            persistenceRegistry.Register(new MenuSelectedDescriptionComponentPersistenceDescriptor());
            return persistenceRegistry;
        }

        /// <summary>
        /// Creates a detached component clone by copying its public readable and writable properties.
        /// </summary>
        /// <param name="sourceComponent">Component instance that should be cloned.</param>
        /// <returns>Detached component clone populated with the same public property values.</returns>
        Component CloneComponent(Component sourceComponent) {
            if (sourceComponent == null) {
                throw new ArgumentNullException(nameof(sourceComponent));
            }

            Component clonedComponent = Activator.CreateInstance(sourceComponent.GetType()) as Component;
            if (clonedComponent == null) {
                throw new InvalidOperationException($"Component type '{sourceComponent.GetType().FullName}' could not be instantiated for platform override editing.");
            }

            PropertyInfo[] properties = sourceComponent.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            for (int index = 0; index < properties.Length; index++) {
                PropertyInfo property = properties[index];
                if (!property.CanRead || !property.CanWrite) {
                    continue;
                }
                if (property.GetIndexParameters().Length != 0) {
                    continue;
                }
                if (string.Equals(property.Name, nameof(Component.Parent), StringComparison.Ordinal)) {
                    continue;
                }
                if (property.GetMethod == null || property.SetMethod == null) {
                    continue;
                }
                if (!property.GetMethod.IsPublic || !property.SetMethod.IsPublic) {
                    continue;
                }

                object propertyValue = property.GetValue(sourceComponent);
                property.SetValue(clonedComponent, propertyValue);
            }

            return clonedComponent;
        }

        /// <summary>
        /// Builds the effective save-state used to serialize one fully independent platform override.
        /// </summary>
        /// <param name="componentSaveState">Component save-state that owns the common and platform metadata.</param>
        /// <param name="platformId">Platform identifier whose effective references should be gathered.</param>
        /// <returns>Effective save-state containing the references required by the override.</returns>
        EntityComponentSaveState BuildEffectiveOverrideSaveState(EntityComponentSaveState componentSaveState, string platformId) {
            if (componentSaveState == null) {
                throw new ArgumentNullException(nameof(componentSaveState));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            EntityComponentSaveState effectiveSaveState = new EntityComponentSaveState();
            foreach (KeyValuePair<string, SceneAssetReference> assetReferenceEntry in componentSaveState.EnumerateNamedAssetReferences()) {
                effectiveSaveState.SetAssetReference(assetReferenceEntry.Key, assetReferenceEntry.Value);
            }

            if (componentSaveState.TryGetPlatformOverride(platformId, out EntityComponentPlatformOverrideState overrideState)) {
                foreach (KeyValuePair<string, SceneAssetReference> assetReferenceEntry in overrideState.EnumerateNamedAssetReferences()) {
                    effectiveSaveState.SetAssetReference(assetReferenceEntry.Key, assetReferenceEntry.Value);
                }
            }

            return effectiveSaveState;
        }

        /// <summary>
        /// Deserializes one stored platform override payload back into a detached editable component.
        /// </summary>
        /// <param name="commonComponent">Common live component whose override payload should be materialized.</param>
        /// <param name="overrideState">Stored override payload metadata.</param>
        /// <returns>Detached editable component when deserialization succeeds; otherwise null.</returns>
        Component DeserializeOverrideComponent(Component commonComponent, EntityComponentPlatformOverrideState overrideState) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            } else if (overrideState == null) {
                throw new ArgumentNullException(nameof(overrideState));
            }

            IComponentPersistenceDescriptor descriptor = PersistenceRegistry.GetDescriptor(commonComponent);
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = descriptor.ComponentTypeId,
                ComponentIndex = 0,
                Payload = overrideState.Payload ?? Array.Empty<byte>()
            };

            EntitySaveComponent deserializedSaveComponent = new EntitySaveComponent();
            try {
                return descriptor.DeserializeComponent(record, deserializedSaveComponent, new ThrowingSceneAssetReferenceResolver());
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Retrieves one cached platform override component when it already exists in the current editor session.
        /// </summary>
        /// <param name="commonComponent">Common live component that owns the override.</param>
        /// <param name="platformId">Platform identifier whose override should be returned.</param>
        /// <returns>Cached override component when one exists; otherwise null.</returns>
        Component GetCachedOverrideComponent(Component commonComponent, string platformId) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (!OverrideComponentsByCommonComponent.TryGetValue(commonComponent, out Dictionary<string, Component> overridesByPlatformId)) {
                return null;
            }

            if (!overridesByPlatformId.TryGetValue(platformId, out Component overrideComponent)) {
                return null;
            }

            return overrideComponent;
        }

        /// <summary>
        /// Stores one editable override component in the current editor-session cache.
        /// </summary>
        /// <param name="commonComponent">Common live component that owns the override.</param>
        /// <param name="platformId">Platform identifier whose override is being cached.</param>
        /// <param name="overrideComponent">Detached editable override component to cache.</param>
        void CacheOverrideComponent(Component commonComponent, string platformId, Component overrideComponent) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (overrideComponent == null) {
                throw new ArgumentNullException(nameof(overrideComponent));
            }

            if (!OverrideComponentsByCommonComponent.TryGetValue(commonComponent, out Dictionary<string, Component> overridesByPlatformId)) {
                overridesByPlatformId = new Dictionary<string, Component>(StringComparer.OrdinalIgnoreCase);
                OverrideComponentsByCommonComponent.Add(commonComponent, overridesByPlatformId);
            }

            overridesByPlatformId[platformId] = overrideComponent;
        }

        /// <summary>
        /// Resolver used when detached override deserialization unexpectedly requests runtime asset reconstruction.
        /// </summary>
        sealed class ThrowingSceneAssetReferenceResolver : ISceneAssetReferenceResolver {
            /// <summary>
            /// Rejects unexpected runtime model resolution for detached override materialization.
            /// </summary>
            /// <param name="reference">Reference that was unexpectedly requested.</param>
            /// <returns>This method never returns.</returns>
            public RuntimeModel ResolveModel(SceneAssetReference reference) {
                throw new InvalidOperationException("Detached platform override editing does not support model asset reconstruction through the scene resolver.");
            }

            /// <summary>
            /// Rejects unexpected runtime material resolution for detached override materialization.
            /// </summary>
            /// <param name="reference">Reference that was unexpectedly requested.</param>
            /// <returns>This method never returns.</returns>
            public RuntimeMaterial ResolveMaterial(SceneAssetReference reference) {
                throw new InvalidOperationException("Detached platform override editing does not support material asset reconstruction through the scene resolver.");
            }

            /// <summary>
            /// Rejects unexpected runtime font resolution for detached override materialization.
            /// </summary>
            /// <param name="reference">Reference that was unexpectedly requested.</param>
            /// <returns>This method never returns.</returns>
            public FontAsset ResolveFont(SceneAssetReference reference) {
                throw new InvalidOperationException("Detached platform override editing does not support font asset reconstruction through the scene resolver.");
            }
        }
    }
}

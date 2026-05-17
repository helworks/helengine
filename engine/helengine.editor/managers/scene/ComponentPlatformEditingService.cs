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
        /// Cached platform override snapshot components keyed by their common component and platform id.
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

            if (!saveComponent.TryGetComponentState(commonComponent, out EntityComponentSaveState saveState)) {
                return commonComponent;
            }

            if (!saveState.TryGetPlatformOverride(platformId, out EntityComponentPlatformOverrideState overrideState)) {
                return commonComponent;
            }

            Component overrideSnapshotComponent = GetOrLoadOverrideSnapshotComponent(commonComponent, platformId, overrideState);
            if (overrideSnapshotComponent == null) {
                return commonComponent;
            }

            if (!overrideState.HasAnyPropertyOverrides) {
                return overrideSnapshotComponent;
            }

            return BuildEditableComponent(commonComponent, overrideSnapshotComponent, overrideState);
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

            return CloneComponent(commonComponent);
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

            EntityComponentPlatformOverrideState overrideState = GetOrCreatePlatformOverrideState(componentSaveState, platformId);
            overrideState.Payload = record.Payload;
            ReplaceOverrideAssetReferences(overrideState, effectiveOverrideSaveState);
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
        /// Marks one property path as explicitly overridden for the supplied component platform payload.
        /// </summary>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="platformId">Target platform being edited.</param>
        /// <param name="propertyPath">Stable property path that was edited.</param>
        public void MarkPropertyOverride(Component commonComponent, EntitySaveComponent saveComponent, string platformId, string propertyPath) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (string.IsNullOrWhiteSpace(propertyPath)) {
                throw new ArgumentException("Property path must be provided.", nameof(propertyPath));
            }

            if (string.Equals(platformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            EntityComponentSaveState componentSaveState = saveComponent.GetOrCreateComponentState(commonComponent);
            EntityComponentPlatformOverrideState overrideState = GetOrCreatePlatformOverrideState(componentSaveState, platformId);
            overrideState.SetPropertyOverride(propertyPath);
        }

        /// <summary>
        /// Returns whether one property path is explicitly overridden for the supplied component platform payload.
        /// </summary>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="editableComponent">Effective editable component shown for the current platform.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="platformId">Target platform being edited.</param>
        /// <param name="propertyPath">Stable property path to query.</param>
        /// <returns>True when the property path is overridden for the target platform.</returns>
        public bool IsPropertyOverrideActive(
            Component commonComponent,
            Component editableComponent,
            EntitySaveComponent saveComponent,
            string platformId,
            string propertyPath) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            } else if (editableComponent == null) {
                throw new ArgumentNullException(nameof(editableComponent));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (string.IsNullOrWhiteSpace(propertyPath)) {
                throw new ArgumentException("Property path must be provided.", nameof(propertyPath));
            }

            if (string.Equals(platformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
            if (!saveComponent.TryGetComponentState(commonComponent, out EntityComponentSaveState componentSaveState)) {
                return false;
            }
            if (!componentSaveState.TryGetPlatformOverride(platformId, out EntityComponentPlatformOverrideState overrideState)) {
                return false;
            }
            if (overrideState.HasAnyPropertyOverrides) {
                return overrideState.HasPropertyOverride(propertyPath);
            }

            return !object.Equals(
                ReadPropertyPathValue(commonComponent, propertyPath),
                ReadPropertyPathValue(editableComponent, propertyPath));
        }

        /// <summary>
        /// Clears one explicit property override marker from the supplied component platform payload.
        /// </summary>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="platformId">Target platform whose property override should be cleared.</param>
        /// <param name="propertyPath">Stable property path that should return to common behavior.</param>
        public void ClearPropertyOverride(Component commonComponent, EntitySaveComponent saveComponent, string platformId, string propertyPath) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (string.IsNullOrWhiteSpace(propertyPath)) {
                throw new ArgumentException("Property path must be provided.", nameof(propertyPath));
            }

            if (string.Equals(platformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return;
            }
            if (!saveComponent.TryGetComponentState(commonComponent, out EntityComponentSaveState componentSaveState)) {
                return;
            }
            if (!componentSaveState.TryGetPlatformOverride(platformId, out EntityComponentPlatformOverrideState overrideState)) {
                return;
            }

            overrideState.ClearPropertyOverride(propertyPath);
            string assetReferenceName = TryResolveAssetReferenceName(propertyPath);
            if (!string.IsNullOrWhiteSpace(assetReferenceName)) {
                overrideState.RemoveAssetReference(assetReferenceName);
            }

            if (!overrideState.HasAnyPropertyOverrides && !overrideState.HasAnyAssetReferences) {
                componentSaveState.RemovePlatformOverride(platformId);
                ClearCachedOverrideComponent(commonComponent, platformId);
            }
        }

        /// <summary>
        /// Ensures one stable editor component key exists for the supplied common component.
        /// </summary>
        /// <param name="component">Common live component attached to the entity.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <returns>Stable editor component key.</returns>
        public string EnsureComponentKey(Component component, EntitySaveComponent saveComponent) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            EntityComponentSaveState saveState = saveComponent.GetOrCreateComponentState(component);
            if (string.IsNullOrWhiteSpace(saveState.ComponentKey)) {
                saveState.ComponentKey = Guid.NewGuid().ToString("N");
            }

            return saveState.ComponentKey;
        }

        /// <summary>
        /// Returns whether one common live component is removed for the supplied platform.
        /// </summary>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="platformId">Target platform being edited.</param>
        /// <returns>True when the common component is removed for the supplied platform.</returns>
        public bool IsComponentRemoved(Component commonComponent, EntitySaveComponent saveComponent, string platformId) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (string.Equals(platformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
            if (!saveComponent.TryGetComponentPlatformOverride(platformId, out EntityPlatformComponentOverrideState platformOverrideState)) {
                return false;
            }

            return platformOverrideState.IsComponentRemoved(EnsureComponentKey(commonComponent, saveComponent));
        }

        /// <summary>
        /// Returns the detached platform-only components authored for the supplied platform.
        /// </summary>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="platformId">Target platform being edited.</param>
        /// <returns>Detached platform-only component states authored for the platform.</returns>
        public IReadOnlyList<EntityPlatformAddedComponentState> GetAddedComponents(EntitySaveComponent saveComponent, string platformId) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (string.Equals(platformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return Array.Empty<EntityPlatformAddedComponentState>();
            }
            if (!saveComponent.TryGetComponentPlatformOverride(platformId, out EntityPlatformComponentOverrideState platformOverrideState)) {
                return Array.Empty<EntityPlatformAddedComponentState>();
            }

            List<EntityPlatformAddedComponentState> addedComponents = new List<EntityPlatformAddedComponentState>();
            foreach (EntityPlatformAddedComponentState addedComponentState in platformOverrideState.EnumerateAddedComponents()) {
                if (addedComponentState == null || addedComponentState.Component == null || addedComponentState.SaveState == null) {
                    continue;
                }

                addedComponents.Add(addedComponentState);
            }

            return addedComponents;
        }

        /// <summary>
        /// Adds one detached platform-only component for the supplied platform without mutating the common live entity component list.
        /// </summary>
        /// <param name="descriptor">Descriptor that defines the component type to add.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="platformId">Target platform being edited.</param>
        /// <returns>Detached platform-only component state.</returns>
        public EntityPlatformAddedComponentState AddPlatformOnlyComponent(EditorComponentAddDescriptor descriptor, EntitySaveComponent saveComponent, string platformId) {
            if (descriptor == null) {
                throw new ArgumentNullException(nameof(descriptor));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (string.Equals(platformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Platform-only components cannot be added on the common tab.");
            }

            Component detachedComponent = descriptor.CreateComponentInstance();
            EntityComponentSaveState addedComponentSaveState = new EntityComponentSaveState {
                ComponentKey = Guid.NewGuid().ToString("N")
            };
            EntityPlatformAddedComponentState addedComponentState = new EntityPlatformAddedComponentState {
                ComponentKey = addedComponentSaveState.ComponentKey,
                Component = detachedComponent,
                SaveState = addedComponentSaveState
            };

            saveComponent.GetOrCreateComponentPlatformOverride(platformId).SetAddedComponent(addedComponentState);
            return addedComponentState;
        }

        /// <summary>
        /// Removes one component for the supplied platform without mutating unrelated common component state.
        /// </summary>
        /// <param name="component">Component being removed from the active platform view.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="platformId">Target platform being edited.</param>
        /// <returns>True when the component still exists on common and was hidden only for the platform; otherwise false.</returns>
        public bool RemoveComponent(Component component, EntitySaveComponent saveComponent, string platformId) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (string.Equals(platformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Platform-specific remove behavior is not valid on the common tab.");
            }

            EntityPlatformComponentOverrideState platformOverrideState = saveComponent.GetOrCreateComponentPlatformOverride(platformId);
            EntityPlatformAddedComponentState addedComponentState = FindAddedComponentState(platformOverrideState, component);
            if (addedComponentState != null) {
                platformOverrideState.RemoveAddedComponent(addedComponentState.ComponentKey);
                RemoveEmptyComponentPlatformOverride(saveComponent, platformId, platformOverrideState);
                return false;
            }

            EntityComponentSaveState componentSaveState = saveComponent.GetOrCreateComponentState(component);
            string componentKey = EnsureComponentKey(component, saveComponent);
            platformOverrideState.MarkComponentRemoved(componentKey);
            componentSaveState.RemovePlatformOverride(platformId);
            ClearCachedOverrideComponent(component, platformId);
            RemoveEmptyComponentPlatformOverride(saveComponent, platformId, platformOverrideState);
            return true;
        }

        /// <summary>
        /// Reverts one component existence override back to common behavior for the supplied platform.
        /// </summary>
        /// <param name="component">Component whose existence override should be reverted.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="platformId">Target platform being edited.</param>
        public void RevertComponentExistenceOverride(Component component, EntitySaveComponent saveComponent, string platformId) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (!saveComponent.TryGetComponentPlatformOverride(platformId, out EntityPlatformComponentOverrideState platformOverrideState)) {
                return;
            }

            EntityPlatformAddedComponentState addedComponentState = FindAddedComponentState(platformOverrideState, component);
            if (addedComponentState != null) {
                platformOverrideState.RemoveAddedComponent(addedComponentState.ComponentKey);
                RemoveEmptyComponentPlatformOverride(saveComponent, platformId, platformOverrideState);
                return;
            }

            if (!saveComponent.TryGetComponentState(component, out EntityComponentSaveState componentSaveState)) {
                return;
            }

            if (!string.IsNullOrWhiteSpace(componentSaveState.ComponentKey)) {
                platformOverrideState.RestoreRemovedComponent(componentSaveState.ComponentKey);
            }

            RemoveEmptyComponentPlatformOverride(saveComponent, platformId, platformOverrideState);
        }

        /// <summary>
        /// Attempts to resolve one detached platform-only component state from the supplied entity-level component overrides.
        /// </summary>
        /// <param name="component">Detached component instance to resolve.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="platformId">Target platform whose detached component state should be queried.</param>
        /// <param name="addedComponentState">Resolved detached component state when one exists.</param>
        /// <returns>True when the platform owns one detached state for the supplied component instance.</returns>
        public bool TryGetAddedComponentState(
            Component component,
            EntitySaveComponent saveComponent,
            string platformId,
            out EntityPlatformAddedComponentState addedComponentState) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            addedComponentState = null;
            if (string.Equals(platformId, CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
            if (!saveComponent.TryGetComponentPlatformOverride(platformId, out EntityPlatformComponentOverrideState platformOverrideState)) {
                return false;
            }

            addedComponentState = FindAddedComponentState(platformOverrideState, component);
            return addedComponentState != null;
        }

        /// <summary>
        /// Stores a stable asset reference in the detached save-state for one platform-only added component.
        /// </summary>
        /// <param name="component">Detached platform-only component that owns the updated asset property.</param>
        /// <param name="saveComponent">Hidden save component that stores entity-level platform overrides.</param>
        /// <param name="platformId">Target platform being edited.</param>
        /// <param name="referenceName">Stable property reference slot name.</param>
        /// <param name="assetReference">Stable asset reference assigned to the property.</param>
        public void StoreAddedComponentAssetReference(
            Component component,
            EntitySaveComponent saveComponent,
            string platformId,
            string referenceName,
            SceneAssetReference assetReference) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (string.IsNullOrWhiteSpace(referenceName)) {
                throw new ArgumentException("Reference name must be provided.", nameof(referenceName));
            } else if (assetReference == null) {
                throw new ArgumentNullException(nameof(assetReference));
            }

            if (!TryGetAddedComponentState(component, saveComponent, platformId, out EntityPlatformAddedComponentState addedComponentState)) {
                throw new InvalidOperationException("Detached platform-only component asset references require a tracked added component state.");
            }

            addedComponentState.SaveState.SetAssetReference(referenceName, assetReference);
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
            persistenceRegistry.Register(new DebugComponentPersistenceDescriptor());
            persistenceRegistry.Register(new DirectionalLightComponentPersistenceDescriptor());
            persistenceRegistry.Register(new AmbientLightComponentPersistenceDescriptor());
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
        /// Builds the effective editable component by overlaying the explicitly overridden properties onto a fresh clone of the common component.
        /// </summary>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="overrideSnapshotComponent">Detached snapshot component that stores the overridden values.</param>
        /// <param name="overrideState">Platform override metadata that enumerates which properties are explicitly overridden.</param>
        /// <returns>Editable component composed from the common component plus the explicit overrides.</returns>
        Component BuildEditableComponent(
            Component commonComponent,
            Component overrideSnapshotComponent,
            EntityComponentPlatformOverrideState overrideState) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            } else if (overrideSnapshotComponent == null) {
                throw new ArgumentNullException(nameof(overrideSnapshotComponent));
            } else if (overrideState == null) {
                throw new ArgumentNullException(nameof(overrideState));
            }

            Component editableComponent = CloneComponent(commonComponent);
            ApplyExplicitPropertyOverrides(editableComponent, overrideSnapshotComponent, overrideState);
            return editableComponent;
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
        /// Applies every explicit property override stored in one platform payload onto the supplied editable component.
        /// </summary>
        /// <param name="editableComponent">Editable component that should receive the override values.</param>
        /// <param name="overrideSnapshotComponent">Detached snapshot component that stores the override values.</param>
        /// <param name="overrideState">Platform override metadata that enumerates which properties are explicitly overridden.</param>
        void ApplyExplicitPropertyOverrides(
            Component editableComponent,
            Component overrideSnapshotComponent,
            EntityComponentPlatformOverrideState overrideState) {
            if (editableComponent == null) {
                throw new ArgumentNullException(nameof(editableComponent));
            } else if (overrideSnapshotComponent == null) {
                throw new ArgumentNullException(nameof(overrideSnapshotComponent));
            } else if (overrideState == null) {
                throw new ArgumentNullException(nameof(overrideState));
            }

            foreach (string propertyPath in overrideState.EnumeratePropertyOverrides()) {
                WritePropertyPathValue(editableComponent, propertyPath, ReadPropertyPathValue(overrideSnapshotComponent, propertyPath));
            }
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
        /// Resolves the cached or deserialized snapshot component that stores the persisted override values for one platform.
        /// </summary>
        /// <param name="commonComponent">Common live component that owns the override.</param>
        /// <param name="platformId">Platform identifier whose override snapshot should be resolved.</param>
        /// <param name="overrideState">Stored override payload metadata.</param>
        /// <returns>Detached snapshot component when one can be materialized; otherwise null.</returns>
        Component GetOrLoadOverrideSnapshotComponent(Component commonComponent, string platformId, EntityComponentPlatformOverrideState overrideState) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (overrideState == null) {
                throw new ArgumentNullException(nameof(overrideState));
            }

            Component cachedOverrideComponent = GetCachedOverrideComponent(commonComponent, platformId);
            if (cachedOverrideComponent != null) {
                return cachedOverrideComponent;
            }

            Component deserializedOverrideComponent = DeserializeOverrideComponent(commonComponent, overrideState);
            if (deserializedOverrideComponent == null) {
                return null;
            }

            CacheOverrideComponent(commonComponent, platformId, deserializedOverrideComponent);
            return deserializedOverrideComponent;
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
        /// Clears one cached platform override snapshot from the current editor session.
        /// </summary>
        /// <param name="commonComponent">Common live component that owns the override snapshot.</param>
        /// <param name="platformId">Platform identifier whose cached snapshot should be cleared.</param>
        void ClearCachedOverrideComponent(Component commonComponent, string platformId) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (!OverrideComponentsByCommonComponent.TryGetValue(commonComponent, out Dictionary<string, Component> overridesByPlatformId)) {
                return;
            }

            overridesByPlatformId.Remove(platformId);
            if (overridesByPlatformId.Count < 1) {
                OverrideComponentsByCommonComponent.Remove(commonComponent);
            }
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
        /// Replaces the stored asset-reference set on one platform override payload with the effective references supplied by the save-state.
        /// </summary>
        /// <param name="overrideState">Platform override payload whose asset references should be replaced.</param>
        /// <param name="effectiveOverrideSaveState">Effective save-state that supplies the current asset references.</param>
        void ReplaceOverrideAssetReferences(EntityComponentPlatformOverrideState overrideState, EntityComponentSaveState effectiveOverrideSaveState) {
            if (overrideState == null) {
                throw new ArgumentNullException(nameof(overrideState));
            } else if (effectiveOverrideSaveState == null) {
                throw new ArgumentNullException(nameof(effectiveOverrideSaveState));
            }

            List<string> existingReferenceNames = new List<string>();
            foreach (KeyValuePair<string, SceneAssetReference> assetReference in overrideState.EnumerateNamedAssetReferences()) {
                existingReferenceNames.Add(assetReference.Key);
            }

            for (int index = 0; index < existingReferenceNames.Count; index++) {
                overrideState.RemoveAssetReference(existingReferenceNames[index]);
            }

            foreach (KeyValuePair<string, SceneAssetReference> assetReferenceEntry in effectiveOverrideSaveState.EnumerateNamedAssetReferences()) {
                overrideState.SetAssetReference(assetReferenceEntry.Key, assetReferenceEntry.Value);
            }
        }

        /// <summary>
        /// Resolves the mutable platform override state for the supplied component and platform, creating it when needed.
        /// </summary>
        /// <param name="componentSaveState">Component save-state that owns the override metadata.</param>
        /// <param name="platformId">Platform identifier whose override state should be returned.</param>
        /// <returns>Mutable platform override state for the supplied component and platform.</returns>
        EntityComponentPlatformOverrideState GetOrCreatePlatformOverrideState(EntityComponentSaveState componentSaveState, string platformId) {
            if (componentSaveState == null) {
                throw new ArgumentNullException(nameof(componentSaveState));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (componentSaveState.TryGetPlatformOverride(platformId, out EntityComponentPlatformOverrideState existingOverrideState)) {
                return existingOverrideState;
            }

            EntityComponentPlatformOverrideState overrideState = new EntityComponentPlatformOverrideState {
                PlatformId = platformId
            };
            componentSaveState.SetPlatformOverride(platformId, overrideState);
            return overrideState;
        }

        /// <summary>
        /// Reads one value from the supplied component using a stable property path.
        /// </summary>
        /// <param name="target">Component that owns the requested property path.</param>
        /// <param name="propertyPath">Stable property path to read.</param>
        /// <returns>Value read from the supplied property path.</returns>
        object ReadPropertyPathValue(object target, string propertyPath) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            } else if (string.IsNullOrWhiteSpace(propertyPath)) {
                throw new ArgumentException("Property path must be provided.", nameof(propertyPath));
            }

            int separatorIndex = propertyPath.IndexOf('.');
            if (separatorIndex < 0) {
                PropertyInfo directProperty = ResolveRequiredProperty(target.GetType(), propertyPath);
                return directProperty.GetValue(target);
            }

            string parentPropertyName = propertyPath.Substring(0, separatorIndex);
            string nestedPropertyName = propertyPath.Substring(separatorIndex + 1);
            PropertyInfo parentProperty = ResolveRequiredProperty(target.GetType(), parentPropertyName);
            object parentValue = parentProperty.GetValue(target);
            if (parentValue == null) {
                throw new InvalidOperationException($"Property '{parentPropertyName}' on '{target.GetType().FullName}' is null and cannot resolve nested path '{propertyPath}'.");
            }

            PropertyInfo nestedProperty = ResolveRequiredProperty(parentValue.GetType(), nestedPropertyName);
            return nestedProperty.GetValue(parentValue);
        }

        /// <summary>
        /// Writes one value to the supplied component using a stable property path.
        /// </summary>
        /// <param name="target">Component that owns the requested property path.</param>
        /// <param name="propertyPath">Stable property path to write.</param>
        /// <param name="value">Value that should be stored at the supplied property path.</param>
        void WritePropertyPathValue(object target, string propertyPath, object value) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            } else if (string.IsNullOrWhiteSpace(propertyPath)) {
                throw new ArgumentException("Property path must be provided.", nameof(propertyPath));
            }

            int separatorIndex = propertyPath.IndexOf('.');
            if (separatorIndex < 0) {
                PropertyInfo directProperty = ResolveRequiredProperty(target.GetType(), propertyPath);
                directProperty.SetValue(target, value);
                return;
            }

            string parentPropertyName = propertyPath.Substring(0, separatorIndex);
            string nestedPropertyName = propertyPath.Substring(separatorIndex + 1);
            PropertyInfo parentProperty = ResolveRequiredProperty(target.GetType(), parentPropertyName);
            object parentValue = parentProperty.GetValue(target);
            if (parentValue == null) {
                throw new InvalidOperationException($"Property '{parentPropertyName}' on '{target.GetType().FullName}' is null and cannot resolve nested path '{propertyPath}'.");
            }

            PropertyInfo nestedProperty = ResolveRequiredProperty(parentValue.GetType(), nestedPropertyName);
            nestedProperty.SetValue(parentValue, value);
            parentProperty.SetValue(target, parentValue);
        }

        /// <summary>
        /// Resolves one required readable and writable property by name.
        /// </summary>
        /// <param name="targetType">Type that owns the requested property.</param>
        /// <param name="propertyName">Property name to resolve.</param>
        /// <returns>Resolved property metadata.</returns>
        PropertyInfo ResolveRequiredProperty(Type targetType, string propertyName) {
            if (targetType == null) {
                throw new ArgumentNullException(nameof(targetType));
            } else if (string.IsNullOrWhiteSpace(propertyName)) {
                throw new ArgumentException("Property name must be provided.", nameof(propertyName));
            }

            PropertyInfo property = targetType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanRead || !property.CanWrite) {
                throw new InvalidOperationException($"Property '{propertyName}' was not found or is not editable on '{targetType.FullName}'.");
            }

            return property;
        }

        /// <summary>
        /// Resolves the stable asset-reference slot name for one direct property path when the property is asset-backed.
        /// </summary>
        /// <param name="propertyPath">Stable property path that may map to an asset reference name.</param>
        /// <returns>Stable asset-reference name when one exists for the path; otherwise null.</returns>
        string TryResolveAssetReferenceName(string propertyPath) {
            if (string.IsNullOrWhiteSpace(propertyPath)) {
                throw new ArgumentException("Property path must be provided.", nameof(propertyPath));
            }

            if (propertyPath.IndexOf('.') >= 0) {
                return null;
            }

            return propertyPath;
        }

        /// <summary>
        /// Finds one detached platform-only component state by the live detached component instance.
        /// </summary>
        /// <param name="platformOverrideState">Platform override state that owns the detached components.</param>
        /// <param name="component">Detached component instance to resolve.</param>
        /// <returns>Matching detached component state when one exists; otherwise null.</returns>
        EntityPlatformAddedComponentState FindAddedComponentState(EntityPlatformComponentOverrideState platformOverrideState, Component component) {
            if (platformOverrideState == null) {
                throw new ArgumentNullException(nameof(platformOverrideState));
            } else if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            foreach (EntityPlatformAddedComponentState addedComponentState in platformOverrideState.EnumerateAddedComponents()) {
                if (ReferenceEquals(addedComponentState.Component, component)) {
                    return addedComponentState;
                }
            }

            return null;
        }

        /// <summary>
        /// Removes one empty entity-level component override container after its last added or removed component override is cleared.
        /// </summary>
        /// <param name="saveComponent">Hidden save component that owns the entity-level component override container.</param>
        /// <param name="platformId">Platform identifier whose override container should be pruned.</param>
        /// <param name="platformOverrideState">Platform override container being inspected.</param>
        void RemoveEmptyComponentPlatformOverride(
            EntitySaveComponent saveComponent,
            string platformId,
            EntityPlatformComponentOverrideState platformOverrideState) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (platformOverrideState == null) {
                throw new ArgumentNullException(nameof(platformOverrideState));
            }

            if (!platformOverrideState.HasAnyOverrides) {
                saveComponent.RemoveComponentPlatformOverride(platformId);
            }
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

            /// <summary>
            /// Rejects unexpected runtime texture resolution for detached override materialization.
            /// </summary>
            /// <param name="reference">Reference that was unexpectedly requested.</param>
            /// <returns>This method never returns.</returns>
            public RuntimeTexture ResolveTexture(SceneAssetReference reference) {
                throw new InvalidOperationException("Detached platform override editing does not support texture asset reconstruction through the scene resolver.");
            }
        }
    }
}

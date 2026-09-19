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
        /// Cached override snapshot components keyed by their common component and scope path.
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

            return ResolveEditableComponent(commonComponent, saveComponent, EditorOverrideScope.ForPlatform(platformId));
        }

        /// <summary>
        /// Resolves the effective editable component for one scope path by folding the parent path first.
        /// </summary>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="scope">Scope path being edited.</param>
        /// <returns>The effective editable component for the supplied scope path.</returns>
        public Component ResolveEditableComponent(Component commonComponent, EntitySaveComponent saveComponent, EditorOverrideScope scope) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            }
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }
            if (scope.IsCommon) {
                return commonComponent;
            }

            Component parentComponent = ResolveEditableComponent(commonComponent, saveComponent, scope.Parent);
            if (!saveComponent.TryGetComponentState(commonComponent, out EntityComponentSaveState saveState)
                || !saveState.TryGetScopedPlatformOverride(scope, out EntityComponentPlatformOverrideState overrideState)) {
                return parentComponent;
            }

            Component snapshotComponent = GetOrLoadOverrideSnapshotComponent(commonComponent, scope, overrideState);
            if (snapshotComponent == null) {
                return parentComponent;
            }
            if (!overrideState.HasAnyPropertyOverrides) {
                return snapshotComponent;
            }

            return BuildEditableComponent(parentComponent, snapshotComponent, overrideState);
        }

        /// <summary>
        /// Ensures an editable component exists for one scope path.
        /// </summary>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="scope">Scope path being edited.</param>
        /// <returns>Editable override component for the supplied scope path.</returns>
        public Component EnsureScopeOverrideComponent(Component commonComponent, EntitySaveComponent saveComponent, EditorOverrideScope scope) {
            if (scope.IsCommon) {
                return commonComponent;
            }

            Component editableComponent = ResolveEditableComponent(commonComponent, saveComponent, scope);
            return CloneComponent(editableComponent);
        }

        /// <summary>
        /// Marks one property path as explicitly overridden at one scope path.
        /// </summary>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="scope">Scope path being edited.</param>
        /// <param name="propertyPath">Stable property path that was edited.</param>
        public void MarkScopePropertyOverride(Component commonComponent, EntitySaveComponent saveComponent, EditorOverrideScope scope, string propertyPath) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            }
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }
            if (string.IsNullOrWhiteSpace(propertyPath)) {
                throw new ArgumentException("Property path must be provided.", nameof(propertyPath));
            }
            if (scope.IsCommon) {
                return;
            }

            saveComponent.GetOrCreateComponentState(commonComponent)
                .GetOrCreateScopedPlatformOverride(scope)
                .SetPropertyOverride(propertyPath);
        }

        /// <summary>
        /// Persists one detached component payload and its explicit property/reference metadata at one scope path.
        /// </summary>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="overrideComponent">Editable override component that should be persisted.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="scope">Scope path being edited.</param>
        public void PersistScopeOverride(Component commonComponent, Component overrideComponent, EntitySaveComponent saveComponent, EditorOverrideScope scope) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            }
            if (overrideComponent == null) {
                throw new ArgumentNullException(nameof(overrideComponent));
            }
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }
            if (scope.IsCommon) {
                throw new InvalidOperationException("Common component state should not be persisted as an override.");
            }

            EntityComponentSaveState componentSaveState = saveComponent.GetOrCreateComponentState(commonComponent);
            EntityComponentSaveState effectiveOverrideSaveState = BuildEffectiveOverrideSaveState(componentSaveState, scope);
            IComponentPersistenceDescriptor descriptor = PersistenceRegistry.GetDescriptor(overrideComponent);
            SceneComponentAssetRecord record = descriptor.SerializeComponent(overrideComponent, 0, effectiveOverrideSaveState);
            EntityComponentPlatformOverrideState overrideState = componentSaveState.GetOrCreateScopedPlatformOverride(scope);
            overrideState.Payload = record.Payload;
            ReplaceOverrideAssetReferences(overrideState, effectiveOverrideSaveState);
            CacheOverrideComponent(commonComponent, scope.ToString(), overrideComponent);
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

            return EnsureScopeOverrideComponent(commonComponent, saveComponent, EditorOverrideScope.ForPlatform(platformId));
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

            PersistScopeOverride(commonComponent, overrideComponent, saveComponent, EditorOverrideScope.ForPlatform(platformId));
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

            StoreScopeAssetReference(
                commonComponent,
                editableComponent,
                saveComponent,
                EditorOverrideScope.ForPlatform(platformId),
                referenceName,
                assetReference);
        }

        /// <summary>
        /// Stores a stable asset reference at one scope path.
        /// </summary>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="editableComponent">Editable component that owns the updated property.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="scope">Scope path being edited.</param>
        /// <param name="referenceName">Stable property reference slot name.</param>
        /// <param name="assetReference">Stable asset reference assigned to the property.</param>
        public void StoreScopeAssetReference(
            Component commonComponent,
            Component editableComponent,
            EntitySaveComponent saveComponent,
            EditorOverrideScope scope,
            string referenceName,
            SceneAssetReference assetReference) {
            if (commonComponent == null || editableComponent == null || saveComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            }
            if (scope.IsCommon) {
                saveComponent.SetAssetReference(commonComponent, referenceName, assetReference);
                return;
            }

            EntityComponentPlatformOverrideState overrideState = saveComponent
                .GetOrCreateComponentState(commonComponent)
                .GetOrCreateScopedPlatformOverride(scope);
            overrideState.SetAssetReference(referenceName, assetReference);
            PersistScopeOverride(commonComponent, editableComponent, saveComponent, scope);
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

            MarkScopePropertyOverride(commonComponent, saveComponent, EditorOverrideScope.ForPlatform(platformId), propertyPath);
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

            return IsScopePropertyOverrideActive(
                commonComponent,
                editableComponent,
                saveComponent,
                EditorOverrideScope.ForPlatform(platformId),
                propertyPath);
        }

        /// <summary>
        /// Returns whether one property is explicitly overridden at one scope path.
        /// </summary>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="editableComponent">Effective editable component shown for the current scope path.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="scope">Scope path being edited.</param>
        /// <param name="propertyPath">Stable property path to query.</param>
        /// <returns>True when the property path is overridden at the supplied scope path.</returns>
        public bool IsScopePropertyOverrideActive(
            Component commonComponent,
            Component editableComponent,
            EntitySaveComponent saveComponent,
            EditorOverrideScope scope,
            string propertyPath) {
            if (saveComponent == null || commonComponent == null || editableComponent == null) {
                return false;
            }
            if (scope.IsCommon) {
                return false;
            }
            if (!saveComponent.TryGetComponentState(commonComponent, out EntityComponentSaveState componentSaveState)
                || !componentSaveState.TryGetScopedPlatformOverride(scope, out EntityComponentPlatformOverrideState overrideState)) {
                return false;
            }
            if (overrideState.HasAnyPropertyOverrides) {
                return overrideState.HasPropertyOverride(propertyPath);
            }

            return !object.Equals(
                ReadPropertyPathValue(ResolveEditableComponent(commonComponent, saveComponent, scope.Parent), propertyPath),
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

            ClearScopePropertyOverride(commonComponent, saveComponent, EditorOverrideScope.ForPlatform(platformId), propertyPath);
        }

        /// <summary>
        /// Clears one explicit property override at one scope path.
        /// </summary>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="scope">Scope path whose property override should be cleared.</param>
        /// <param name="propertyPath">Stable property path that should return to parent behavior.</param>
        public void ClearScopePropertyOverride(Component commonComponent, EntitySaveComponent saveComponent, EditorOverrideScope scope, string propertyPath) {
            if (saveComponent == null || commonComponent == null || string.IsNullOrWhiteSpace(propertyPath)) {
                return;
            }
            if (scope.IsCommon) {
                return;
            }
            if (!saveComponent.TryGetComponentState(commonComponent, out EntityComponentSaveState componentSaveState)
                || !componentSaveState.TryGetScopedPlatformOverride(scope, out EntityComponentPlatformOverrideState overrideState)) {
                return;
            }

            overrideState.ClearPropertyOverride(propertyPath);
            string assetReferenceName = TryResolveAssetReferenceName(propertyPath);
            if (!string.IsNullOrWhiteSpace(assetReferenceName)) {
                overrideState.RemoveAssetReference(assetReferenceName);
            }
            if (overrideState.HasMemberValue(propertyPath)) {
                overrideState.RemoveMemberValue(propertyPath);
            }
            if (!overrideState.HasAnyPropertyOverrides && !overrideState.HasAnyAssetReferences && !overrideState.HasAnyMemberValues) {
                componentSaveState.RemoveScopedPlatformOverride(scope);
                ClearCachedOverrideComponent(commonComponent, scope.ToString());
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

            return IsComponentRemoved(commonComponent, saveComponent, EditorOverrideScope.ForPlatform(platformId));
        }

        /// <summary>
        /// Returns whether one common live component is removed at one scope path or any of its parents.
        /// </summary>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="scope">Scope path being edited.</param>
        /// <returns>True when the common component is removed at the supplied scope path.</returns>
        public bool IsComponentRemoved(Component commonComponent, EntitySaveComponent saveComponent, EditorOverrideScope scope) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }
            if (scope.IsCommon) {
                return false;
            }

            string componentKey = EnsureComponentKey(commonComponent, saveComponent);
            if (saveComponent.TryGetComponentPlatformOverride(scope, out EntityPlatformComponentOverrideState overrideState)
                && overrideState.IsComponentRemoved(componentKey)) {
                return true;
            }

            return IsComponentRemoved(commonComponent, saveComponent, scope.Parent);
        }

        /// <summary>
        /// Returns the detached platform-only components inherited by the supplied platform.
        /// </summary>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="platformId">Target platform being edited.</param>
        /// <returns>Detached platform-only component states inherited by the platform.</returns>
        public IReadOnlyList<EntityPlatformAddedComponentState> GetAddedComponents(EntitySaveComponent saveComponent, string platformId) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return GetAddedComponents(saveComponent, EditorOverrideScope.ForPlatform(platformId));
        }

        /// <summary>
        /// Returns detached component additions inherited by one scope path.
        /// </summary>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="scope">Scope path being edited.</param>
        /// <returns>Detached component states authored on the scope path or on any of its parents.</returns>
        public IReadOnlyList<EntityPlatformAddedComponentState> GetAddedComponents(EntitySaveComponent saveComponent, EditorOverrideScope scope) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }
            if (scope.IsCommon) {
                return Array.Empty<EntityPlatformAddedComponentState>();
            }

            Dictionary<string, EntityPlatformAddedComponentState> addedByKey = new Dictionary<string, EntityPlatformAddedComponentState>(StringComparer.Ordinal);
            for (int depth = 1; depth <= scope.Depth; depth++) {
                EditorOverrideScopeStep[] prefixSteps = new EditorOverrideScopeStep[depth];
                for (int index = 0; index < depth; index++) {
                    prefixSteps[index] = scope.Steps[index];
                }

                AddAddedComponentsForScope(saveComponent, new EditorOverrideScope(prefixSteps), addedByKey);
            }

            return addedByKey.Values.ToArray();
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

            return AddScopeOnlyComponent(descriptor, saveComponent, EditorOverrideScope.ForPlatform(platformId));
        }

        /// <summary>
        /// Adds one detached component directly to one scope path.
        /// </summary>
        /// <param name="descriptor">Descriptor that defines the component type to add.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="scope">Scope path being edited.</param>
        /// <returns>Detached scope-only component state.</returns>
        public EntityPlatformAddedComponentState AddScopeOnlyComponent(EditorComponentAddDescriptor descriptor, EntitySaveComponent saveComponent, EditorOverrideScope scope) {
            if (descriptor == null) {
                throw new ArgumentNullException(nameof(descriptor));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }
            if (scope.IsCommon) {
                throw new InvalidOperationException("Scoped components cannot be added on the common tab.");
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
            saveComponent.GetOrCreateComponentPlatformOverride(scope).SetAddedComponent(addedComponentState);
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

            return RemoveComponent(component, saveComponent, EditorOverrideScope.ForPlatform(platformId));
        }

        /// <summary>
        /// Removes one component at one scope path.
        /// </summary>
        /// <param name="component">Component being removed from the active scope view.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="scope">Scope path being edited.</param>
        /// <returns>True when the component still exists on the parent path and was hidden only for this scope path; otherwise false.</returns>
        public bool RemoveComponent(Component component, EntitySaveComponent saveComponent, EditorOverrideScope scope) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }
            if (scope.IsCommon) {
                throw new InvalidOperationException("Scoped remove behavior is not valid on the common tab.");
            }

            EntityPlatformComponentOverrideState scopeOverride = saveComponent.GetOrCreateComponentPlatformOverride(scope);
            EntityPlatformAddedComponentState addedComponentState = FindAddedComponentState(scopeOverride, component);
            if (addedComponentState != null) {
                scopeOverride.RemoveAddedComponent(addedComponentState.ComponentKey);
                RemoveEmptyComponentPlatformOverride(saveComponent, scope, scopeOverride);
                return false;
            }

            EntityComponentSaveState componentSaveState = saveComponent.GetOrCreateComponentState(component);
            string componentKey = EnsureComponentKey(component, saveComponent);
            scopeOverride.MarkComponentRemoved(componentKey);
            componentSaveState.RemoveScopedPlatformOverride(scope);
            ClearCachedOverrideComponent(component, scope.ToString());
            RemoveEmptyComponentPlatformOverride(saveComponent, scope, scopeOverride);
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

            RevertComponentExistenceOverride(component, saveComponent, EditorOverrideScope.ForPlatform(platformId));
        }

        /// <summary>
        /// Reverts one component existence override at one scope path.
        /// </summary>
        /// <param name="component">Component whose existence override should be reverted.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="scope">Scope path being edited.</param>
        public void RevertComponentExistenceOverride(Component component, EntitySaveComponent saveComponent, EditorOverrideScope scope) {
            if (!saveComponent.TryGetComponentPlatformOverride(scope, out EntityPlatformComponentOverrideState scopeOverride)) {
                return;
            }

            EntityPlatformAddedComponentState addedComponentState = FindAddedComponentState(scopeOverride, component);
            if (addedComponentState != null) {
                scopeOverride.RemoveAddedComponent(addedComponentState.ComponentKey);
            } else if (saveComponent.TryGetComponentState(component, out EntityComponentSaveState componentSaveState)
                && !string.IsNullOrWhiteSpace(componentSaveState.ComponentKey)) {
                scopeOverride.RestoreRemovedComponent(componentSaveState.ComponentKey);
            }

            RemoveEmptyComponentPlatformOverride(saveComponent, scope, scopeOverride);
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

            return TryGetAddedComponentState(component, saveComponent, EditorOverrideScope.ForPlatform(platformId), out addedComponentState);
        }

        /// <summary>
        /// Attempts to resolve a detached component inherited by one scope path.
        /// </summary>
        /// <param name="component">Detached component instance to resolve.</param>
        /// <param name="saveComponent">Hidden save component that stores editor metadata.</param>
        /// <param name="scope">Scope path whose detached component state should be queried.</param>
        /// <param name="addedComponentState">Resolved detached component state when one exists.</param>
        /// <returns>True when the scope path or one of its parents owns a detached state for the component.</returns>
        public bool TryGetAddedComponentState(
            Component component,
            EntitySaveComponent saveComponent,
            EditorOverrideScope scope,
            out EntityPlatformAddedComponentState addedComponentState) {
            addedComponentState = null;
            if (saveComponent.TryGetComponentPlatformOverride(scope, out EntityPlatformComponentOverrideState scopeOverride)) {
                addedComponentState = FindAddedComponentState(scopeOverride, component);
            }
            if (addedComponentState != null) {
                return true;
            }
            if (scope.IsCommon) {
                return false;
            }

            return TryGetAddedComponentState(component, saveComponent, scope.Parent, out addedComponentState);
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

            StoreAddedComponentAssetReference(
                component,
                saveComponent,
                EditorOverrideScope.ForPlatform(platformId),
                referenceName,
                assetReference);
        }

        /// <summary>
        /// Stores a stable asset reference in a detached component inherited by one scope path.
        /// </summary>
        /// <param name="component">Detached scope-only component that owns the updated asset property.</param>
        /// <param name="saveComponent">Hidden save component that stores entity-level component overrides.</param>
        /// <param name="scope">Scope path being edited.</param>
        /// <param name="referenceName">Stable property reference slot name.</param>
        /// <param name="assetReference">Stable asset reference assigned to the property.</param>
        public void StoreAddedComponentAssetReference(
            Component component,
            EntitySaveComponent saveComponent,
            EditorOverrideScope scope,
            string referenceName,
            SceneAssetReference assetReference) {
            if (!TryGetAddedComponentState(component, saveComponent, scope, out EntityPlatformAddedComponentState addedComponentState)) {
                throw new InvalidOperationException("Detached scoped component asset references require a tracked added component state.");
            }

            addedComponentState.SaveState.SetAssetReference(referenceName, assetReference);
        }

        /// <summary>
        /// Creates the standard editor component persistence registry used by scene save and load.
        /// </summary>
        /// <returns>Initialized persistence registry.</returns>
        ComponentPersistenceRegistry CreatePersistenceRegistry() {
            return new ComponentPersistenceRegistry();
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
        /// Builds effective asset-reference metadata inherited by one scope path payload.
        /// </summary>
        /// <param name="componentSaveState">Component save-state that owns the common and scoped metadata.</param>
        /// <param name="scope">Scope path whose effective references should be gathered.</param>
        /// <returns>Effective save-state containing the references required by the override.</returns>
        EntityComponentSaveState BuildEffectiveOverrideSaveState(EntityComponentSaveState componentSaveState, EditorOverrideScope scope) {
            if (componentSaveState == null) {
                throw new ArgumentNullException(nameof(componentSaveState));
            }

            EntityComponentSaveState effectiveSaveState = new EntityComponentSaveState();
            foreach (KeyValuePair<string, SceneAssetReference> assetReferenceEntry in componentSaveState.EnumerateNamedAssetReferences()) {
                effectiveSaveState.SetAssetReference(assetReferenceEntry.Key, assetReferenceEntry.Value);
            }

            for (int depth = 1; depth <= scope.Depth; depth++) {
                EditorOverrideScopeStep[] prefixSteps = new EditorOverrideScopeStep[depth];
                for (int index = 0; index < depth; index++) {
                    prefixSteps[index] = scope.Steps[index];
                }

                if (!componentSaveState.TryGetScopedPlatformOverride(new EditorOverrideScope(prefixSteps), out EntityComponentPlatformOverrideState overrideState)) {
                    continue;
                }

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
                ComponentTypeId = ResolvePersistedComponentTypeId(commonComponent, descriptor),
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
        /// Resolves the persisted component type id that should be used when rebuilding one detached override payload.
        /// </summary>
        /// <param name="component">Concrete component whose persisted type id should be emitted.</param>
        /// <param name="descriptor">Resolved persistence descriptor that owns the component shape.</param>
        /// <returns>Stable persisted component type id for the component payload.</returns>
        string ResolvePersistedComponentTypeId(Component component, IComponentPersistenceDescriptor descriptor) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            } else if (descriptor == null) {
                throw new ArgumentNullException(nameof(descriptor));
            }

            if (descriptor is AutomaticScriptComponentPersistenceDescriptor) {
                return AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(component.GetType());
            }

            return descriptor.ComponentTypeId;
        }

        /// <summary>
        /// Resolves the cached or deserialized snapshot component that stores the persisted override values for one scope path.
        /// </summary>
        /// <param name="commonComponent">Common live component that owns the override.</param>
        /// <param name="scope">Scope path whose override snapshot should be resolved.</param>
        /// <param name="overrideState">Stored override payload metadata.</param>
        /// <returns>Detached snapshot component when one can be materialized; otherwise null.</returns>
        Component GetOrLoadOverrideSnapshotComponent(Component commonComponent, EditorOverrideScope scope, EntityComponentPlatformOverrideState overrideState) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            }
            if (overrideState == null) {
                throw new ArgumentNullException(nameof(overrideState));
            }

            string cacheKey = scope.ToString();
            Component cachedOverrideComponent = GetCachedOverrideComponent(commonComponent, cacheKey);
            if (cachedOverrideComponent != null) {
                return cachedOverrideComponent;
            }

            Component deserializedOverrideComponent = DeserializeOverrideComponent(commonComponent, overrideState);
            if (deserializedOverrideComponent == null) {
                return null;
            }

            CacheOverrideComponent(commonComponent, cacheKey, deserializedOverrideComponent);
            return deserializedOverrideComponent;
        }

        /// <summary>
        /// Retrieves one cached override component when it already exists in the current editor session.
        /// </summary>
        /// <param name="commonComponent">Common live component that owns the override.</param>
        /// <param name="cacheKey">Scope path key whose override should be returned.</param>
        /// <returns>Cached override component when one exists; otherwise null.</returns>
        Component GetCachedOverrideComponent(Component commonComponent, string cacheKey) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            } else if (string.IsNullOrWhiteSpace(cacheKey)) {
                throw new ArgumentException("Scope cache key must be provided.", nameof(cacheKey));
            }

            if (!OverrideComponentsByCommonComponent.TryGetValue(commonComponent, out Dictionary<string, Component> overridesByScope)) {
                return null;
            }

            if (!overridesByScope.TryGetValue(cacheKey, out Component overrideComponent)) {
                return null;
            }

            return overrideComponent;
        }

        /// <summary>
        /// Clears one cached override snapshot from the current editor session.
        /// </summary>
        /// <param name="commonComponent">Common live component that owns the override snapshot.</param>
        /// <param name="cacheKey">Scope path key whose cached snapshot should be cleared.</param>
        void ClearCachedOverrideComponent(Component commonComponent, string cacheKey) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            } else if (string.IsNullOrWhiteSpace(cacheKey)) {
                throw new ArgumentException("Scope cache key must be provided.", nameof(cacheKey));
            }

            if (!OverrideComponentsByCommonComponent.TryGetValue(commonComponent, out Dictionary<string, Component> overridesByScope)) {
                return;
            }

            overridesByScope.Remove(cacheKey);
            if (overridesByScope.Count < 1) {
                OverrideComponentsByCommonComponent.Remove(commonComponent);
            }
        }

        /// <summary>
        /// Stores one editable override component in the current editor-session cache.
        /// </summary>
        /// <param name="commonComponent">Common live component that owns the override.</param>
        /// <param name="cacheKey">Scope path key whose override is being cached.</param>
        /// <param name="overrideComponent">Detached editable override component to cache.</param>
        void CacheOverrideComponent(Component commonComponent, string cacheKey, Component overrideComponent) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            } else if (string.IsNullOrWhiteSpace(cacheKey)) {
                throw new ArgumentException("Scope cache key must be provided.", nameof(cacheKey));
            } else if (overrideComponent == null) {
                throw new ArgumentNullException(nameof(overrideComponent));
            }

            if (!OverrideComponentsByCommonComponent.TryGetValue(commonComponent, out Dictionary<string, Component> overridesByScope)) {
                overridesByScope = new Dictionary<string, Component>(StringComparer.OrdinalIgnoreCase);
                OverrideComponentsByCommonComponent.Add(commonComponent, overridesByScope);
            }

            overridesByScope[cacheKey] = overrideComponent;
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
                if (TryParseIndexedPropertyPath(propertyPath, out string arrayPropertyName, out int elementIndex)) {
                    PropertyInfo readArrayProperty = target.GetType().GetProperty(arrayPropertyName, BindingFlags.Instance | BindingFlags.Public);
                    if (readArrayProperty == null
                        || !readArrayProperty.CanRead
                        || readArrayProperty.GetValue(target) is not Array readArrayValue
                        || elementIndex < 0
                        || elementIndex >= readArrayValue.Length) {
                        return null;
                    }

                    return readArrayValue.GetValue(elementIndex);
                }

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
        /// Parses one direct property path shaped like <c>Name[index]</c> into its property name and element index.
        /// </summary>
        /// <param name="propertyPath">Stable property path to parse.</param>
        /// <param name="propertyName">Receives the array property name when the path is indexed.</param>
        /// <param name="elementIndex">Receives the zero-based element index when the path is indexed.</param>
        /// <returns>True when the path addresses one array element.</returns>
        static bool TryParseIndexedPropertyPath(string propertyPath, out string propertyName, out int elementIndex) {
            propertyName = null;
            elementIndex = -1;
            int openBracketIndex = propertyPath.IndexOf('[');
            if (openBracketIndex <= 0 || !propertyPath.EndsWith("]", StringComparison.Ordinal)) {
                return false;
            }

            string indexText = propertyPath.Substring(openBracketIndex + 1, propertyPath.Length - openBracketIndex - 2);
            if (!int.TryParse(indexText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int parsedIndex)) {
                return false;
            }

            propertyName = propertyPath.Substring(0, openBracketIndex);
            elementIndex = parsedIndex;
            return true;
        }

        /// <summary>
        /// Resolves one required array property value and validates the requested element index.
        /// </summary>
        /// <param name="target">Component that owns the array property.</param>
        /// <param name="arrayPropertyName">Array property name.</param>
        /// <param name="elementIndex">Zero-based element index being addressed.</param>
        /// <param name="propertyPath">Original property path used for diagnostics.</param>
        /// <returns>Resolved array instance.</returns>
        static Array ResolveRequiredArrayValue(object target, string arrayPropertyName, int elementIndex, string propertyPath) {
            PropertyInfo arrayProperty = target.GetType().GetProperty(arrayPropertyName, BindingFlags.Instance | BindingFlags.Public);
            if (arrayProperty == null || !arrayProperty.CanRead) {
                throw new InvalidOperationException($"Property '{arrayPropertyName}' was not found or is not readable on '{target.GetType().FullName}'.");
            }

            if (arrayProperty.GetValue(target) is not Array arrayValue) {
                throw new InvalidOperationException($"Property '{arrayPropertyName}' on '{target.GetType().FullName}' is not an array and cannot resolve path '{propertyPath}'.");
            }

            if (elementIndex < 0 || elementIndex >= arrayValue.Length) {
                throw new InvalidOperationException($"Property path '{propertyPath}' addresses element {elementIndex} outside the current array length {arrayValue.Length} on '{target.GetType().FullName}'.");
            }

            return arrayValue;
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
                if (TryParseIndexedPropertyPath(propertyPath, out string arrayPropertyName, out int elementIndex)) {
                    Array arrayValue = ResolveRequiredArrayValue(target, arrayPropertyName, elementIndex, propertyPath);
                    arrayValue.SetValue(value, elementIndex);
                    return;
                }

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
        /// Adds valid detached component states from one scope to a keyed result set.
        /// </summary>
        void AddAddedComponentsForScope(
            EntitySaveComponent saveComponent,
            EditorOverrideScope scope,
            Dictionary<string, EntityPlatformAddedComponentState> addedByKey) {
            if (!saveComponent.TryGetComponentPlatformOverride(scope, out EntityPlatformComponentOverrideState overrideState)) {
                return;
            }

            foreach (EntityPlatformAddedComponentState addedComponentState in overrideState.EnumerateAddedComponents()) {
                if (addedComponentState == null || addedComponentState.Component == null || addedComponentState.SaveState == null) {
                    continue;
                }
                addedByKey[addedComponentState.ComponentKey] = addedComponentState;
            }
        }

        /// <summary>
        /// Removes one empty entity-level component override container after its last added or removed component override is cleared.
        /// </summary>
        /// <param name="saveComponent">Hidden save component that owns the entity-level component override container.</param>
        /// <param name="scope">Scope path whose override container should be pruned.</param>
        /// <param name="platformOverrideState">Scoped override container being inspected.</param>
        void RemoveEmptyComponentPlatformOverride(
            EntitySaveComponent saveComponent,
            EditorOverrideScope scope,
            EntityPlatformComponentOverrideState platformOverrideState) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (platformOverrideState == null) {
                throw new ArgumentNullException(nameof(platformOverrideState));
            }

            if (!platformOverrideState.HasAnyOverrides) {
                saveComponent.RemoveComponentPlatformOverride(scope);
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

            /// <summary>
            /// Rejects unexpected animation-clip resolution for detached override materialization.
            /// </summary>
            /// <param name="reference">Reference that was unexpectedly requested.</param>
            /// <returns>This method never returns.</returns>
            public AnimationClipAsset ResolveAnimationClip(SceneAssetReference reference) {
                throw new InvalidOperationException("Detached platform override editing does not support animation clip reconstruction through the scene resolver.");
            }

            /// <summary>Rejects unexpected audio resolution for detached override editing.</summary>
            /// <param name="reference">Reference that was unexpectedly requested.</param>
            /// <returns>This method never returns.</returns>
            public AudioAsset ResolveAudio(SceneAssetReference reference) {
                throw new InvalidOperationException("Detached platform override editing does not support audio asset reconstruction through the scene resolver.");
            }
        }
    }
}

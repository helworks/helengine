namespace helengine.editor {
    /// <summary>
    /// Expands blueprint instance roots inside serialized scene assets before platform packaging rewrites them.
    /// </summary>
    public sealed class BlueprintPackagedSceneExpansionService {
        /// <summary>
        /// Stable persisted component type id used by serialized blueprint instance markers.
        /// </summary>
        static readonly string BlueprintInstanceComponentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(BlueprintInstanceComponent));

        /// <summary>
        /// Absolute path to the project root.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Component persistence registry used to materialize persisted blueprint instance components.
        /// </summary>
        readonly ComponentPersistenceRegistry PersistenceRegistry;

        /// <summary>
        /// Resolver used while deserializing blueprint instance component payloads. Blueprint instance metadata should not depend on runtime assets.
        /// </summary>
        readonly ISceneAssetReferenceResolver ComponentReferenceResolver;

        /// <summary>
        /// Initializes a new packaged-scene blueprint expansion service.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the assets folder.</param>
        /// <param name="persistenceRegistry">Registry used to deserialize persisted component records.</param>
        public BlueprintPackagedSceneExpansionService(string projectRootPath, ComponentPersistenceRegistry persistenceRegistry) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (persistenceRegistry == null) {
                throw new ArgumentNullException(nameof(persistenceRegistry));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            PersistenceRegistry = persistenceRegistry;
            ComponentReferenceResolver = new NullSceneAssetReferenceResolver();
        }

        /// <summary>
        /// Expands every blueprint instance root inside one serialized scene asset into ordinary child entities.
        /// </summary>
        /// <param name="sceneAsset">Scene asset to expand in place.</param>
        public void Expand(SceneAsset sceneAsset) {
            if (sceneAsset == null) {
                throw new ArgumentNullException(nameof(sceneAsset));
            }

            List<SceneAssetReference> mergedReferences = new List<SceneAssetReference>(sceneAsset.AssetReferences ?? Array.Empty<SceneAssetReference>());
            HashSet<string> mergedReferenceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < mergedReferences.Count; index++) {
                mergedReferenceKeys.Add(BuildReferenceKey(mergedReferences[index]));
            }

            SceneEntityAsset[] rootEntities = sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>();
            for (int index = 0; index < rootEntities.Length; index++) {
                ExpandEntityRecursive(rootEntities[index], mergedReferences, mergedReferenceKeys);
            }

            sceneAsset.AssetReferences = mergedReferences.ToArray();
        }

        /// <summary>
        /// Expands blueprint instances found on one serialized entity and its authored descendants.
        /// </summary>
        /// <param name="entityAsset">Entity currently being processed.</param>
        /// <param name="mergedReferences">Merged scene-level asset reference list receiving blueprint dependencies.</param>
        /// <param name="mergedReferenceKeys">Deduplication keys for already merged references.</param>
        void ExpandEntityRecursive(
            SceneEntityAsset entityAsset,
            List<SceneAssetReference> mergedReferences,
            HashSet<string> mergedReferenceKeys) {
            if (entityAsset == null) {
                throw new ArgumentNullException(nameof(entityAsset));
            }
            if (mergedReferences == null) {
                throw new ArgumentNullException(nameof(mergedReferences));
            }
            if (mergedReferenceKeys == null) {
                throw new ArgumentNullException(nameof(mergedReferenceKeys));
            }

            if (TryExtractBlueprintInstance(entityAsset, out BlueprintInstanceComponent instanceComponent, out int componentIndex)) {
                ExpandBlueprintInstance(entityAsset, instanceComponent, componentIndex, mergedReferences, mergedReferenceKeys);
            }

            SceneEntityAsset[] children = entityAsset.Children ?? Array.Empty<SceneEntityAsset>();
            for (int index = 0; index < children.Length; index++) {
                ExpandEntityRecursive(children[index], mergedReferences, mergedReferenceKeys);
            }
        }

        /// <summary>
        /// Expands one serialized blueprint instance root into ordinary child entities and removes the editor-only instance component.
        /// </summary>
        /// <param name="instanceRoot">Scene-owned instance root being expanded.</param>
        /// <param name="instanceComponent">Deserialized instance component payload.</param>
        /// <param name="componentIndex">Serialized component index that should be removed.</param>
        /// <param name="mergedReferences">Merged scene-level asset reference list receiving blueprint dependencies.</param>
        /// <param name="mergedReferenceKeys">Deduplication keys for already merged references.</param>
        void ExpandBlueprintInstance(
            SceneEntityAsset instanceRoot,
            BlueprintInstanceComponent instanceComponent,
            int componentIndex,
            List<SceneAssetReference> mergedReferences,
            HashSet<string> mergedReferenceKeys) {
            if (instanceRoot == null) {
                throw new ArgumentNullException(nameof(instanceRoot));
            }
            if (instanceComponent == null) {
                throw new ArgumentNullException(nameof(instanceComponent));
            }
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex));
            }

            BlueprintAsset blueprintAsset = LoadBlueprintAsset(instanceComponent.BlueprintAssetPath);
            ValidateNoNestedBlueprintInstances(blueprintAsset.RootEntity);

            RemoveComponentAt(instanceRoot, componentIndex);

            List<SceneEntityAsset> expandedChildren = new List<SceneEntityAsset>(instanceRoot.Children ?? Array.Empty<SceneEntityAsset>());
            expandedChildren.Add(CloneEntity(blueprintAsset.RootEntity));
            instanceRoot.Children = expandedChildren.ToArray();

            SceneAssetReference[] blueprintReferences = blueprintAsset.AssetReferences ?? Array.Empty<SceneAssetReference>();
            for (int index = 0; index < blueprintReferences.Length; index++) {
                string referenceKey = BuildReferenceKey(blueprintReferences[index]);
                if (mergedReferenceKeys.Add(referenceKey)) {
                    mergedReferences.Add(CloneReference(blueprintReferences[index]));
                }
            }
        }

        /// <summary>
        /// Attempts to extract one blueprint instance component from a serialized entity component list.
        /// </summary>
        /// <param name="entityAsset">Serialized entity payload to inspect.</param>
        /// <param name="instanceComponent">Resolved blueprint instance component when present.</param>
        /// <param name="componentIndex">Serialized component-list index when present.</param>
        /// <returns>True when the entity defines one blueprint instance component.</returns>
        bool TryExtractBlueprintInstance(SceneEntityAsset entityAsset, out BlueprintInstanceComponent instanceComponent, out int componentIndex) {
            SceneComponentAssetRecord[] components = entityAsset?.Components ?? Array.Empty<SceneComponentAssetRecord>();
            for (int index = 0; index < components.Length; index++) {
                SceneComponentAssetRecord record = components[index];
                if (!IsBlueprintInstanceComponentRecord(record)) {
                    continue;
                }

                IComponentPersistenceDescriptor descriptor = PersistenceRegistry.GetDescriptor(record.ComponentTypeId);
                Component component = descriptor.DeserializeComponent(record, null, ComponentReferenceResolver);
                if (component is BlueprintInstanceComponent blueprintInstanceComponent) {
                    instanceComponent = blueprintInstanceComponent;
                    componentIndex = index;
                    return true;
                }
            }

            instanceComponent = null;
            componentIndex = -1;
            return false;
        }

        /// <summary>
        /// Loads one referenced blueprint asset from disk.
        /// </summary>
        /// <param name="blueprintAssetPath">Project-relative blueprint asset path.</param>
        /// <returns>Loaded blueprint asset payload.</returns>
        BlueprintAsset LoadBlueprintAsset(string blueprintAssetPath) {
            string fullPath = ResolveBlueprintFullPath(blueprintAssetPath);
            using FileStream stream = File.OpenRead(fullPath);
            Asset asset = AssetSerializer.Deserialize(stream);
            if (asset is BlueprintAsset blueprintAsset) {
                BlueprintValidationService.ValidateAsset(blueprintAsset);
                return blueprintAsset;
            }

            throw new InvalidOperationException($"Blueprint instance asset '{blueprintAssetPath}' did not deserialize into a BlueprintAsset.");
        }

        /// <summary>
        /// Rejects nested blueprint instance records inside blueprint source content in v1.
        /// </summary>
        /// <param name="entityAsset">Current blueprint entity payload being inspected.</param>
        void ValidateNoNestedBlueprintInstances(SceneEntityAsset entityAsset) {
            if (entityAsset == null) {
                throw new ArgumentNullException(nameof(entityAsset));
            }

            SceneComponentAssetRecord[] components = entityAsset.Components ?? Array.Empty<SceneComponentAssetRecord>();
            for (int index = 0; index < components.Length; index++) {
                SceneComponentAssetRecord record = components[index];
                if (!IsBlueprintInstanceComponentRecord(record)) {
                    continue;
                }

                IComponentPersistenceDescriptor descriptor = PersistenceRegistry.GetDescriptor(record.ComponentTypeId);
                Component component = descriptor.DeserializeComponent(record, null, ComponentReferenceResolver);
                if (component is BlueprintInstanceComponent) {
                    throw new InvalidOperationException("Blueprint sources may not contain nested blueprint instances in v1.");
                }
            }

            SceneEntityAsset[] children = entityAsset.Children ?? Array.Empty<SceneEntityAsset>();
            for (int index = 0; index < children.Length; index++) {
                ValidateNoNestedBlueprintInstances(children[index]);
            }
        }

        /// <summary>
        /// Resolves whether one serialized component record is the persisted blueprint instance marker.
        /// </summary>
        /// <param name="record">Serialized component record being inspected.</param>
        /// <returns>True when the record encodes a blueprint instance marker.</returns>
        static bool IsBlueprintInstanceComponentRecord(SceneComponentAssetRecord record) {
            if (record == null || string.IsNullOrWhiteSpace(record.ComponentTypeId)) {
                return false;
            }

            return string.Equals(record.ComponentTypeId, BlueprintInstanceComponentTypeId, StringComparison.Ordinal);
        }

        /// <summary>
        /// Removes one serialized component record from the supplied entity and reindexes the remaining records.
        /// </summary>
        /// <param name="entityAsset">Entity whose serialized component list should be updated.</param>
        /// <param name="componentIndex">Zero-based component-list index to remove.</param>
        void RemoveComponentAt(SceneEntityAsset entityAsset, int componentIndex) {
            SceneComponentAssetRecord[] components = entityAsset.Components ?? Array.Empty<SceneComponentAssetRecord>();
            List<SceneComponentAssetRecord> keptComponents = new List<SceneComponentAssetRecord>(Math.Max(0, components.Length - 1));
            for (int index = 0; index < components.Length; index++) {
                if (index != componentIndex && components[index] != null) {
                    keptComponents.Add(CloneComponentRecord(components[index]));
                }
            }

            for (int index = 0; index < keptComponents.Count; index++) {
                keptComponents[index].ComponentIndex = index;
            }

            entityAsset.Components = keptComponents.ToArray();
        }

        /// <summary>
        /// Resolves one project-relative blueprint asset path to an absolute filesystem path.
        /// </summary>
        /// <param name="blueprintAssetPath">Project-relative blueprint asset path.</param>
        /// <returns>Absolute blueprint file path.</returns>
        string ResolveBlueprintFullPath(string blueprintAssetPath) {
            if (string.IsNullOrWhiteSpace(blueprintAssetPath)) {
                throw new InvalidOperationException("Blueprint instance roots must define a blueprint asset path.");
            }

            string fullPath = Path.GetFullPath(Path.Combine(ProjectRootPath, "assets", blueprintAssetPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!fullPath.StartsWith(ProjectRootPath, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Blueprint instance asset path must stay inside the current project.");
            }

            return fullPath;
        }

        /// <summary>
        /// Builds a stable deduplication key for one scene asset reference.
        /// </summary>
        /// <param name="reference">Scene asset reference to key.</param>
        /// <returns>Stable deduplication key.</returns>
        static string BuildReferenceKey(SceneAssetReference reference) {
            return string.Concat(
                reference?.SourceKind.ToString() ?? string.Empty,
                "|",
                reference?.RelativePath ?? string.Empty,
                "|",
                reference?.ProviderId ?? string.Empty,
                "|",
                reference?.AssetId ?? string.Empty);
        }

        /// <summary>
        /// Clones one serialized entity recursively so blueprint source assets are never mutated in place.
        /// </summary>
        /// <param name="entityAsset">Serialized entity payload to clone.</param>
        /// <returns>Detached clone of the serialized entity.</returns>
        static SceneEntityAsset CloneEntity(SceneEntityAsset entityAsset) {
            if (entityAsset == null) {
                return null;
            }

            SceneComponentAssetRecord[] components = entityAsset.Components ?? Array.Empty<SceneComponentAssetRecord>();
            SceneEntityAsset[] children = entityAsset.Children ?? Array.Empty<SceneEntityAsset>();
            SceneEntityPlatformExistenceOverrideAsset[] existenceOverrides = entityAsset.PlatformExistenceOverrides ?? Array.Empty<SceneEntityPlatformExistenceOverrideAsset>();
            SceneEntityPlatformTransformOverrideAsset[] transformOverrides = entityAsset.PlatformTransformOverrides ?? Array.Empty<SceneEntityPlatformTransformOverrideAsset>();
            SceneEntityPlatformComponentOverrideAsset[] componentOverrides = entityAsset.PlatformComponentOverrides ?? Array.Empty<SceneEntityPlatformComponentOverrideAsset>();

            return new SceneEntityAsset {
                Id = entityAsset.Id,
                Name = entityAsset.Name,
                IsStatic = entityAsset.IsStatic,
                Enabled = entityAsset.Enabled,
                LayerMask = entityAsset.LayerMask,
                LocalPosition = entityAsset.LocalPosition,
                LocalScale = entityAsset.LocalScale,
                LocalOrientation = entityAsset.LocalOrientation,
                Components = components.Select(CloneComponentRecord).ToArray(),
                PlatformExistenceOverrides = existenceOverrides.Select(CloneExistenceOverride).ToArray(),
                PlatformTransformOverrides = transformOverrides.Select(CloneTransformOverride).ToArray(),
                PlatformComponentOverrides = componentOverrides.Select(CloneComponentOverride).ToArray(),
                Children = children.Select(CloneEntity).ToArray()
            };
        }

        static SceneComponentAssetRecord CloneComponentRecord(SceneComponentAssetRecord record) {
            if (record == null) {
                return null;
            }

            return new SceneComponentAssetRecord {
                ComponentTypeId = record.ComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                ComponentKey = record.ComponentKey,
                Payload = record.Payload?.ToArray()
            };
        }

        static SceneAssetReference CloneReference(SceneAssetReference reference) {
            if (reference == null) {
                return null;
            }

            return global::helengine.SceneAssetReferenceFactory.Rehydrate(
                reference.SourceKind,
                reference.RelativePath,
                reference.ProviderId,
                reference.AssetId);
        }

        static SceneEntityPlatformExistenceOverrideAsset CloneExistenceOverride(SceneEntityPlatformExistenceOverrideAsset overrideAsset) {
            if (overrideAsset == null) {
                return null;
            }

            return new SceneEntityPlatformExistenceOverrideAsset {
                PlatformId = overrideAsset.PlatformId,
                Exists = overrideAsset.Exists
            };
        }

        static SceneEntityPlatformTransformOverrideAsset CloneTransformOverride(SceneEntityPlatformTransformOverrideAsset overrideAsset) {
            if (overrideAsset == null) {
                return null;
            }

            return new SceneEntityPlatformTransformOverrideAsset {
                PlatformId = overrideAsset.PlatformId,
                HasLocalPositionOverride = overrideAsset.HasLocalPositionOverride,
                LocalPosition = overrideAsset.LocalPosition,
                HasLocalScaleOverride = overrideAsset.HasLocalScaleOverride,
                LocalScale = overrideAsset.LocalScale,
                HasLocalOrientationOverride = overrideAsset.HasLocalOrientationOverride,
                LocalOrientation = overrideAsset.LocalOrientation
            };
        }

        static SceneEntityPlatformComponentOverrideAsset CloneComponentOverride(SceneEntityPlatformComponentOverrideAsset overrideAsset) {
            if (overrideAsset == null) {
                return null;
            }

            return new SceneEntityPlatformComponentOverrideAsset {
                PlatformId = overrideAsset.PlatformId,
                RemovedComponentKeys = (overrideAsset.RemovedComponentKeys ?? Array.Empty<string>()).ToArray(),
                AddedComponents = (overrideAsset.AddedComponents ?? Array.Empty<SceneEntityPlatformAddedComponentAsset>())
                    .Select(CloneAddedComponent)
                    .ToArray()
            };
        }

        static SceneEntityPlatformAddedComponentAsset CloneAddedComponent(SceneEntityPlatformAddedComponentAsset addedComponent) {
            if (addedComponent == null) {
                return null;
            }

            return new SceneEntityPlatformAddedComponentAsset {
                Component = CloneComponentRecord(addedComponent.Component)
            };
        }

        /// <summary>
        /// Resolver that fails if blueprint instance metadata unexpectedly tries to materialize runtime asset references.
        /// </summary>
        sealed class NullSceneAssetReferenceResolver : ISceneAssetReferenceResolver {
            public RuntimeModel ResolveModel(SceneAssetReference reference) {
                throw new InvalidOperationException("Blueprint instance metadata must not require runtime model resolution during packaging.");
            }

            public RuntimeMaterial ResolveMaterial(SceneAssetReference reference) {
                throw new InvalidOperationException("Blueprint instance metadata must not require runtime material resolution during packaging.");
            }

            public FontAsset ResolveFont(SceneAssetReference reference) {
                throw new InvalidOperationException("Blueprint instance metadata must not require font resolution during packaging.");
            }

            public RuntimeTexture ResolveTexture(SceneAssetReference reference) {
                throw new InvalidOperationException("Blueprint instance metadata must not require texture resolution during packaging.");
            }

            public AnimationClipAsset ResolveAnimationClip(SceneAssetReference reference) {
                throw new InvalidOperationException("Blueprint instance metadata must not require animation clip resolution during packaging.");
            }
        }
    }
}

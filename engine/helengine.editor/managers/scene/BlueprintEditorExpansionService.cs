namespace helengine.editor {
    /// <summary>
    /// Expands blueprint instance roots into inherited read-only scene subtrees for editor visualization.
    /// </summary>
    public sealed class BlueprintEditorExpansionService {
        /// <summary>
        /// Absolute path to the project root.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Blueprint-load service reused to materialize source blueprint entities.
        /// </summary>
        readonly BlueprintLoadService BlueprintLoadService;

        /// <summary>
        /// Initializes a new blueprint editor expansion service.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the assets folder.</param>
        /// <param name="persistenceRegistry">Registry used to deserialize supported component types.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime-backed assets.</param>
        public BlueprintEditorExpansionService(
            string projectRootPath,
            ComponentPersistenceRegistry persistenceRegistry,
            ISceneAssetReferenceResolver referenceResolver) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (persistenceRegistry == null) {
                throw new ArgumentNullException(nameof(persistenceRegistry));
            }
            if (referenceResolver == null) {
                throw new ArgumentNullException(nameof(referenceResolver));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            BlueprintLoadService = new BlueprintLoadService(persistenceRegistry, referenceResolver);
        }

        /// <summary>
        /// Expands one scene-owned blueprint instance root into inherited children.
        /// </summary>
        /// <param name="instanceRoot">Scene-owned instance root to expand.</param>
        public void ExpandInstanceRoot(EditorEntity instanceRoot) {
            if (instanceRoot == null) {
                throw new ArgumentNullException(nameof(instanceRoot));
            }

            BlueprintInstanceComponent instanceComponent = FindBlueprintInstanceComponent(instanceRoot);
            if (instanceComponent == null || string.IsNullOrWhiteSpace(instanceComponent.BlueprintAssetPath)) {
                return;
            }

            RemoveExpandedChildren(instanceRoot);

            string blueprintFullPath = ResolveBlueprintFullPath(instanceComponent.BlueprintAssetPath);
            using FileStream stream = new FileStream(blueprintFullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Asset asset = AssetSerializer.Deserialize(stream);
            if (asset is not BlueprintAsset blueprintAsset) {
                throw new InvalidOperationException("Blueprint instance reference did not deserialize into a BlueprintAsset.");
            }

            LoadedEditorBlueprintDocument loadedBlueprint = BlueprintLoadService.Load(blueprintAsset);
            if (loadedBlueprint.RootEntity == null) {
                throw new InvalidOperationException("Blueprint instance expansion did not materialize a root entity.");
            }

            MarkInheritedSubtree(loadedBlueprint.RootEntity, instanceComponent.BlueprintAssetPath);
            instanceRoot.AddChild(loadedBlueprint.RootEntity);
        }

        /// <summary>
        /// Removes previously expanded inherited children from one instance root before re-expanding it.
        /// </summary>
        /// <param name="instanceRoot">Instance root whose inherited children should be removed.</param>
        void RemoveExpandedChildren(EditorEntity instanceRoot) {
            if (instanceRoot.Children == null || instanceRoot.Children.Count < 1) {
                return;
            }

            List<EditorEntity> inheritedChildren = new List<EditorEntity>();
            for (int i = 0; i < instanceRoot.Children.Count; i++) {
                if (instanceRoot.Children[i] is not EditorEntity childEntity) {
                    continue;
                }
                if (!BlueprintSceneSaveFilterService.ShouldSerializeEntity(childEntity)) {
                    inheritedChildren.Add(childEntity);
                }
            }

            for (int i = 0; i < inheritedChildren.Count; i++) {
                inheritedChildren[i].Enabled = false;
                Core.Instance.ObjectManager.RemoveEntity(inheritedChildren[i]);
            }
        }

        /// <summary>
        /// Marks one loaded blueprint subtree as inherited read-only content.
        /// </summary>
        /// <param name="entity">Entity at the current subtree root.</param>
        /// <param name="blueprintAssetPath">Project-relative blueprint asset path that produced the subtree.</param>
        void MarkInheritedSubtree(EditorEntity entity, string blueprintAssetPath) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            uint sourceEntityId = 0u;
            EntitySaveComponent saveComponent = FindEntitySaveComponent(entity);
            if (saveComponent != null) {
                sourceEntityId = saveComponent.EntityId;
            }

            entity.AddComponent(new BlueprintInheritedEntityComponent {
                BlueprintAssetPath = blueprintAssetPath ?? string.Empty,
                SourceEntityId = sourceEntityId
            });

            if (entity.Components != null) {
                List<Component> visibleComponents = new List<Component>();
                for (int i = 0; i < entity.Components.Count; i++) {
                    Component component = entity.Components[i];
                    if (component == null || component is IEditorHiddenComponent) {
                        continue;
                    }
                    if (component is BlueprintInheritedEntityComponent || component is BlueprintInheritedComponentMarker) {
                        continue;
                    }

                    visibleComponents.Add(component);
                }

                for (int i = 0; i < visibleComponents.Count; i++) {
                    string sourceComponentKey = string.Empty;
                    if (saveComponent != null &&
                        saveComponent.TryGetComponentState(visibleComponents[i], out EntityComponentSaveState saveState) &&
                        !string.IsNullOrWhiteSpace(saveState.ComponentKey)) {
                        sourceComponentKey = saveState.ComponentKey;
                    }

                    entity.AddComponent(new BlueprintInheritedComponentMarker {
                        BlueprintAssetPath = blueprintAssetPath ?? string.Empty,
                        SourceEntityId = sourceEntityId,
                        SourceComponentKey = sourceComponentKey,
                        TargetComponentTypeId = visibleComponents[i].GetType().FullName ?? visibleComponents[i].GetType().Name
                    });
                }
            }

            if (entity.Children == null) {
                return;
            }

            for (int i = 0; i < entity.Children.Count; i++) {
                if (entity.Children[i] is EditorEntity childEntity) {
                    MarkInheritedSubtree(childEntity, blueprintAssetPath);
                }
            }
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
        /// Resolves the blueprint instance component attached to one scene-owned instance root.
        /// </summary>
        /// <param name="entity">Scene-owned entity to inspect.</param>
        /// <returns>Attached blueprint instance component when present; otherwise null.</returns>
        BlueprintInstanceComponent FindBlueprintInstanceComponent(EditorEntity entity) {
            if (entity?.Components == null) {
                return null;
            }

            for (int i = 0; i < entity.Components.Count; i++) {
                if (entity.Components[i] is BlueprintInstanceComponent instanceComponent) {
                    return instanceComponent;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the hidden save component attached to one editor entity.
        /// </summary>
        /// <param name="entity">Entity whose save component should be returned.</param>
        /// <returns>Attached hidden save component when present; otherwise null.</returns>
        EntitySaveComponent FindEntitySaveComponent(EditorEntity entity) {
            if (entity?.Components == null) {
                return null;
            }

            for (int i = 0; i < entity.Components.Count; i++) {
                if (entity.Components[i] is EntitySaveComponent saveComponent) {
                    return saveComponent;
                }
            }

            return null;
        }
    }
}

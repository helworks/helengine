namespace helengine.editor {
    /// <summary>
    /// Applies high-level per-platform entity and component existence authoring rules without forcing scene generators to manipulate low-level save metadata directly.
    /// </summary>
    public sealed class PlatformSceneAuthoringHelperService {
        /// <summary>
        /// Entity existence editing service used to persist per-platform entity visibility rules.
        /// </summary>
        readonly EntityPlatformExistenceEditingService EntityExistenceEditingService;

        /// <summary>
        /// Component existence editing service used to persist per-platform component visibility rules.
        /// </summary>
        readonly ComponentPlatformEditingService ComponentPlatformEditingService;

        /// <summary>
        /// Initializes one high-level platform scene authoring helper.
        /// </summary>
        public PlatformSceneAuthoringHelperService() {
            EntityExistenceEditingService = new EntityPlatformExistenceEditingService();
            ComponentPlatformEditingService = new ComponentPlatformEditingService();
        }

        /// <summary>
        /// Restricts the supplied entity subtree so it exists only on the requested target platforms and is pruned from every other project-supported platform.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root whose supported platform list should drive the authored overrides.</param>
        /// <param name="rootEntity">Root entity whose entire subtree should be constrained.</param>
        /// <param name="includedPlatformIds">Platform identifiers that should keep the subtree.</param>
        public void RestrictEntitySubtreeToPlatforms(string projectRootPath, EditorEntity rootEntity, IReadOnlyList<string> includedPlatformIds) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            } else if (rootEntity == null) {
                throw new ArgumentNullException(nameof(rootEntity));
            } else if (includedPlatformIds == null) {
                throw new ArgumentNullException(nameof(includedPlatformIds));
            }

            HashSet<string> includedPlatformSet = BuildIncludedPlatformSet(includedPlatformIds);
            IReadOnlyList<string> supportedPlatformIds = LoadSupportedPlatformIds(projectRootPath);
            ApplyEntitySubtreePlatformRestrictions(rootEntity, supportedPlatformIds, includedPlatformSet);
        }

        /// <summary>
        /// Excludes the supplied entity subtree from the requested target platforms while keeping it on every other project-supported platform.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root whose supported platform list should drive the authored overrides.</param>
        /// <param name="rootEntity">Root entity whose entire subtree should be excluded.</param>
        /// <param name="excludedPlatformIds">Platform identifiers that should prune the subtree.</param>
        public void ExcludeEntitySubtreeFromPlatforms(string projectRootPath, EditorEntity rootEntity, IReadOnlyList<string> excludedPlatformIds) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            } else if (rootEntity == null) {
                throw new ArgumentNullException(nameof(rootEntity));
            } else if (excludedPlatformIds == null) {
                throw new ArgumentNullException(nameof(excludedPlatformIds));
            }

            HashSet<string> excludedPlatformSet = BuildIncludedPlatformSet(excludedPlatformIds);
            IReadOnlyList<string> supportedPlatformIds = LoadSupportedPlatformIds(projectRootPath);
            ApplyEntitySubtreePlatformExclusions(rootEntity, supportedPlatformIds, excludedPlatformSet);
        }

        /// <summary>
        /// Restricts one common live component so it exists only on the requested target platforms and is removed everywhere else.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root whose supported platform list should drive the authored overrides.</param>
        /// <param name="ownerEntity">Entity that owns the common live component.</param>
        /// <param name="component">Common live component that should be constrained.</param>
        /// <param name="includedPlatformIds">Platform identifiers that should keep the component.</param>
        public void RestrictComponentToPlatforms(string projectRootPath, EditorEntity ownerEntity, Component component, IReadOnlyList<string> includedPlatformIds) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            } else if (ownerEntity == null) {
                throw new ArgumentNullException(nameof(ownerEntity));
            } else if (component == null) {
                throw new ArgumentNullException(nameof(component));
            } else if (includedPlatformIds == null) {
                throw new ArgumentNullException(nameof(includedPlatformIds));
            }

            EntitySaveComponent saveComponent = EnsureEntitySaveComponent(ownerEntity);
            HashSet<string> includedPlatformSet = BuildIncludedPlatformSet(includedPlatformIds);
            IReadOnlyList<string> supportedPlatformIds = LoadSupportedPlatformIds(projectRootPath);
            for (int index = 0; index < supportedPlatformIds.Count; index++) {
                string supportedPlatformId = supportedPlatformIds[index];
                if (includedPlatformSet.Contains(supportedPlatformId)) {
                    ComponentPlatformEditingService.RevertComponentExistenceOverride(component, saveComponent, supportedPlatformId);
                    continue;
                }

                if (!ComponentPlatformEditingService.IsComponentRemoved(component, saveComponent, supportedPlatformId)) {
                    ComponentPlatformEditingService.RemoveComponent(component, saveComponent, supportedPlatformId);
                }
            }
        }

        /// <summary>
        /// Excludes one common live component from the requested target platforms while preserving it everywhere else.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root whose supported platform list should drive the authored overrides.</param>
        /// <param name="ownerEntity">Entity that owns the common live component.</param>
        /// <param name="component">Common live component that should be excluded.</param>
        /// <param name="excludedPlatformIds">Platform identifiers that should prune the component.</param>
        public void ExcludeComponentFromPlatforms(string projectRootPath, EditorEntity ownerEntity, Component component, IReadOnlyList<string> excludedPlatformIds) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            } else if (ownerEntity == null) {
                throw new ArgumentNullException(nameof(ownerEntity));
            } else if (component == null) {
                throw new ArgumentNullException(nameof(component));
            } else if (excludedPlatformIds == null) {
                throw new ArgumentNullException(nameof(excludedPlatformIds));
            }

            EntitySaveComponent saveComponent = EnsureEntitySaveComponent(ownerEntity);
            HashSet<string> excludedPlatformSet = BuildIncludedPlatformSet(excludedPlatformIds);
            IReadOnlyList<string> supportedPlatformIds = LoadSupportedPlatformIds(projectRootPath);
            for (int index = 0; index < supportedPlatformIds.Count; index++) {
                string supportedPlatformId = supportedPlatformIds[index];
                if (excludedPlatformSet.Contains(supportedPlatformId)) {
                    if (!ComponentPlatformEditingService.IsComponentRemoved(component, saveComponent, supportedPlatformId)) {
                        ComponentPlatformEditingService.RemoveComponent(component, saveComponent, supportedPlatformId);
                    }

                    continue;
                }

                ComponentPlatformEditingService.RevertComponentExistenceOverride(component, saveComponent, supportedPlatformId);
            }
        }

        /// <summary>
        /// Applies the resolved platform restrictions recursively to one entity subtree.
        /// </summary>
        /// <param name="entity">Current subtree entity.</param>
        /// <param name="supportedPlatformIds">Supported project platform identifiers.</param>
        /// <param name="includedPlatformSet">Normalized platform identifiers that should keep the subtree.</param>
        void ApplyEntitySubtreePlatformRestrictions(
            EditorEntity entity,
            IReadOnlyList<string> supportedPlatformIds,
            HashSet<string> includedPlatformSet) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            } else if (supportedPlatformIds == null) {
                throw new ArgumentNullException(nameof(supportedPlatformIds));
            } else if (includedPlatformSet == null) {
                throw new ArgumentNullException(nameof(includedPlatformSet));
            }

            EntitySaveComponent saveComponent = EnsureEntitySaveComponent(entity);
            for (int index = 0; index < supportedPlatformIds.Count; index++) {
                string supportedPlatformId = supportedPlatformIds[index];
                EntityExistenceEditingService.SetExists(saveComponent, supportedPlatformId, includedPlatformSet.Contains(supportedPlatformId));
            }

            if (entity.Children == null) {
                return;
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                if (entity.Children[childIndex] is EditorEntity childEntity) {
                    ApplyEntitySubtreePlatformRestrictions(childEntity, supportedPlatformIds, includedPlatformSet);
                }
            }
        }

        /// <summary>
        /// Applies the resolved platform exclusions recursively to one entity subtree.
        /// </summary>
        /// <param name="entity">Current subtree entity.</param>
        /// <param name="supportedPlatformIds">Supported project platform identifiers.</param>
        /// <param name="excludedPlatformSet">Normalized platform identifiers that should prune the subtree.</param>
        void ApplyEntitySubtreePlatformExclusions(
            EditorEntity entity,
            IReadOnlyList<string> supportedPlatformIds,
            HashSet<string> excludedPlatformSet) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            } else if (supportedPlatformIds == null) {
                throw new ArgumentNullException(nameof(supportedPlatformIds));
            } else if (excludedPlatformSet == null) {
                throw new ArgumentNullException(nameof(excludedPlatformSet));
            }

            EntitySaveComponent saveComponent = EnsureEntitySaveComponent(entity);
            for (int index = 0; index < supportedPlatformIds.Count; index++) {
                string supportedPlatformId = supportedPlatformIds[index];
                EntityExistenceEditingService.SetExists(saveComponent, supportedPlatformId, !excludedPlatformSet.Contains(supportedPlatformId));
            }

            if (entity.Children == null) {
                return;
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                if (entity.Children[childIndex] is EditorEntity childEntity) {
                    ApplyEntitySubtreePlatformExclusions(childEntity, supportedPlatformIds, excludedPlatformSet);
                }
            }
        }

        /// <summary>
        /// Loads the normalized supported platform list for the supplied project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        /// <returns>Normalized supported platform list.</returns>
        static IReadOnlyList<string> LoadSupportedPlatformIds(string projectRootPath) {
            EditorProjectPlatformsDocument platformsDocument = new EditorProjectPlatformsService(projectRootPath).Load();
            if (platformsDocument.SupportedPlatforms == null || platformsDocument.SupportedPlatforms.Count < 1) {
                throw new InvalidOperationException("Platform scene authoring requires at least one supported platform in settings/platforms.json.");
            }

            return platformsDocument.SupportedPlatforms;
        }

        /// <summary>
        /// Builds one case-insensitive set of included target platforms.
        /// </summary>
        /// <param name="includedPlatformIds">Raw platform identifiers supplied by the caller.</param>
        /// <returns>Normalized included platform set.</returns>
        static HashSet<string> BuildIncludedPlatformSet(IReadOnlyList<string> includedPlatformIds) {
            if (includedPlatformIds == null) {
                throw new ArgumentNullException(nameof(includedPlatformIds));
            }

            HashSet<string> includedPlatformSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < includedPlatformIds.Count; index++) {
                string platformId = includedPlatformIds[index];
                if (string.IsNullOrWhiteSpace(platformId)) {
                    continue;
                }

                includedPlatformSet.Add(platformId.Trim());
            }

            if (includedPlatformSet.Count < 1) {
                throw new InvalidOperationException("Platform scene authoring requires at least one included platform.");
            }

            return includedPlatformSet;
        }

        /// <summary>
        /// Resolves the hidden save component attached to one editor entity, creating it when the generated subtree does not already carry one.
        /// </summary>
        /// <param name="entity">Entity whose hidden save component should be returned.</param>
        /// <returns>Attached hidden save component.</returns>
        static EntitySaveComponent EnsureEntitySaveComponent(EditorEntity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (entity.Components != null) {
                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    if (entity.Components[componentIndex] is EntitySaveComponent saveComponent) {
                        return saveComponent;
                    }
                }
            }

            EntitySaveComponent createdSaveComponent = new EntitySaveComponent();
            entity.AddComponent(createdSaveComponent);
            return createdSaveComponent;
        }
    }
}

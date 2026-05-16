namespace helengine.editor {
    /// <summary>
    /// Ensures platform-generated menu scene assets exist before build systems resolve selected scene ids to authored scene files.
    /// </summary>
    public sealed class EditorGeneratedMenuScenePreparationService {
        /// <summary>
        /// Absolute project root path that owns the source assets directory.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Script type resolver used to rebuild generated menu scenes from persisted provider type names.
        /// </summary>
        readonly IScriptTypeResolver ScriptTypeResolver;

        /// <summary>
        /// Project scene catalog used to resolve stable scene ids back to authored source paths.
        /// </summary>
        readonly EditorProjectSceneCatalogService SceneCatalogService;

        /// <summary>
        /// Menu component persistence descriptor used to read the provider type stored on the desktop menu root.
        /// </summary>
        readonly MenuComponentPersistenceDescriptor MenuComponentDescriptor;

        /// <summary>
        /// Initializes one generated menu-scene preparation service for the supplied project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        /// <param name="scriptTypeResolver">Script type resolver used by generated menu scene regeneration.</param>
        public EditorGeneratedMenuScenePreparationService(string projectRootPath, IScriptTypeResolver scriptTypeResolver) {
            ProjectRootPath = string.IsNullOrWhiteSpace(projectRootPath)
                ? throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath))
                : Path.GetFullPath(projectRootPath);
            ScriptTypeResolver = scriptTypeResolver;
            SceneCatalogService = new EditorProjectSceneCatalogService(ProjectRootPath);
            MenuComponentDescriptor = new MenuComponentPersistenceDescriptor();
        }

        /// <summary>
        /// Ensures any generated platform menu scenes referenced by the supplied build selection exist on disk before later build phases run.
        /// </summary>
        /// <param name="sceneIds">Stable scene ids selected for the build.</param>
        public void EnsurePrepared(IReadOnlyList<string> sceneIds) {
            if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            }
            if (!ContainsSceneId(sceneIds, PlatformMenuSceneResolver.NintendoDsMainMenuSceneId)) {
                return;
            }
            if (TryResolveExistingScenePath(PlatformMenuSceneResolver.NintendoDsMainMenuSceneId, out _)) {
                return;
            }
            if (ScriptTypeResolver == null) {
                throw new InvalidOperationException(
                    $"Generated menu scene '{PlatformMenuSceneResolver.NintendoDsMainMenuSceneId}' is missing and no script type resolver is available to rebuild it.");
            }

            string desktopSceneRelativePath = SceneCatalogService.ResolveScenePath(PlatformMenuSceneResolver.DesktopMainMenuSceneId);
            string providerTypeName = ReadMenuProviderTypeName(desktopSceneRelativePath);
            string nintendoDsSceneRelativePath = BuildNintendoDsSceneRelativePath(desktopSceneRelativePath);
            EditorMenuSceneRegenerationService regenerationService = new EditorMenuSceneRegenerationService(ProjectRootPath, ScriptTypeResolver);
            regenerationService.Regenerate(nintendoDsSceneRelativePath, providerTypeName);
        }

        /// <summary>
        /// Resolves whether the supplied scene id is already part of the selected build set.
        /// </summary>
        /// <param name="sceneIds">Stable scene ids selected for the build.</param>
        /// <param name="sceneId">Stable scene id to search for.</param>
        /// <returns>True when the scene id is present.</returns>
        static bool ContainsSceneId(IReadOnlyList<string> sceneIds, string sceneId) {
            if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            }
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            for (int index = 0; index < sceneIds.Count; index++) {
                if (string.Equals(sceneIds[index], sceneId, StringComparison.Ordinal)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to resolve one authored scene path for the supplied stable scene id.
        /// </summary>
        /// <param name="sceneId">Stable scene id to resolve.</param>
        /// <param name="relativeScenePath">Resolved project-relative scene path when found.</param>
        /// <returns>True when the scene id resolved successfully.</returns>
        bool TryResolveExistingScenePath(string sceneId, out string relativeScenePath) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            try {
                relativeScenePath = SceneCatalogService.ResolveScenePath(sceneId);
                return true;
            } catch (InvalidOperationException) {
                relativeScenePath = string.Empty;
                return false;
            }
        }

        /// <summary>
        /// Reads the persisted menu provider type id from one authored desktop menu scene asset.
        /// </summary>
        /// <param name="relativeScenePath">Project-relative authored desktop menu scene path.</param>
        /// <returns>Persisted assembly-qualified menu provider type name.</returns>
        string ReadMenuProviderTypeName(string relativeScenePath) {
            if (string.IsNullOrWhiteSpace(relativeScenePath)) {
                throw new ArgumentException("Relative scene path must be provided.", nameof(relativeScenePath));
            }

            string fullScenePath = Path.Combine(ProjectRootPath, "assets", relativeScenePath.Replace('/', Path.DirectorySeparatorChar));
            using FileStream stream = File.OpenRead(fullScenePath);
            Asset asset = EditorAssetBinarySerializer.Deserialize(stream);
            if (asset is not SceneAsset sceneAsset) {
                throw new InvalidOperationException($"Desktop menu scene '{relativeScenePath}' did not deserialize into a SceneAsset.");
            }

            string providerTypeName = ReadMenuProviderTypeName(sceneAsset.RootEntities);
            if (string.IsNullOrWhiteSpace(providerTypeName)) {
                throw new InvalidOperationException($"Desktop menu scene '{relativeScenePath}' did not contain a serialized menu provider type.");
            }

            return providerTypeName;
        }

        /// <summary>
        /// Reads the first serialized menu provider type id found within the supplied scene hierarchy.
        /// </summary>
        /// <param name="entities">Scene entities whose serialized components should be inspected.</param>
        /// <returns>First non-empty serialized provider type id, or an empty string when absent.</returns>
        string ReadMenuProviderTypeName(SceneEntityAsset[] entities) {
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            }

            for (int index = 0; index < entities.Length; index++) {
                SceneEntityAsset entity = entities[index];
                if (entity == null) {
                    continue;
                }

                string providerTypeName = ReadMenuProviderTypeName(entity.Components ?? Array.Empty<SceneComponentAssetRecord>());
                if (!string.IsNullOrWhiteSpace(providerTypeName)) {
                    return providerTypeName;
                }
                if (entity.Children == null || entity.Children.Length == 0) {
                    continue;
                }

                providerTypeName = ReadMenuProviderTypeName(entity.Children);
                if (!string.IsNullOrWhiteSpace(providerTypeName)) {
                    return providerTypeName;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Reads the first serialized menu provider type id found within the supplied component collection.
        /// </summary>
        /// <param name="components">Serialized components owned by one scene entity.</param>
        /// <returns>First non-empty serialized provider type id, or an empty string when absent.</returns>
        string ReadMenuProviderTypeName(SceneComponentAssetRecord[] components) {
            if (components == null) {
                throw new ArgumentNullException(nameof(components));
            }

            for (int index = 0; index < components.Length; index++) {
                SceneComponentAssetRecord record = components[index];
                if (!string.Equals(record.ComponentTypeId, MenuComponent.SerializedComponentTypeId, StringComparison.Ordinal)) {
                    continue;
                }

                Component component = MenuComponentDescriptor.DeserializeComponent(record, null, null);
                if (component is not MenuComponent menuComponent) {
                    throw new InvalidOperationException("Serialized menu component did not deserialize into a MenuComponent.");
                }
                if (!string.IsNullOrWhiteSpace(menuComponent.ProviderTypeName)) {
                    return menuComponent.ProviderTypeName;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Builds the authored Nintendo DS menu scene path beside the existing desktop menu scene asset.
        /// </summary>
        /// <param name="desktopSceneRelativePath">Project-relative authored desktop menu scene path.</param>
        /// <returns>Project-relative authored Nintendo DS menu scene path.</returns>
        static string BuildNintendoDsSceneRelativePath(string desktopSceneRelativePath) {
            if (string.IsNullOrWhiteSpace(desktopSceneRelativePath)) {
                throw new ArgumentException("Desktop scene relative path must be provided.", nameof(desktopSceneRelativePath));
            }

            string desktopDirectoryPath = Path.GetDirectoryName(desktopSceneRelativePath.Replace('/', Path.DirectorySeparatorChar))
                ?? throw new InvalidOperationException("Desktop menu scene path did not include a containing directory.");
            return Path.Combine(desktopDirectoryPath, PlatformMenuSceneResolver.NintendoDsMainMenuSceneId + ".helen").Replace('\\', '/');
        }
    }
}

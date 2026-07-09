namespace helengine.editor {
    /// <summary>
    /// Serializes the current editor blueprint authoring state into one `.hblueprint` asset stored under the project assets folder.
    /// </summary>
    public class BlueprintSaveService {
        /// <summary>
        /// Absolute path to the project root.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Absolute path to the project assets root.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Shared scene-save service reused to serialize the blueprint subtree payload.
        /// </summary>
        readonly SceneSaveService SceneSaveService;

        /// <summary>
        /// Initializes a new blueprint save service for one project root.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the assets folder.</param>
        /// <param name="persistenceRegistry">Registry used to serialize persisted components.</param>
        public BlueprintSaveService(string projectRootPath, ComponentPersistenceRegistry persistenceRegistry) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (persistenceRegistry == null) {
                throw new ArgumentNullException(nameof(persistenceRegistry));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.GetFullPath(Path.Combine(ProjectRootPath, "assets"));
            SceneSaveService = new SceneSaveService(ProjectRootPath, persistenceRegistry);
        }

        /// <summary>
        /// Saves the current blueprint authoring state to one `.hblueprint` file on disk.
        /// </summary>
        /// <param name="fullPath">Absolute path where the blueprint file should be written.</param>
        public void Save(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Blueprint path must be provided.", nameof(fullPath));
            }

            EditorEntity rootEntity = BlueprintValidationService.ResolveSingleEditableRoot(Core.Instance.ObjectManager.Entities);
            BlueprintValidationService.ValidateRootForSave(rootEntity);

            string normalizedPath = Path.GetFullPath(fullPath);
            if (!IsPathInsideAssetsRoot(normalizedPath)) {
                throw new InvalidOperationException("Blueprint files must be stored inside the project assets folder.");
            }

            string directoryPath = Path.GetDirectoryName(normalizedPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Blueprint path does not include a writable directory.");
            }

            Directory.CreateDirectory(directoryPath);

            string tempScenePath = Path.Combine(
                AssetsRootPath,
                ".blueprint-temp",
                Guid.NewGuid().ToString("N") + SceneAsset.FileExtension);

            try {
                string tempDirectoryPath = Path.GetDirectoryName(tempScenePath);
                if (string.IsNullOrWhiteSpace(tempDirectoryPath)) {
                    throw new InvalidOperationException("Blueprint temporary save directory could not be resolved.");
                }

                Directory.CreateDirectory(tempDirectoryPath);
                SceneSaveService.Save(tempScenePath);

                SceneAsset sceneAsset;
                using (FileStream stream = File.OpenRead(tempScenePath)) {
                    sceneAsset = AssertSceneAsset(AssetSerializer.Deserialize(stream));
                }

                SceneEntityAsset[] rootEntities = sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>();
                if (rootEntities.Length != 1) {
                    throw new InvalidOperationException("Blueprint save must serialize exactly one editable root entity.");
                }

                BlueprintAsset blueprintAsset = new BlueprintAsset {
                    Id = BuildBlueprintId(normalizedPath),
                    RootEntity = rootEntities[0],
                    AssetReferences = sceneAsset.AssetReferences ?? Array.Empty<SceneAssetReference>()
                };

                using FileStream outputStream = new FileStream(normalizedPath, FileMode.Create, FileAccess.Write, FileShare.None);
                AssetSerializer.Serialize(outputStream, blueprintAsset);
            } finally {
                if (File.Exists(tempScenePath)) {
                    File.Delete(tempScenePath);
                }
            }
        }

        /// <summary>
        /// Casts one deserialized asset to the expected scene asset type.
        /// </summary>
        /// <param name="asset">Deserialized asset to validate.</param>
        /// <returns>Validated scene asset.</returns>
        static SceneAsset AssertSceneAsset(Asset asset) {
            if (asset is SceneAsset sceneAsset) {
                return sceneAsset;
            }

            throw new InvalidOperationException("Blueprint intermediate save did not deserialize into a SceneAsset.");
        }

        /// <summary>
        /// Builds the project-relative blueprint asset id for one output file path.
        /// </summary>
        /// <param name="fullPath">Absolute file path where the blueprint will be stored.</param>
        /// <returns>Project-relative blueprint asset id stored inside the blueprint file.</returns>
        string BuildBlueprintId(string fullPath) {
            string normalizedPath = Path.GetFullPath(fullPath);
            if (!IsPathInsideAssetsRoot(normalizedPath)) {
                throw new InvalidOperationException("Blueprint files must be stored inside the project assets folder.");
            }

            return Path.GetRelativePath(AssetsRootPath, normalizedPath).Replace('\\', '/');
        }

        /// <summary>
        /// Determines whether one full path points inside the project assets folder.
        /// </summary>
        /// <param name="fullPath">Absolute path to validate.</param>
        /// <returns>True when the path points inside the assets folder.</returns>
        bool IsPathInsideAssetsRoot(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                return false;
            }
            if (string.Equals(fullPath, AssetsRootPath, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            string rootWithSeparator = AssetsRootPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? AssetsRootPath
                : AssetsRootPath + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }
    }
}

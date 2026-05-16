namespace helengine.editor {
    /// <summary>
    /// Regenerates baked menu scene assets inside one project by routing authored menu definitions through the normal scene serializer.
    /// </summary>
    public sealed class EditorMenuSceneRegenerationService {
        /// <summary>
        /// Absolute project root path that owns the target assets folder.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Absolute assets root path used to validate the target scene location.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Builder used to bake menu definitions into scene asset payloads.
        /// </summary>
        readonly DemoMenuSceneBuildService SceneBuildService;

        /// <summary>
        /// Initializes one editor menu scene regeneration service for the supplied project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        /// <param name="scriptTypeResolver">Resolver backed by the currently loaded gameplay assemblies.</param>
        public EditorMenuSceneRegenerationService(string projectRootPath, IScriptTypeResolver scriptTypeResolver) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (scriptTypeResolver == null) {
                throw new ArgumentNullException(nameof(scriptTypeResolver));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.GetFullPath(Path.Combine(ProjectRootPath, "assets"));
            SceneBuildService = new DemoMenuSceneBuildService(scriptTypeResolver);
        }

        /// <summary>
        /// Rebuilds one menu scene asset beneath the project assets folder using the supplied menu definition provider.
        /// </summary>
        /// <param name="sceneId">Project-relative scene id stored inside the regenerated scene asset.</param>
        /// <param name="providerTypeName">Assembly-qualified provider type name used to bake the scene.</param>
        public void Regenerate(string sceneId, string providerTypeName) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }
            if (string.IsNullOrWhiteSpace(providerTypeName)) {
                throw new ArgumentException("Provider type name must be provided.", nameof(providerTypeName));
            }

            string normalizedSceneId = sceneId.Replace('\\', '/');
            string scenePath = ResolveScenePath(normalizedSceneId);
            DemoMenuSceneBuildVariant variant = ResolveBuildVariant(normalizedSceneId);
            SceneAsset sceneAsset = SceneBuildService.BuildSceneAsset(normalizedSceneId, providerTypeName, variant);
            string directoryPath = Path.GetDirectoryName(scenePath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Scene path does not include a writable directory.");
            }

            Directory.CreateDirectory(directoryPath);
            string temporaryScenePath = scenePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                using (FileStream stream = new FileStream(temporaryScenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                    AssetSerializer.Serialize(stream, sceneAsset);
                }

                File.Move(temporaryScenePath, scenePath, true);
            } catch {
                if (File.Exists(temporaryScenePath)) {
                    File.Delete(temporaryScenePath);
                }

                throw;
            }
        }

        /// <summary>
        /// Resolves one scene id to an absolute path beneath the project assets folder.
        /// </summary>
        /// <param name="sceneId">Project-relative scene id to resolve.</param>
        /// <returns>Absolute path for the regenerated scene file.</returns>
        string ResolveScenePath(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            string relativeScenePath = sceneId.Replace('/', Path.DirectorySeparatorChar);
            string fullScenePath = Path.GetFullPath(Path.Combine(AssetsRootPath, relativeScenePath));
            if (!IsPathInsideAssetsRoot(fullScenePath)) {
                throw new InvalidOperationException("Menu scenes must be regenerated inside the project assets folder.");
            }

            return fullScenePath;
        }

        /// <summary>
        /// Determines whether one absolute path is stored inside the project assets folder.
        /// </summary>
        /// <param name="fullPath">Absolute path to validate.</param>
        /// <returns>True when the path is inside the assets root.</returns>
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

        /// <summary>
        /// Resolves the menu layout variant that should be generated for the supplied scene id.
        /// </summary>
        /// <param name="sceneId">Project-relative scene id being regenerated.</param>
        /// <returns>Layout variant that should be emitted for the scene id.</returns>
        static DemoMenuSceneBuildVariant ResolveBuildVariant(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            string sceneName = Path.GetFileNameWithoutExtension(sceneId);
            if (string.Equals(sceneName, PlatformMenuSceneResolver.NintendoDsMainMenuSceneId, StringComparison.Ordinal)) {
                return DemoMenuSceneBuildVariant.NintendoDs;
            }

            return DemoMenuSceneBuildVariant.Desktop;
        }
    }
}

namespace helengine.editor {
    /// <summary>
    /// Resolves the unioned 3D physics scene feature mask and preprocessor symbols required by one build's selected scenes.
    /// </summary>
    public sealed class EditorPhysics3DCodegenFeatureSymbolService {
        /// <summary>
        /// Absolute project root that owns the source `assets` folder.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Absolute source assets root used to resolve project-relative scene ids.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Initializes one 3D physics codegen feature-symbol resolver for the supplied project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        public EditorPhysics3DCodegenFeatureSymbolService(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
        }

        /// <summary>
        /// Resolves the unioned 3D physics scene feature mask required by the supplied authored scene ids.
        /// </summary>
        /// <param name="sceneIds">Project-relative authored scene ids selected for the build.</param>
        /// <returns>Unioned 3D physics scene feature mask.</returns>
        public PhysicsSceneFeatureFlags3D ResolveFeatureFlags(IReadOnlyList<string> sceneIds) {
            if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            }

            PhysicsSceneFeatureFlags3D featureFlags = PhysicsSceneFeatureFlags3D.None;
            for (int index = 0; index < sceneIds.Count; index++) {
                SceneAsset sceneAsset = LoadSceneAsset(sceneIds[index]);
                featureFlags |= PhysicsSceneFeatureAnalyzer3D.Analyze(sceneAsset);
            }

            return featureFlags;
        }

        /// <summary>
        /// Resolves the ordered 3D physics preprocessor symbols required by the supplied authored scene ids.
        /// </summary>
        /// <param name="sceneIds">Project-relative authored scene ids selected for the build.</param>
        /// <returns>Ordered preprocessor symbols for generated-core stripping.</returns>
        public IReadOnlyList<string> ResolveSymbols(IReadOnlyList<string> sceneIds) {
            PhysicsSceneFeatureFlags3D featureFlags = ResolveFeatureFlags(sceneIds);
            return PhysicsSceneFeatureSymbolCatalog3D.BuildSymbols(featureFlags);
        }

        /// <summary>
        /// Loads one authored scene asset from the project source assets folder.
        /// </summary>
        /// <param name="sceneId">Project-relative scene id to load.</param>
        /// <returns>Loaded scene asset.</returns>
        SceneAsset LoadSceneAsset(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            string fullScenePath = ResolveProjectAssetPath(sceneId);
            using FileStream stream = File.OpenRead(fullScenePath);
            Asset asset = AssetSerializer.Deserialize(stream);
            if (asset is not SceneAsset sceneAsset) {
                throw new InvalidOperationException($"Scene '{sceneId}' did not deserialize into a SceneAsset.");
            }

            return sceneAsset;
        }

        /// <summary>
        /// Resolves one project-relative asset path beneath the source `assets` folder.
        /// </summary>
        /// <param name="relativePath">Project-relative asset path.</param>
        /// <returns>Absolute source asset path.</returns>
        string ResolveProjectAssetPath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            string normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(AssetsRootPath, normalizedRelativePath));
            string assetsRootPrefix = EnsureTrailingDirectorySeparator(AssetsRootPath);
            if (!fullPath.StartsWith(assetsRootPrefix, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Project asset paths must stay inside the source assets folder.");
            }

            return fullPath;
        }

        /// <summary>
        /// Ensures one directory path ends with a trailing separator before prefix comparisons occur.
        /// </summary>
        /// <param name="path">Directory path that should end with a separator.</param>
        /// <returns>Directory path with a trailing separator.</returns>
        string EnsureTrailingDirectorySeparator(string path) {
            if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)) {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}

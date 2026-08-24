using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Persists per-project editor session state, such as the last open scene, inside `user_settings`.
    /// </summary>
    public sealed class EditorSessionStateService {
        /// <summary>
        /// Shared JSON serialization options used for session state documents.
        /// </summary>
        static JsonSerializerOptions JsonSerializerOptions { get; } = new JsonSerializerOptions {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        static EditorSessionStateService() {
            JsonSerializerOptions.Converters.Add(new SceneAssetReferenceJsonConverter());
        }

        /// <summary>
        /// Absolute project root that owns the local session state file.
        /// </summary>
        string ProjectRootPath { get; }

        /// <summary>
        /// Absolute path to the session state file inside `user_settings`.
        /// </summary>
        string StateFilePath {
            get {
                return Path.Combine(ProjectRootPath, "user_settings", "editor_session.json");
            }
        }

        /// <summary>
        /// Initializes one session state service for the supplied project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        public EditorSessionStateService(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        /// <summary>
        /// Reads the absolute path of the last open scene recorded for this project.
        /// </summary>
        /// <returns>Absolute scene path, or null when no valid path is recorded.</returns>
        public string TryGetLastScenePath() {
            try {
                if (!File.Exists(StateFilePath)) {
                    return null;
                }

                EditorSessionStateDocument document = JsonSerializer.Deserialize<EditorSessionStateDocument>(File.ReadAllText(StateFilePath), JsonSerializerOptions);
                if (document?.LastSceneReference != null) {
                    return ResolveReferencePath(document.LastSceneReference);
                }
                if (string.IsNullOrWhiteSpace(document?.LastScenePath)) {
                    return null;
                }

                return Path.IsPathRooted(document.LastScenePath)
                    ? Path.GetFullPath(document.LastScenePath)
                    : Path.GetFullPath(Path.Combine(ProjectRootPath, document.LastScenePath));
            } catch (Exception ex) {
                // A corrupt state file must never block editor startup.
                Logger.WriteError($"Editor session state read failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Records the last open scene for this project, storing project-relative paths when possible.
        /// </summary>
        /// <param name="scenePath">Absolute path of the scene that is now open.</param>
        public void SetLastScenePath(string scenePath) {
            if (string.IsNullOrWhiteSpace(scenePath)) {
                throw new ArgumentException("Scene path must be provided.", nameof(scenePath));
            }

            try {
                string fullPath = Path.GetFullPath(scenePath);
                string storedPath = fullPath.StartsWith(ProjectRootPath, StringComparison.OrdinalIgnoreCase)
                    ? Path.GetRelativePath(ProjectRootPath, fullPath)
                    : fullPath;
                SceneAssetReference reference = null;
                string assetsRoot = Path.Combine(ProjectRootPath, "assets");
                if (fullPath.StartsWith(assetsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath)) {
                    AssetEntryKind kind = new EditorAssetPathClassifier().Classify(fullPath);
                    AssetIdentityMetadataService metadataService = new AssetIdentityMetadataService();
                    AssetIdentityMetadataDocument metadata = metadataService.LoadOrCreate(fullPath, string.Empty);
                    string hash = new EditorAssetHashCache(ProjectRootPath).GetContentHash(fullPath);
                    reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(metadata.AssetId, Path.GetRelativePath(assetsRoot, fullPath).Replace('\\', '/'), hash);
                }
                Directory.CreateDirectory(Path.Combine(ProjectRootPath, "user_settings"));
                File.WriteAllText(StateFilePath, JsonSerializer.Serialize(new EditorSessionStateDocument {
                    LastScenePath = reference == null ? storedPath : null,
                    LastSceneReference = reference
                }, JsonSerializerOptions));
            } catch (Exception ex) {
                // Failing to persist session state must never fail the scene operation that triggered it.
                Logger.WriteError($"Editor session state write failed: {ex.Message}");
            }
        }

        /// <summary>Resolves a typed scene reference to its current project path.</summary>
        string ResolveReferencePath(SceneAssetReference reference) {
            if (reference.SourceKind != SceneAssetReferenceSourceKind.FileSystem) {
                return Path.Combine(ProjectRootPath, reference.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            }
            EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(ProjectRootPath);
            AssetReferenceResolution resolution = resolver.Resolve(reference, AssetEntryKind.Scene);
            return resolution.FullPath;
        }
    }
}

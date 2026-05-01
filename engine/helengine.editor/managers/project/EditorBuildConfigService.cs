using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Loads and persists editor-local build configuration stored in `user_settings/build_config.json`.
    /// </summary>
    public sealed class EditorBuildConfigService {
        /// <summary>
        /// Gets the JSON formatting rules used for the local build configuration document.
        /// </summary>
        static JsonSerializerOptions JsonSerializerOptions { get; } = new() {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Gets the absolute path to the current project root directory.
        /// </summary>
        string ProjectRootPath { get; }

        /// <summary>
        /// Gets the absolute path to `user_settings/build_config.json`.
        /// </summary>
        string BuildConfigFilePath {
            get {
                return Path.Combine(ProjectRootPath, "user_settings", "build_config.json");
            }
        }

        /// <summary>
        /// Initializes one build-config service for the supplied project root directory.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative path to the current project root directory.</param>
        public EditorBuildConfigService(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        /// <summary>
        /// Loads the local build configuration, silently regenerating missing or invalid data and seeding newly enabled platforms.
        /// </summary>
        /// <param name="supportedPlatforms">Supported platform identifiers declared by the current project.</param>
        /// <param name="currentSceneId">Project-relative scene identifier used to seed first-time platform selections.</param>
        /// <returns>Validated local build configuration document for the current project.</returns>
        public EditorBuildConfigDocument Load(IReadOnlyList<string> supportedPlatforms, string currentSceneId) {
            if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            }

            EditorBuildConfigDocument document = TryLoadDocument();
            if (document == null) {
                document = new EditorBuildConfigDocument();
            }

            bool changed = EnsurePlatformEntries(document, supportedPlatforms, currentSceneId);
            if (!File.Exists(BuildConfigFilePath) || changed) {
                Save(document);
            }

            return document;
        }

        /// <summary>
        /// Persists the supplied local build configuration document to `user_settings/build_config.json`.
        /// </summary>
        /// <param name="document">Validated local build configuration to persist.</param>
        public void Save(EditorBuildConfigDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            string buildConfigDirectoryPath = Path.GetDirectoryName(BuildConfigFilePath);
            Directory.CreateDirectory(buildConfigDirectoryPath);

            string json = JsonSerializer.Serialize(document, JsonSerializerOptions);
            File.WriteAllText(BuildConfigFilePath, json);
        }

        /// <summary>
        /// Attempts to load the local build configuration document from disk.
        /// </summary>
        /// <returns>Loaded build configuration document, or null when the file is missing or malformed.</returns>
        EditorBuildConfigDocument TryLoadDocument() {
            if (!File.Exists(BuildConfigFilePath)) {
                return null;
            }

            try {
                string json = File.ReadAllText(BuildConfigFilePath);
                EditorBuildConfigDocument document = JsonSerializer.Deserialize<EditorBuildConfigDocument>(json, JsonSerializerOptions);
                if (document == null) {
                    return null;
                }

                document.Platforms ??= [];
                document.QueueItems ??= [];
                for (int index = 0; index < document.Platforms.Count; index++) {
                    EditorBuildPlatformConfigDocument platform = document.Platforms[index];
                    platform.SelectedSceneIds ??= [];
                    platform.SceneOrders ??= [];
                }
                return document;
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Ensures the supplied build document contains one platform configuration entry for each supported platform.
        /// </summary>
        /// <param name="document">Local build configuration document to normalize.</param>
        /// <param name="supportedPlatforms">Supported platform identifiers declared by the current project.</param>
        /// <param name="currentSceneId">Project-relative scene identifier used when seeding new platform entries.</param>
        /// <returns>True when the document changed; otherwise false.</returns>
        bool EnsurePlatformEntries(EditorBuildConfigDocument document, IReadOnlyList<string> supportedPlatforms, string currentSceneId) {
            bool changed = false;

            if (document.Platforms == null) {
                document.Platforms = [];
                changed = true;
            }

            if (document.QueueItems == null) {
                document.QueueItems = [];
                changed = true;
            }

            for (int i = 0; i < supportedPlatforms.Count; i++) {
                string platformId = supportedPlatforms[i];
                if (HasPlatformEntry(document.Platforms, platformId)) {
                    continue;
                }

                document.Platforms.Add(CreatePlatformDocument(platformId, currentSceneId));
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Returns true when the supplied platform collection already contains an entry for the requested platform identifier.
        /// </summary>
        /// <param name="platforms">Platform configuration collection to inspect.</param>
        /// <param name="platformId">Platform identifier to search for.</param>
        /// <returns>True when a matching platform configuration already exists; otherwise false.</returns>
        bool HasPlatformEntry(IReadOnlyList<EditorBuildPlatformConfigDocument> platforms, string platformId) {
            for (int i = 0; i < platforms.Count; i++) {
                if (string.Equals(platforms[i].PlatformId, platformId, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Creates one default local build configuration entry for the supplied platform identifier.
        /// </summary>
        /// <param name="platformId">Platform identifier the new configuration belongs to.</param>
        /// <param name="currentSceneId">Project-relative scene identifier used for first-time seeding.</param>
        /// <returns>New platform configuration document seeded for first-time use.</returns>
        EditorBuildPlatformConfigDocument CreatePlatformDocument(string platformId, string currentSceneId) {
            EditorBuildPlatformConfigDocument document = new EditorBuildPlatformConfigDocument {
                PlatformId = platformId,
                OutputDirectoryPath = string.Empty,
                DebugBuild = false
            };

            if (!string.IsNullOrWhiteSpace(currentSceneId)) {
                document.SelectedSceneIds.Add(currentSceneId);
            }

            return document;
        }
    }
}

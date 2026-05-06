using System.Text.Json;
using System.Text.Json.Serialization;

namespace helengine.editor {
    /// <summary>
    /// Loads and persists project-shared platform profile settings stored in `settings/platform.<platform-id>.json`.
    /// </summary>
    public sealed class EditorProfileSettingsService {
        /// <summary>
        /// Gets the JSON formatting rules used for the local profile settings document.
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
        /// Gets the absolute path to the shared `settings` directory.
        /// </summary>
        string SettingsDirectoryPath {
            get {
                return Path.Combine(ProjectRootPath, "settings");
            }
        }

        /// <summary>
        /// Gets the absolute path to the legacy `user_settings/profile_config.json` file.
        /// </summary>
        string LegacyProfileConfigFilePath {
            get {
                return Path.Combine(ProjectRootPath, "user_settings", "profile_config.json");
            }
        }

        /// <summary>
        /// Initializes one profile-settings service for the supplied project root directory.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative path to the current project root directory.</param>
        public EditorProfileSettingsService(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        /// <summary>
        /// Loads the profile settings document, seeding one normalized platform file for each requested platform.
        /// </summary>
        /// <param name="supportedPlatforms">Supported platform identifiers declared by the current project.</param>
        /// <returns>Validated platform profile settings document for the current project.</returns>
        public EditorProfileSettingsDocument Load(IReadOnlyList<string> supportedPlatforms) {
            if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            }

            EditorProfileSettingsDocument existingDocument = TryLoadExisting();
            EditorProfileSettingsDocument document = new EditorProfileSettingsDocument();
            for (int index = 0; index < supportedPlatforms.Count; index++) {
                string platformId = supportedPlatforms[index];
                EditorPlatformProfileSettingsDocument platform = FindPlatform(existingDocument == null ? null : existingDocument.Platforms, platformId);
                if (platform == null) {
                    platform = CreatePlatformDocument(platformId);
                } else {
                    NormalizePlatform(platform, platformId);
                }

                document.Platforms.Add(platform);
            }

            Save(document);
            return document;
        }

        /// <summary>
        /// Attempts to load existing profile settings without seeding new platform entries.
        /// </summary>
        /// <returns>Loaded profile settings document, or null when the file is missing or malformed.</returns>
        public EditorProfileSettingsDocument TryLoadExisting() {
            EditorProfileSettingsDocument document = TryLoadSplitDocument();
            if (document != null && document.Platforms.Count > 0) {
                return document;
            }

            document = TryLoadLegacyDocument();
            if (document == null) {
                return null;
            }

            Save(document);
            return TryLoadSplitDocument();
        }

        /// <summary>
        /// Persists the supplied profile settings document to one file per platform under `settings`.
        /// </summary>
        /// <param name="document">Validated platform profile settings to persist.</param>
        public void Save(EditorProfileSettingsDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            Directory.CreateDirectory(SettingsDirectoryPath);

            for (int index = 0; index < document.Platforms.Count; index++) {
                EditorPlatformProfileSettingsDocument platform = document.Platforms[index];
                if (platform == null) {
                    continue;
                }

                NormalizePlatform(platform, platform.PlatformId);
                string json = JsonSerializer.Serialize(platform, JsonSerializerOptions);
                File.WriteAllText(GetPlatformFilePath(platform.PlatformId), json);
            }
        }

        /// <summary>
        /// Gets the file path used for one platform's shared profile settings.
        /// </summary>
        /// <param name="platformId">Platform identifier that owns the settings file.</param>
        /// <returns>Absolute path to the platform settings file.</returns>
        string GetPlatformFilePath(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return Path.Combine(SettingsDirectoryPath, $"platform.{platformId}.json");
        }

        /// <summary>
        /// Attempts to load one aggregated profile settings document from per-platform files.
        /// </summary>
        /// <returns>Aggregated platform profile settings document, or null when no valid platform files exist.</returns>
        EditorProfileSettingsDocument TryLoadSplitDocument() {
            if (!Directory.Exists(SettingsDirectoryPath)) {
                return null;
            }

            string[] filePaths = Directory.GetFiles(SettingsDirectoryPath, "platform.*.json");
            Array.Sort(filePaths, StringComparer.OrdinalIgnoreCase);

            EditorProfileSettingsDocument document = new EditorProfileSettingsDocument();
            for (int index = 0; index < filePaths.Length; index++) {
                EditorPlatformProfileSettingsDocument platform = TryLoadPlatformDocument(filePaths[index]);
                if (platform == null || string.IsNullOrWhiteSpace(platform.PlatformId)) {
                    continue;
                }

                NormalizePlatform(platform, platform.PlatformId);
                document.Platforms.Add(platform);
            }

            if (document.Platforms.Count == 0) {
                return null;
            }

            return document;
        }

        /// <summary>
        /// Attempts to load the legacy combined profile settings document from `user_settings/profile_config.json`.
        /// </summary>
        /// <returns>Loaded legacy document, or null when the file is missing or malformed.</returns>
        EditorProfileSettingsDocument TryLoadLegacyDocument() {
            if (!File.Exists(LegacyProfileConfigFilePath)) {
                return null;
            }

            try {
                string json = File.ReadAllText(LegacyProfileConfigFilePath);
                EditorProfileSettingsDocument document = JsonSerializer.Deserialize<EditorProfileSettingsDocument>(json, JsonSerializerOptions);
                if (document == null || document.Platforms == null) {
                    return null;
                }

                for (int index = 0; index < document.Platforms.Count; index++) {
                    EditorPlatformProfileSettingsDocument platform = document.Platforms[index];
                    if (platform == null || string.IsNullOrWhiteSpace(platform.PlatformId)) {
                        continue;
                    }

                    NormalizePlatform(platform, platform.PlatformId);
                }

                return document;
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Attempts to load one per-platform profile document from the supplied file path.
        /// </summary>
        /// <param name="filePath">Absolute path to the per-platform profile file.</param>
        /// <returns>Loaded per-platform profile document, or null when the file is missing or malformed.</returns>
        EditorPlatformProfileSettingsDocument TryLoadPlatformDocument(string filePath) {
            if (!File.Exists(filePath)) {
                return null;
            }

            try {
                string json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<EditorPlatformProfileSettingsDocument>(json, JsonSerializerOptions);
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Finds the platform profile record for one platform id.
        /// </summary>
        /// <param name="platforms">Existing profile records.</param>
        /// <param name="platformId">Platform identifier to locate.</param>
        /// <returns>Matching platform profile record when present; otherwise null.</returns>
        EditorPlatformProfileSettingsDocument FindPlatform(IReadOnlyList<EditorPlatformProfileSettingsDocument> platforms, string platformId) {
            if (platforms == null) {
                return null;
            }

            for (int i = 0; i < platforms.Count; i++) {
                EditorPlatformProfileSettingsDocument platform = platforms[i];
                if (platform != null && string.Equals(platform.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)) {
                    return platform;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates one default profile record for a newly supported platform.
        /// </summary>
        /// <param name="platformId">Platform identifier the new profile record belongs to.</param>
        /// <returns>New platform profile document seeded with defaults.</returns>
        EditorPlatformProfileSettingsDocument CreatePlatformDocument(string platformId) {
            EditorPlatformProfileSettingsDocument document = new EditorPlatformProfileSettingsDocument {
                PlatformId = platformId,
                Build = new EditorBuildProfileSettingsDocument(),
                Graphics = new EditorGraphicsProfileSettingsDocument(),
                Codegen = new EditorCodegenProfileSettingsDocument()
            };

            NormalizePlatform(document, platformId);
            return document;
        }

        /// <summary>
        /// Normalizes one platform profile document so required nested settings objects always exist.
        /// </summary>
        /// <param name="platform">Platform profile document to normalize.</param>
        /// <param name="platformId">Canonical platform identifier for the document.</param>
        void NormalizePlatform(EditorPlatformProfileSettingsDocument platform, string platformId) {
            if (platform == null) {
                throw new ArgumentNullException(nameof(platform));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            platform.PlatformId = platformId;
            platform.Build ??= new EditorBuildProfileSettingsDocument();
            platform.Graphics ??= new EditorGraphicsProfileSettingsDocument();
            platform.Codegen ??= new EditorCodegenProfileSettingsDocument();
            platform.Build.SelectedBuildProfileId ??= string.Empty;
            platform.Build.SelectedOptionValues ??= [];
            platform.Graphics.SelectedGraphicsProfileId ??= string.Empty;
            platform.Graphics.SelectedOptionValues ??= [];
            platform.Graphics.RendererShadowQualityTier ??= "medium";
            platform.Codegen.SelectedCodegenProfileId ??= string.Empty;
            platform.Codegen.SelectedOptionValues ??= [];
        }
    }
}

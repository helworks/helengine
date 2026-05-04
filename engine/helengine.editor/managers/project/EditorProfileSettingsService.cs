using System.Text.Json;
using System.Text.Json.Serialization;

namespace helengine.editor {
    /// <summary>
    /// Loads and persists editor-local platform profile settings stored in `user_settings/profile_config.json`.
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
        /// Gets the absolute path to `user_settings/profile_config.json`.
        /// </summary>
        string ProfileConfigFilePath {
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
        /// Loads the profile settings document, regenerating missing or invalid data and seeding newly supported platforms.
        /// </summary>
        /// <param name="supportedPlatforms">Supported platform identifiers declared by the current project.</param>
        /// <returns>Validated platform profile settings document for the current project.</returns>
        public EditorProfileSettingsDocument Load(IReadOnlyList<string> supportedPlatforms) {
            if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            }

            EditorProfileSettingsDocument document = TryLoadDocument();
            if (document == null) {
                document = new EditorProfileSettingsDocument();
            }

            bool changed = NormalizePlatforms(document, supportedPlatforms);
            if (!File.Exists(ProfileConfigFilePath) || changed) {
                Save(document);
            }

            return document;
        }

        /// <summary>
        /// Attempts to load an existing profile settings document without seeding new platform entries.
        /// </summary>
        /// <returns>Loaded profile settings document, or null when the file is missing or malformed.</returns>
        public EditorProfileSettingsDocument TryLoadExisting() {
            return TryLoadDocument();
        }

        /// <summary>
        /// Persists the supplied profile settings document to `user_settings/profile_config.json`.
        /// </summary>
        /// <param name="document">Validated platform profile settings to persist.</param>
        public void Save(EditorProfileSettingsDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            string settingsDirectoryPath = Path.GetDirectoryName(ProfileConfigFilePath);
            Directory.CreateDirectory(settingsDirectoryPath);

            string json = JsonSerializer.Serialize(document, JsonSerializerOptions);
            File.WriteAllText(ProfileConfigFilePath, json);
        }

        /// <summary>
        /// Attempts to load the profile settings document from disk.
        /// </summary>
        /// <returns>Loaded profile settings document, or null when the file is missing or malformed.</returns>
        EditorProfileSettingsDocument TryLoadDocument() {
            if (!File.Exists(ProfileConfigFilePath)) {
                return null;
            }

            try {
                string json = File.ReadAllText(ProfileConfigFilePath);
                EditorProfileSettingsDocument document = JsonSerializer.Deserialize<EditorProfileSettingsDocument>(json, JsonSerializerOptions);
                if (document == null) {
                    return null;
                }

                document.Platforms ??= [];
                for (int index = 0; index < document.Platforms.Count; index++) {
                    EditorPlatformProfileSettingsDocument platform = document.Platforms[index];
                    if (platform == null) {
                        document.Platforms[index] = new EditorPlatformProfileSettingsDocument();
                        continue;
                    }

                    platform.Build ??= new EditorBuildProfileSettingsDocument();
                    platform.Graphics ??= new EditorGraphicsProfileSettingsDocument();
                    platform.Codegen ??= new EditorCodegenProfileSettingsDocument();
                    platform.Build.SelectedBuildProfileId ??= string.Empty;
                    platform.Graphics.SelectedGraphicsProfileId ??= string.Empty;
                    platform.Graphics.RendererShadowQualityTier ??= "medium";
                    platform.Build.SelectedOptionValues ??= [];
                    platform.Graphics.SelectedOptionValues ??= [];
                    platform.Codegen.SelectedCodegenProfileId ??= string.Empty;
                    platform.Codegen.SelectedOptionValues ??= [];
                }

                return document;
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Ensures the supplied document contains one normalized profile entry for each supported platform.
        /// </summary>
        /// <param name="document">Profile settings document to normalize.</param>
        /// <param name="supportedPlatforms">Supported platform identifiers declared by the current project.</param>
        /// <returns>True when the document changed; otherwise false.</returns>
        bool NormalizePlatforms(EditorProfileSettingsDocument document, IReadOnlyList<string> supportedPlatforms) {
            bool changed = false;
            List<EditorPlatformProfileSettingsDocument> normalizedPlatforms = new List<EditorPlatformProfileSettingsDocument>(supportedPlatforms.Count);

            if (document.Platforms == null) {
                document.Platforms = [];
                changed = true;
            }

            for (int i = 0; i < supportedPlatforms.Count; i++) {
                string platformId = supportedPlatforms[i];
                EditorPlatformProfileSettingsDocument platform = FindPlatform(document.Platforms, platformId);
                if (platform == null) {
                    normalizedPlatforms.Add(CreatePlatformDocument(platformId));
                    changed = true;
                    continue;
                }

                if (!string.Equals(platform.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)) {
                    platform.PlatformId = platformId;
                    changed = true;
                }

                if (platform.Build == null) {
                    platform.Build = new EditorBuildProfileSettingsDocument();
                    changed = true;
                }
                if (string.IsNullOrWhiteSpace(platform.Build.SelectedBuildProfileId)) {
                    platform.Build.SelectedBuildProfileId = string.Empty;
                    changed = true;
                }
                if (platform.Build.SelectedOptionValues == null) {
                    platform.Build.SelectedOptionValues = [];
                    changed = true;
                }

                if (platform.Graphics == null) {
                    platform.Graphics = new EditorGraphicsProfileSettingsDocument();
                    changed = true;
                }
                if (platform.Codegen == null) {
                    platform.Codegen = new EditorCodegenProfileSettingsDocument();
                    changed = true;
                }
                if (string.IsNullOrWhiteSpace(platform.Graphics.SelectedGraphicsProfileId)) {
                    platform.Graphics.SelectedGraphicsProfileId = string.Empty;
                    changed = true;
                }
                if (platform.Graphics.SelectedOptionValues == null) {
                    platform.Graphics.SelectedOptionValues = [];
                    changed = true;
                }
                if (string.IsNullOrWhiteSpace(platform.Graphics.RendererShadowQualityTier)) {
                    platform.Graphics.RendererShadowQualityTier = "medium";
                    changed = true;
                }
                if (string.IsNullOrWhiteSpace(platform.Codegen.SelectedCodegenProfileId)) {
                    platform.Codegen.SelectedCodegenProfileId = string.Empty;
                    changed = true;
                }
                if (platform.Codegen.SelectedOptionValues == null) {
                    platform.Codegen.SelectedOptionValues = [];
                    changed = true;
                }

                normalizedPlatforms.Add(platform);
            }

            if (document.Platforms.Count != normalizedPlatforms.Count) {
                changed = true;
            } else {
                for (int i = 0; i < document.Platforms.Count; i++) {
                    if (!ReferenceEquals(document.Platforms[i], normalizedPlatforms[i])) {
                        changed = true;
                        break;
                    }
                }
            }

            document.Platforms = normalizedPlatforms;
            return changed;
        }

        /// <summary>
        /// Finds the platform profile record for one platform id.
        /// </summary>
        /// <param name="platforms">Existing profile records.</param>
        /// <param name="platformId">Platform identifier to locate.</param>
        /// <returns>Matching platform profile record when present; otherwise null.</returns>
        EditorPlatformProfileSettingsDocument FindPlatform(IReadOnlyList<EditorPlatformProfileSettingsDocument> platforms, string platformId) {
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
            return new EditorPlatformProfileSettingsDocument {
                PlatformId = platformId,
                Build = new EditorBuildProfileSettingsDocument(),
                Graphics = new EditorGraphicsProfileSettingsDocument(),
                Codegen = new EditorCodegenProfileSettingsDocument()
            };
        }
    }
}

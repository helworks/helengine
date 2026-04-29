using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Loads and persists editor-local project settings stored in `settings/project.json`.
    /// </summary>
    public sealed class EditorProjectLocalSettingsService {
        /// <summary>
        /// Gets the JSON formatting rules used for local project settings.
        /// </summary>
        static JsonSerializerOptions JsonSerializerOptions { get; } = new() {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Gets the absolute path to the current project root.
        /// </summary>
        string ProjectRootPath { get; }

        /// <summary>
        /// Gets the supported platform identifiers allowed for the current project.
        /// </summary>
        IReadOnlyList<string> SupportedPlatforms { get; }

        /// <summary>
        /// Initializes one local-settings service for the supplied project root and supported platforms.
        /// </summary>
        /// <param name="projectRootPath">Absolute path to the current project root directory.</param>
        /// <param name="supportedPlatforms">Supported platform identifiers declared by the project's `.heproj` file.</param>
        public EditorProjectLocalSettingsService(string projectRootPath, IReadOnlyList<string> supportedPlatforms) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            }
            if (supportedPlatforms.Count == 0) {
                throw new InvalidOperationException("At least one supported platform must be provided.");
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            SupportedPlatforms = supportedPlatforms;
        }

        /// <summary>
        /// Loads the active project platform, regenerating local settings when the stored value is missing or invalid.
        /// </summary>
        /// <returns>Validated active platform identifier for the current project.</returns>
        public string LoadActivePlatform() {
            EditorProjectLocalSettingsDocument document = TryLoadDocument();
            if (document != null && IsSupportedPlatform(document.ActivePlatform)) {
                return document.ActivePlatform;
            }

            string defaultPlatform = ResolveDefaultPlatform();
            SaveActivePlatform(defaultPlatform);
            return defaultPlatform;
        }

        /// <summary>
        /// Persists one validated active project platform to `settings/project.json`.
        /// </summary>
        /// <param name="activePlatform">Supported platform identifier to persist.</param>
        public void SaveActivePlatform(string activePlatform) {
            if (!IsSupportedPlatform(activePlatform)) {
                throw new InvalidOperationException($"Platform '{activePlatform}' is not supported by the current project.");
            }

            string settingsDirectoryPath = Path.Combine(ProjectRootPath, "settings");
            Directory.CreateDirectory(settingsDirectoryPath);

            EditorProjectLocalSettingsDocument document = new EditorProjectLocalSettingsDocument {
                ActivePlatform = activePlatform
            };

            string json = JsonSerializer.Serialize(document, JsonSerializerOptions);
            File.WriteAllText(Path.Combine(settingsDirectoryPath, "project.json"), json);
        }

        /// <summary>
        /// Attempts to load the current local settings document from disk.
        /// </summary>
        /// <returns>Loaded local settings document, or null when the file is missing or malformed.</returns>
        EditorProjectLocalSettingsDocument TryLoadDocument() {
            string settingsFilePath = Path.Combine(ProjectRootPath, "settings", "project.json");
            if (!File.Exists(settingsFilePath)) {
                return null;
            }

            try {
                string json = File.ReadAllText(settingsFilePath);
                EditorProjectLocalSettingsDocument document = JsonSerializer.Deserialize<EditorProjectLocalSettingsDocument>(json, JsonSerializerOptions);
                if (document == null) {
                    return null;
                }

                return document;
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Resolves the default active platform that should be used when local settings are missing or invalid.
        /// </summary>
        /// <returns>First supported platform declared by the current project.</returns>
        string ResolveDefaultPlatform() {
            return SupportedPlatforms[0];
        }

        /// <summary>
        /// Returns true when the supplied platform identifier is supported by the current project.
        /// </summary>
        /// <param name="platformId">Platform identifier to validate.</param>
        /// <returns>True when the platform is supported by the current project; otherwise false.</returns>
        bool IsSupportedPlatform(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                return false;
            }

            for (int i = 0; i < SupportedPlatforms.Count; i++) {
                if (string.Equals(SupportedPlatforms[i], platformId, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }
    }
}

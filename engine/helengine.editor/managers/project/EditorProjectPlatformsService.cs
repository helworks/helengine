using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Loads and persists project-shared supported platforms stored in `settings/platforms.json`.
    /// </summary>
    public sealed class EditorProjectPlatformsService {
        /// <summary>
        /// Gets the JSON formatting rules used for the project platform settings document.
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
        /// Gets the absolute path to `settings/platforms.json`.
        /// </summary>
        string PlatformsFilePath {
            get {
                return Path.Combine(ProjectRootPath, "settings", "platforms.json");
            }
        }

        /// <summary>
        /// Initializes one project-platform settings service for the supplied project root directory.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative path to the current project root directory.</param>
        public EditorProjectPlatformsService(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        /// <summary>
        /// Loads the project platform settings document, creating a default empty document when the file is missing or invalid.
        /// </summary>
        /// <returns>Validated project platform settings document for the current project.</returns>
        public EditorProjectPlatformsDocument Load() {
            EditorProjectPlatformsDocument document = TryLoadDocument();
            if (document == null) {
                document = CreateDefaultDocument();
                Save(document);
                return document;
            }

            Normalize(document);
            return document;
        }

        /// <summary>
        /// Persists the supplied project platform settings document to `settings/platforms.json`.
        /// </summary>
        /// <param name="document">Validated platform settings document to persist.</param>
        public void Save(EditorProjectPlatformsDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            Normalize(document);
            string settingsDirectoryPath = Path.GetDirectoryName(PlatformsFilePath);
            Directory.CreateDirectory(settingsDirectoryPath);

            string json = JsonSerializer.Serialize(document, JsonSerializerOptions);
            File.WriteAllText(PlatformsFilePath, json);
        }

        /// <summary>
        /// Attempts to load the project platform settings document from disk.
        /// </summary>
        /// <returns>Loaded platform settings document, or null when the file is missing or malformed.</returns>
        EditorProjectPlatformsDocument TryLoadDocument() {
            if (!File.Exists(PlatformsFilePath)) {
                return null;
            }

            try {
                string json = File.ReadAllText(PlatformsFilePath);
                EditorProjectPlatformsDocument document = JsonSerializer.Deserialize<EditorProjectPlatformsDocument>(json, JsonSerializerOptions);
                if (document == null) {
                    return null;
                }

                return document;
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Creates one default project platform settings document for a newly initialized project.
        /// </summary>
        /// <returns>Default project platform settings document with no preferred platform.</returns>
        EditorProjectPlatformsDocument CreateDefaultDocument() {
            return new EditorProjectPlatformsDocument {
                SupportedPlatforms = []
            };
        }

        /// <summary>
        /// Normalizes supported platform identifiers by trimming blanks and removing duplicates while preserving order.
        /// </summary>
        /// <param name="document">Project platform settings document to normalize.</param>
        void Normalize(EditorProjectPlatformsDocument document) {
            List<string> normalizedPlatforms = [];
            if (document.SupportedPlatforms == null) {
                document.SupportedPlatforms = normalizedPlatforms;
                return;
            }

            for (int index = 0; index < document.SupportedPlatforms.Count; index++) {
                string platformId = document.SupportedPlatforms[index];
                if (string.IsNullOrWhiteSpace(platformId)) {
                    continue;
                }

                string normalizedPlatformId = platformId.Trim();
                if (ContainsPlatform(normalizedPlatforms, normalizedPlatformId)) {
                    continue;
                }

                normalizedPlatforms.Add(normalizedPlatformId);
            }

            document.SupportedPlatforms = normalizedPlatforms;
        }

        /// <summary>
        /// Returns true when the supplied list already contains the requested platform identifier.
        /// </summary>
        /// <param name="platformIds">Platform identifiers to search.</param>
        /// <param name="platformId">Platform identifier to locate.</param>
        /// <returns>True when the identifier is already present; otherwise false.</returns>
        bool ContainsPlatform(IReadOnlyList<string> platformIds, string platformId) {
            for (int index = 0; index < platformIds.Count; index++) {
                if (string.Equals(platformIds[index], platformId, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }
    }
}

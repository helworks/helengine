using System.Text.Json;
using System.Text.Json.Serialization;

namespace helengine.editor {
    /// <summary>
    /// Loads and persists editor-global UI scale preferences stored in one JSON document.
    /// </summary>
    public sealed class EditorPreferencesService {
        /// <summary>
        /// Gets the JSON formatting rules used for the editor preferences document.
        /// </summary>
        static JsonSerializerOptions JsonSerializerOptions { get; } = CreateJsonSerializerOptions();

        /// <summary>
        /// Gets the absolute preferences root directory that contains the preferences JSON file.
        /// </summary>
        string PreferencesRootPath { get; }

        /// <summary>
        /// Gets the absolute path to the editor-global preferences JSON file.
        /// </summary>
        string PreferencesFilePath {
            get {
                return Path.Combine(PreferencesRootPath, "preferences.json");
            }
        }

        /// <summary>
        /// Initializes one editor-global preferences service for the supplied root directory.
        /// </summary>
        /// <param name="preferencesRootPath">Absolute or relative path to the directory that stores editor-global preferences.</param>
        public EditorPreferencesService(string preferencesRootPath) {
            if (string.IsNullOrWhiteSpace(preferencesRootPath)) {
                throw new ArgumentException("Preferences root path must be provided.", nameof(preferencesRootPath));
            }

            PreferencesRootPath = Path.GetFullPath(preferencesRootPath);
        }

        /// <summary>
        /// Loads one validated editor UI scale selection, regenerating the preferences document when it is missing or invalid.
        /// </summary>
        /// <returns>Validated editor UI scale settings for the current user.</returns>
        public EditorUiScaleSettings Load() {
            EditorPreferencesDocument document = TryLoadDocument();
            if (document != null) {
                try {
                    EditorUiScaleSettings settings = new EditorUiScaleSettings(document.UiScaleMode, document.UiScalePercent);
                    Save(settings);
                    return settings;
                } catch {
                }
            }

            EditorUiScaleSettings defaultSettings = CreateDefaultSettings();
            Save(defaultSettings);
            return defaultSettings;
        }

        /// <summary>
        /// Persists the supplied editor UI scale selection to the preferences document.
        /// </summary>
        /// <param name="settings">Validated editor UI scale settings to persist.</param>
        public void Save(EditorUiScaleSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            string preferencesDirectoryPath = Path.GetDirectoryName(PreferencesFilePath);
            Directory.CreateDirectory(preferencesDirectoryPath);

            EditorPreferencesDocument document = new EditorPreferencesDocument {
                UiScaleMode = settings.Mode,
                UiScalePercent = settings.OverridePercent
            };

            string json = JsonSerializer.Serialize(document, JsonSerializerOptions);
            File.WriteAllText(PreferencesFilePath, json);
        }

        /// <summary>
        /// Attempts to load the editor preferences document from disk.
        /// </summary>
        /// <returns>Loaded editor preferences document, or null when the file is missing or malformed.</returns>
        EditorPreferencesDocument TryLoadDocument() {
            if (!File.Exists(PreferencesFilePath)) {
                return null;
            }

            try {
                string json = File.ReadAllText(PreferencesFilePath);
                EditorPreferencesDocument document = JsonSerializer.Deserialize<EditorPreferencesDocument>(json, JsonSerializerOptions);
                if (document == null) {
                    return null;
                }

                return document;
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Creates the default editor UI scale settings used when no valid preference document exists.
        /// </summary>
        /// <returns>Default auto-mode editor UI scale settings.</returns>
        static EditorUiScaleSettings CreateDefaultSettings() {
            return new EditorUiScaleSettings(EditorUiScaleMode.Auto, 100);
        }

        /// <summary>
        /// Creates the JSON serializer options used by the editor preferences document.
        /// </summary>
        /// <returns>Serializer options configured for indented camel-case JSON with enum-string support.</returns>
        static JsonSerializerOptions CreateJsonSerializerOptions() {
            JsonSerializerOptions options = new JsonSerializerOptions {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
    }
}

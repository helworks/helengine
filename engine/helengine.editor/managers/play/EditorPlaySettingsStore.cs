using System.Text.Json;
using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Loads and saves the per-user Play settings in <c>user_settings/play.json</c> (git-ignored). The format is version 1
    /// only: <c>{ "version": 1, "common": {...}, "platforms": { "&lt;id&gt;": {...} } }</c>. A missing file is an empty
    /// document and is never created by loading; unknown versions, properties and common ids fail naming the file.
    /// </summary>
    public sealed class EditorPlaySettingsStore {
        /// <summary>The only format version this editor reads and writes.</summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// Initializes the store for one project.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root.</param>
        /// <exception cref="ArgumentException">Thrown when the project root is blank.</exception>
        public EditorPlaySettingsStore(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            FilePath = Path.Combine(Path.GetFullPath(projectRootPath), "user_settings", "play.json");
        }

        /// <summary>Gets the absolute path of <c>user_settings/play.json</c>.</summary>
        public string FilePath { get; }

        /// <summary>
        /// Loads the stored Play settings.
        /// </summary>
        /// <returns>The stored document, or an empty one when the file does not exist.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the file is malformed or not in the current format.</exception>
        public EditorPlaySettingsDocument Load() {
            EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
            if (!File.Exists(FilePath)) {
                return document;
            }

            JsonDocument json;
            try {
                json = JsonDocument.Parse(File.ReadAllText(FilePath));
            } catch (JsonException exception) {
                throw new InvalidOperationException($"Play settings file '{FilePath}' is not valid JSON: {exception.Message}", exception);
            }

            using (json) {
                JsonElement root = json.RootElement;
                if (root.ValueKind != JsonValueKind.Object) {
                    throw new InvalidOperationException($"Play settings file '{FilePath}' must hold one JSON object.");
                }

                bool sawVersion = false;
                foreach (JsonProperty property in root.EnumerateObject()) {
                    if (string.Equals(property.Name, "version", StringComparison.Ordinal)) {
                        sawVersion = true;
                        if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out int version) || version != CurrentVersion) {
                            throw new InvalidOperationException($"Play settings file '{FilePath}' has version {property.Value.GetRawText()}; this editor reads version {CurrentVersion} only.");
                        }
                    } else if (string.Equals(property.Name, "common", StringComparison.Ordinal)) {
                        ReadSection(property.Value, "common", document.CommonValues);
                        foreach (string settingId in document.CommonValues.Keys) {
                            if (!PlayerCommonPlaySettings.IsCommonId(settingId)) {
                                throw new InvalidOperationException($"Play settings file '{FilePath}' names unknown common setting '{settingId}'.");
                            }
                        }
                    } else if (string.Equals(property.Name, "platforms", StringComparison.Ordinal)) {
                        if (property.Value.ValueKind != JsonValueKind.Object) {
                            throw new InvalidOperationException($"Play settings file '{FilePath}' property 'platforms' must be an object.");
                        }
                        foreach (JsonProperty platform in property.Value.EnumerateObject()) {
                            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
                            ReadSection(platform.Value, "platform '" + platform.Name + "'", values);
                            document.PlatformValues.Add(platform.Name, values);
                        }
                    } else {
                        throw new InvalidOperationException($"Play settings file '{FilePath}' has unknown property '{property.Name}'.");
                    }
                }

                if (!sawVersion) {
                    throw new InvalidOperationException($"Play settings file '{FilePath}' has no version.");
                }
            }

            return document;
        }

        /// <summary>
        /// Saves the document, replacing the file. Boolean text is written as JSON booleans.
        /// </summary>
        /// <param name="document">Document to save.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public void Save(EditorPlaySettingsDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            using FileStream stream = File.Create(FilePath);
            using Utf8JsonWriter writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            writer.WriteStartObject();
            writer.WriteNumber("version", CurrentVersion);
            writer.WriteStartObject("common");
            WriteValues(writer, document.CommonValues);
            writer.WriteEndObject();
            writer.WriteStartObject("platforms");
            foreach (KeyValuePair<string, Dictionary<string, string>> section in document.PlatformValues) {
                writer.WriteStartObject(section.Key);
                WriteValues(writer, section.Value);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        /// <summary>
        /// Reads one object of setting values; strings are kept and JSON booleans become <c>true</c>/<c>false</c>.
        /// </summary>
        /// <param name="section">JSON object to read.</param>
        /// <param name="sectionName">Section name reported on failure.</param>
        /// <param name="values">Receives the values.</param>
        /// <exception cref="InvalidOperationException">Thrown when the section is not an object or a value is another kind.</exception>
        void ReadSection(JsonElement section, string sectionName, Dictionary<string, string> values) {
            if (section.ValueKind != JsonValueKind.Object) {
                throw new InvalidOperationException($"Play settings file '{FilePath}' section {sectionName} must be an object.");
            }

            foreach (JsonProperty entry in section.EnumerateObject()) {
                if (entry.Value.ValueKind == JsonValueKind.String) {
                    values.Add(entry.Name, entry.Value.GetString());
                } else if (entry.Value.ValueKind == JsonValueKind.True) {
                    values.Add(entry.Name, "true");
                } else if (entry.Value.ValueKind == JsonValueKind.False) {
                    values.Add(entry.Name, "false");
                } else {
                    throw new InvalidOperationException($"Play settings file '{FilePath}' value '{entry.Name}' in {sectionName} must be a string or a boolean.");
                }
            }
        }

        /// <summary>
        /// Writes one section's values, turning the text <c>true</c>/<c>false</c> into JSON booleans.
        /// </summary>
        /// <param name="writer">JSON writer positioned inside the section object.</param>
        /// <param name="values">Values to write.</param>
        static void WriteValues(Utf8JsonWriter writer, Dictionary<string, string> values) {
            foreach (KeyValuePair<string, string> entry in values) {
                if (string.Equals(entry.Value, "true", StringComparison.Ordinal)) {
                    writer.WriteBoolean(entry.Key, true);
                } else if (string.Equals(entry.Value, "false", StringComparison.Ordinal)) {
                    writer.WriteBoolean(entry.Key, false);
                } else {
                    writer.WriteString(entry.Key, entry.Value);
                }
            }
        }
    }
}

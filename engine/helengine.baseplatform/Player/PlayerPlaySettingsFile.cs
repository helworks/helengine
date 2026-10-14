using System.Globalization;
using System.Text.Json;

namespace helengine.baseplatform.Player {
    /// <summary>
    /// Reads and writes the <c>--play-settings</c> file. The format is the current one only: version 1, a fixed set of
    /// properties, every property required. Anything else fails and names the file.
    /// </summary>
    public static class PlayerPlaySettingsFile {
        /// <summary>The only format version this build reads and writes.</summary>
        public const int CurrentVersion = 1;

        /// <summary>Property names, in write order.</summary>
        static readonly string[] PropertyNames = [
            "version", "windowScale", "windowMonitor", "editorProcessId", "waitForDebugger",
            "shadowsEnabled", "frameRateUnlimited", "targetFrameRate", "platform"
        ];

        /// <summary>
        /// Writes one resolved settings file, replacing any previous one.
        /// </summary>
        /// <param name="path">Absolute file path.</param>
        /// <param name="settings">Resolved settings.</param>
        /// <exception cref="ArgumentException">Thrown when the path is blank.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the settings are null.</exception>
        public static void Write(string path, PlayerPlaySettings settings) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("The Play settings file path is required.", nameof(path));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using FileStream stream = File.Create(path);
            using Utf8JsonWriter writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            writer.WriteStartObject();
            writer.WriteNumber("version", CurrentVersion);
            writer.WriteString("windowScale", PlayerWindowScaleText.Format(settings.WindowScale));
            writer.WriteString("windowMonitor", PlayerWindowMonitorText.Format(settings.WindowMonitor));
            writer.WriteNumber("editorProcessId", settings.EditorProcessId);
            writer.WriteBoolean("waitForDebugger", settings.WaitForDebugger);
            writer.WriteBoolean("shadowsEnabled", settings.ShadowsEnabled);
            writer.WriteBoolean("frameRateUnlimited", settings.FrameRateUnlimited);
            writer.WriteNumber("targetFrameRate", settings.TargetFrameRate);
            writer.WriteStartObject("platform");
            foreach (KeyValuePair<string, string> entry in settings.PlatformValues) {
                writer.WriteString(entry.Key, entry.Value);
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        /// <summary>
        /// Reads one settings file written by <see cref="Write"/>.
        /// </summary>
        /// <param name="path">Absolute file path.</param>
        /// <returns>The resolved settings.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
        /// <exception cref="InvalidOperationException">Thrown for another version, an unknown or missing property, or a wrongly typed value.</exception>
        public static PlayerPlaySettings Read(string path) {
            if (!File.Exists(path)) {
                throw new FileNotFoundException("The Play settings file does not exist.", path);
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) {
                throw new InvalidOperationException($"Play settings file '{path}' must hold one JSON object.");
            }

            foreach (JsonProperty property in root.EnumerateObject()) {
                if (Array.IndexOf(PropertyNames, property.Name) < 0) {
                    throw new InvalidOperationException($"Play settings file '{path}' has unknown property '{property.Name}'.");
                }
            }

            int version = RequireProperty(root, "version", JsonValueKind.Number, path).GetInt32();
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Play settings file '{path}' has version {version}; this player reads version {CurrentVersion} only.");
            }

            Dictionary<string, string> platformValues = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonProperty entry in RequireProperty(root, "platform", JsonValueKind.Object, path).EnumerateObject()) {
                if (entry.Value.ValueKind != JsonValueKind.String) {
                    throw new InvalidOperationException($"Play settings file '{path}' platform value '{entry.Name}' must be a string.");
                }
                platformValues.Add(entry.Name, entry.Value.GetString());
            }

            return new PlayerPlaySettings(
                PlayerWindowScaleText.Parse(RequireProperty(root, "windowScale", JsonValueKind.String, path).GetString()),
                PlayerWindowMonitorText.Parse(RequireProperty(root, "windowMonitor", JsonValueKind.String, path).GetString()),
                RequireProperty(root, "editorProcessId", JsonValueKind.Number, path).GetInt32(),
                RequireBoolean(root, "waitForDebugger", path),
                RequireBoolean(root, "shadowsEnabled", path),
                RequireBoolean(root, "frameRateUnlimited", path),
                RequireProperty(root, "targetFrameRate", JsonValueKind.Number, path).GetDouble(),
                platformValues);
        }

        /// <summary>
        /// Returns one required property of the expected JSON kind.
        /// </summary>
        /// <param name="root">Root object.</param>
        /// <param name="name">Property name.</param>
        /// <param name="kind">Expected value kind.</param>
        /// <param name="path">File path reported on failure.</param>
        /// <returns>The property value.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the property is missing or of another kind.</exception>
        static JsonElement RequireProperty(JsonElement root, string name, JsonValueKind kind, string path) {
            if (!root.TryGetProperty(name, out JsonElement value)) {
                throw new InvalidOperationException($"Play settings file '{path}' is missing property '{name}'.");
            } else if (value.ValueKind != kind) {
                throw new InvalidOperationException($"Play settings file '{path}' property '{name}' must be a JSON {kind.ToString().ToLower(CultureInfo.InvariantCulture)}.");
            }

            return value;
        }

        /// <summary>
        /// Returns one required boolean property.
        /// </summary>
        /// <param name="root">Root object.</param>
        /// <param name="name">Property name.</param>
        /// <param name="path">File path reported on failure.</param>
        /// <returns>The boolean value.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the property is missing or not a boolean.</exception>
        static bool RequireBoolean(JsonElement root, string name, string path) {
            if (!root.TryGetProperty(name, out JsonElement value)) {
                throw new InvalidOperationException($"Play settings file '{path}' is missing property '{name}'.");
            } else if (value.ValueKind == JsonValueKind.True) {
                return true;
            } else if (value.ValueKind == JsonValueKind.False) {
                return false;
            }

            throw new InvalidOperationException($"Play settings file '{path}' property '{name}' must be true or false.");
        }
    }
}

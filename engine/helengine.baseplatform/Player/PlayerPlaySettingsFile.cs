using System.Globalization;
using System.Text.Json;
using helengine.baseplatform.Definitions;

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
            "shadowsEnabled", "frameRateUnlimited", "targetFrameRate", "platform", "videoMode"
        ];

        /// <summary>Field names of the <c>videoMode</c> object, in write order; any other field is rejected.</summary>
        static readonly string[] VideoModePropertyNames = [
            "id", "width", "height", "progressive", "refreshRateHz", "playerFrameRate", "displayAspectWidth", "displayAspectHeight"
        ];

        /// <summary>
        /// Writes one resolved settings file, replacing any previous one.
        /// </summary>
        /// <param name="path">Absolute file path.</param>
        /// <param name="settings">Resolved settings.</param>
        /// <exception cref="ArgumentException">Thrown when the path is blank or not fully qualified.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the settings are null.</exception>
        public static void Write(string path, PlayerPlaySettings settings) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("The Play settings file path is required.", nameof(path));
            } else if (!Path.IsPathFullyQualified(path)) {
                throw new ArgumentException($"The Play settings file path '{path}' must be absolute.", nameof(path));
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
            if (settings.HasVideoMode) {
                writer.WriteStartObject("videoMode");
                writer.WriteString("id", settings.VideoMode.Id);
                writer.WriteNumber("width", settings.VideoMode.Width);
                writer.WriteNumber("height", settings.VideoMode.Height);
                writer.WriteBoolean("progressive", settings.VideoMode.IsProgressive);
                writer.WriteNumber("refreshRateHz", settings.VideoMode.RefreshRateHz);
                writer.WriteNumber("playerFrameRate", settings.VideoMode.PlayerFrameRate);
                writer.WriteNumber("displayAspectWidth", settings.VideoMode.DisplayAspectWidth);
                writer.WriteNumber("displayAspectHeight", settings.VideoMode.DisplayAspectHeight);
                writer.WriteEndObject();
            } else {
                writer.WriteNull("videoMode");
            }
            writer.WriteEndObject();
        }

        /// <summary>
        /// Reads one settings file written by <see cref="Write"/>.
        /// </summary>
        /// <param name="path">Absolute file path.</param>
        /// <returns>The resolved settings.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown for malformed JSON, another version, an unknown or missing property, a wrongly typed value, or a value the
        /// settings do not allow; every message names the file and the property.
        /// </exception>
        public static PlayerPlaySettings Read(string path) {
            if (!File.Exists(path)) {
                throw new FileNotFoundException("The Play settings file does not exist.", path);
            }

            JsonDocument document;
            try {
                document = JsonDocument.Parse(File.ReadAllText(path));
            } catch (JsonException exception) {
                throw new InvalidOperationException($"Play settings file '{path}' contains malformed JSON.", exception);
            }

            using (document) {
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object) {
                    throw new InvalidOperationException($"Play settings file '{path}' must hold one JSON object.");
                }

                int version = RequireInt32(root, "version", path);
                if (version != CurrentVersion) {
                    throw new InvalidOperationException($"Play settings file '{path}' has version {version}; this player reads version {CurrentVersion} only.");
                }

                foreach (JsonProperty property in root.EnumerateObject()) {
                    if (Array.IndexOf(PropertyNames, property.Name) < 0) {
                        throw new InvalidOperationException($"Play settings file '{path}' has unknown property '{property.Name}'.");
                    }
                }

                int editorProcessId = RequireInt32(root, "editorProcessId", path);
                if (editorProcessId <= 0) {
                    throw new InvalidOperationException($"Play settings file '{path}' property 'editorProcessId' is {editorProcessId}; it must be positive.");
                }

                bool frameRateUnlimited = RequireBoolean(root, "frameRateUnlimited", path);
                double targetFrameRate = RequireFrameRate(root, frameRateUnlimited, path);
                PlatformVideoMode videoMode = RequireVideoMode(root, path);
                if (videoMode != null && !frameRateUnlimited && targetFrameRate != videoMode.PlayerFrameRate) {
                    throw new InvalidOperationException($"Play settings file '{path}' property 'targetFrameRate' is {targetFrameRate.ToString(CultureInfo.InvariantCulture)}, but video mode '{videoMode.Id}' plays at {videoMode.PlayerFrameRate.ToString(CultureInfo.InvariantCulture)} frames per second.");
                }

                return new PlayerPlaySettings(
                    RequireWindowScale(root, path),
                    RequireWindowMonitor(root, path),
                    editorProcessId,
                    RequireBoolean(root, "waitForDebugger", path),
                    RequireBoolean(root, "shadowsEnabled", path),
                    frameRateUnlimited,
                    targetFrameRate,
                    RequirePlatformValues(root, path),
                    videoMode);
            }
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
        /// Returns one required whole-number property that fits a 32-bit integer.
        /// </summary>
        /// <param name="root">Root object.</param>
        /// <param name="name">Property name.</param>
        /// <param name="path">File path reported on failure.</param>
        /// <returns>The integer value.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the property is missing, not a number, fractional or out of range.</exception>
        static int RequireInt32(JsonElement root, string name, string path) {
            JsonElement value = RequireProperty(root, name, JsonValueKind.Number, path);
            if (!value.TryGetInt32(out int number)) {
                throw new InvalidOperationException($"Play settings file '{path}' property '{name}' is {value.GetRawText()}; it must be a whole number in the 32-bit integer range.");
            }

            return number;
        }

        /// <summary>
        /// Returns the required target frame rate, checked against the frame-rate mode: 0 exactly when unlimited, otherwise a
        /// positive finite rate.
        /// </summary>
        /// <param name="root">Root object.</param>
        /// <param name="frameRateUnlimited">Whether the file declares an unlimited frame rate.</param>
        /// <param name="path">File path reported on failure.</param>
        /// <returns>The target frame rate.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the rate is missing, not a number, or inconsistent with the mode.</exception>
        static double RequireFrameRate(JsonElement root, bool frameRateUnlimited, string path) {
            JsonElement value = RequireProperty(root, "targetFrameRate", JsonValueKind.Number, path);
            if (!value.TryGetDouble(out double rate) || !double.IsFinite(rate)) {
                throw new InvalidOperationException($"Play settings file '{path}' property 'targetFrameRate' is {value.GetRawText()}; it must be a finite number.");
            } else if (frameRateUnlimited && rate != 0d) {
                throw new InvalidOperationException($"Play settings file '{path}' property 'targetFrameRate' is {value.GetRawText()} but 'frameRateUnlimited' is true; an unlimited frame rate carries a target of 0.");
            } else if (!frameRateUnlimited && rate <= 0d) {
                throw new InvalidOperationException($"Play settings file '{path}' property 'targetFrameRate' is {value.GetRawText()}; a paced frame rate needs a positive target.");
            }

            return rate;
        }

        /// <summary>
        /// Returns the required <c>windowScale</c> property, matched against the text of every defined window scale.
        /// </summary>
        /// <param name="root">Root object.</param>
        /// <param name="path">File path reported on failure.</param>
        /// <returns>The window scale.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the property is missing, not a string, or not a window scale.</exception>
        static PlayerWindowScale RequireWindowScale(JsonElement root, string path) {
            string text = RequireProperty(root, "windowScale", JsonValueKind.String, path).GetString();
            foreach (PlayerWindowScale scale in Enum.GetValues<PlayerWindowScale>()) {
                if (string.Equals(PlayerWindowScaleText.Format(scale), text, StringComparison.Ordinal)) {
                    return scale;
                }
            }

            throw new InvalidOperationException($"Play settings file '{path}' property 'windowScale' has unknown value '{text}'; expected 1x, 2x, 3x, 4x or fit.");
        }

        /// <summary>
        /// Returns the required <c>windowMonitor</c> property, matched against the text of every defined monitor choice.
        /// </summary>
        /// <param name="root">Root object.</param>
        /// <param name="path">File path reported on failure.</param>
        /// <returns>The monitor choice.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the property is missing, not a string, or not a monitor choice.</exception>
        static PlayerWindowMonitor RequireWindowMonitor(JsonElement root, string path) {
            string text = RequireProperty(root, "windowMonitor", JsonValueKind.String, path).GetString();
            foreach (PlayerWindowMonitor monitor in Enum.GetValues<PlayerWindowMonitor>()) {
                if (string.Equals(PlayerWindowMonitorText.Format(monitor), text, StringComparison.Ordinal)) {
                    return monitor;
                }
            }

            throw new InvalidOperationException($"Play settings file '{path}' property 'windowMonitor' has unknown value '{text}'; expected editor or primary.");
        }

        /// <summary>
        /// Returns the required <c>platform</c> object as setting values keyed by id, rejecting blank, repeated or
        /// non-string entries.
        /// </summary>
        /// <param name="root">Root object.</param>
        /// <param name="path">File path reported on failure.</param>
        /// <returns>The platform values.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the object is missing or holds an invalid entry.</exception>
        static Dictionary<string, string> RequirePlatformValues(JsonElement root, string path) {
            Dictionary<string, string> platformValues = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonProperty entry in RequireProperty(root, "platform", JsonValueKind.Object, path).EnumerateObject()) {
                if (string.IsNullOrWhiteSpace(entry.Name)) {
                    throw new InvalidOperationException($"Play settings file '{path}' property 'platform' has blank id '{entry.Name}'.");
                } else if (entry.Value.ValueKind != JsonValueKind.String) {
                    throw new InvalidOperationException($"Play settings file '{path}' property 'platform' id '{entry.Name}' must be a string.");
                } else if (!platformValues.TryAdd(entry.Name, entry.Value.GetString())) {
                    throw new InvalidOperationException($"Play settings file '{path}' property 'platform' repeats id '{entry.Name}'.");
                }
            }

            return platformValues;
        }

        /// <summary>
        /// Returns the required <c>videoMode</c> property: a full video mode object, or JSON null when the platform declares
        /// no video modes.
        /// </summary>
        /// <param name="root">Root object.</param>
        /// <param name="path">File path reported on failure.</param>
        /// <returns>The video mode, or null when the file carries JSON null.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the property is missing or is neither an object nor null, or when its object has an unknown field,
        /// lacks a field, or holds a wrongly typed or out-of-range one. Every value is checked before the mode is
        /// constructed, so each message names the file and <c>videoMode.&lt;field&gt;</c>.
        /// </exception>
        static PlatformVideoMode RequireVideoMode(JsonElement root, string path) {
            if (!root.TryGetProperty("videoMode", out JsonElement videoModeElement)) {
                throw new InvalidOperationException($"Play settings file '{path}' is missing property 'videoMode'.");
            } else if (videoModeElement.ValueKind == JsonValueKind.Null) {
                return null;
            } else if (videoModeElement.ValueKind != JsonValueKind.Object) {
                throw new InvalidOperationException($"Play settings file '{path}' property 'videoMode' must be an object or null.");
            }

            foreach (JsonProperty property in videoModeElement.EnumerateObject()) {
                if (Array.IndexOf(VideoModePropertyNames, property.Name) < 0) {
                    throw new InvalidOperationException($"Play settings file '{path}' has unknown property 'videoMode.{property.Name}'.");
                }
            }

            JsonElement idElement = RequireVideoModeProperty(videoModeElement, "id", path);
            if (idElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(idElement.GetString())) {
                throw new InvalidOperationException($"Play settings file '{path}' property 'videoMode.id' is {idElement.GetRawText()}; it must be a non-blank string.");
            }

            JsonElement progressiveElement = RequireVideoModeProperty(videoModeElement, "progressive", path);
            if (progressiveElement.ValueKind != JsonValueKind.True && progressiveElement.ValueKind != JsonValueKind.False) {
                throw new InvalidOperationException($"Play settings file '{path}' property 'videoMode.progressive' is {progressiveElement.GetRawText()}; it must be true or false.");
            }

            return new PlatformVideoMode(
                idElement.GetString(),
                RequireVideoModeDimension(videoModeElement, "width", path),
                RequireVideoModeDimension(videoModeElement, "height", path),
                progressiveElement.ValueKind == JsonValueKind.True,
                RequireVideoModeRate(videoModeElement, "refreshRateHz", path),
                RequireVideoModeRate(videoModeElement, "playerFrameRate", path),
                RequireVideoModeDimension(videoModeElement, "displayAspectWidth", path),
                RequireVideoModeDimension(videoModeElement, "displayAspectHeight", path));
        }

        /// <summary>
        /// Returns one required field of the <c>videoMode</c> object, of any JSON kind.
        /// </summary>
        /// <param name="videoModeElement">The <c>videoMode</c> object.</param>
        /// <param name="field">Field name inside the object.</param>
        /// <param name="path">File path reported on failure.</param>
        /// <returns>The field value.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the field is missing, naming the file and <c>videoMode.&lt;field&gt;</c>.</exception>
        static JsonElement RequireVideoModeProperty(JsonElement videoModeElement, string field, string path) {
            if (!videoModeElement.TryGetProperty(field, out JsonElement value)) {
                throw new InvalidOperationException($"Play settings file '{path}' is missing property 'videoMode.{field}'.");
            }

            return value;
        }

        /// <summary>
        /// Returns one required whole-number field of the <c>videoMode</c> object that lies in the range a video mode accepts
        /// for its dimensions and display-aspect terms (1 to <see cref="PlatformVideoMode.MaximumDimension"/>).
        /// </summary>
        /// <param name="videoModeElement">The <c>videoMode</c> object.</param>
        /// <param name="field">Field name inside the object.</param>
        /// <param name="path">File path reported on failure.</param>
        /// <returns>The field value.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the field is missing, not a whole number or out of range.</exception>
        static int RequireVideoModeDimension(JsonElement videoModeElement, string field, string path) {
            JsonElement value = RequireVideoModeProperty(videoModeElement, field, path);
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int number) || number < 1 || number > PlatformVideoMode.MaximumDimension) {
                throw new InvalidOperationException($"Play settings file '{path}' property 'videoMode.{field}' is {value.GetRawText()}; it must be a whole number from 1 to {PlatformVideoMode.MaximumDimension}.");
            }

            return number;
        }

        /// <summary>
        /// Returns one required rate field of the <c>videoMode</c> object, which must be a positive, finite number.
        /// </summary>
        /// <param name="videoModeElement">The <c>videoMode</c> object.</param>
        /// <param name="field">Field name inside the object.</param>
        /// <param name="path">File path reported on failure.</param>
        /// <returns>The rate.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the field is missing, not a number, or not positive and finite.</exception>
        static double RequireVideoModeRate(JsonElement videoModeElement, string field, string path) {
            JsonElement value = RequireVideoModeProperty(videoModeElement, field, path);
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double rate) || !(rate > 0d && double.IsFinite(rate))) {
                throw new InvalidOperationException($"Play settings file '{path}' property 'videoMode.{field}' is {value.GetRawText()}; it must be a positive, finite number.");
            }

            return rate;
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

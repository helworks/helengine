using System.Globalization;
using System.Text.Json;

namespace helengine.projectfile {

    /// <summary>
    /// Provides the entry point for reading canonical `.heproj` project documents from disk.
    /// </summary>
    public sealed class ProjectFileReader {
        /// <summary>
        /// Reads and validates one canonical `.heproj` file from disk.
        /// </summary>
        /// <param name="projectFilePath">Absolute or relative path to the canonical project file.</param>
        /// <returns>The structured read result containing either the parsed project document or validation errors.</returns>
        public async Task<ProjectFileReadResult> ReadAsync(string projectFilePath) {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);

            FileStream stream = File.OpenRead(projectFilePath);

            try {
                using JsonDocument document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
                return ReadDocument(document.RootElement);
            } catch (JsonException exception) {
                return new ProjectFileReadResult([
                    new ProjectFileReadError(ProjectFileReadErrorCode.InvalidJson, exception.Message, string.Empty)
                ]);
            } finally {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Builds one structured read result from the supplied JSON root element.
        /// </summary>
        /// <param name="root">JSON root element representing the canonical project file.</param>
        /// <returns>Structured success or failure for the supplied project payload.</returns>
        static ProjectFileReadResult ReadDocument(JsonElement root) {
            List<ProjectFileReadError> errors = [];
            ProjectFileDocument projectDocument = new ProjectFileDocument();

            TryReadProjectFormatVersion(root, projectDocument, errors);
            TryReadString(root, "name", value => projectDocument.Name = value, errors);
            TryReadString(root, "version", value => projectDocument.Version = value, errors);
            TryReadString(root, "requiredEngineVersion", value => projectDocument.RequiredEngineVersion = value, errors);
            TryReadSupportedPlatforms(root, projectDocument, errors);
            TryReadUtcDateTime(root, "created", value => projectDocument.Created = value, errors);
            TryReadUtcDateTime(root, "lastOpened", value => projectDocument.LastOpened = value, errors);
            TryReadOptionalString(root, "description", value => projectDocument.Description = value);
            TryReadSceneRouting(root, projectDocument, errors);

            if (errors.Count > 0) {
                return new ProjectFileReadResult(errors);
            }

            return new ProjectFileReadResult(projectDocument);
        }

        /// <summary>
        /// Reads and validates the project format version from the canonical project payload.
        /// </summary>
        /// <param name="root">JSON root element representing the canonical project file.</param>
        /// <param name="projectDocument">Project document populated when validation succeeds.</param>
        /// <param name="errors">Structured error list populated when validation fails.</param>
        /// <summary>Reads optional explicit project-owned scene routing without inferring game scene names.</summary>
        static void TryReadSceneRouting(JsonElement root, ProjectFileDocument projectDocument, List<ProjectFileReadError> errors) {
            if (!TryGetProperty(root, "sceneRouting", out JsonElement routingValue) || routingValue.ValueKind == JsonValueKind.Null) {
                return;
            }
            if (routingValue.ValueKind != JsonValueKind.Object || !TryGetProperty(routingValue, "platforms", out JsonElement platformsValue) || platformsValue.ValueKind != JsonValueKind.Object) {
                errors.Add(new ProjectFileReadError(ProjectFileReadErrorCode.InvalidFieldValue, "Field 'sceneRouting.platforms' must be an object of platform routing records.", "sceneRouting.platforms"));
                return;
            }

            ProjectSceneRoutingDocument routing = new ProjectSceneRoutingDocument();
            HashSet<string> platformIds = new(StringComparer.Ordinal);
            foreach (JsonProperty platformProperty in platformsValue.EnumerateObject()) {
                if (string.IsNullOrWhiteSpace(platformProperty.Name) || !platformIds.Add(platformProperty.Name)) {
                    errors.Add(new ProjectFileReadError(ProjectFileReadErrorCode.InvalidFieldValue, "Scene routing platform identifiers must be unique and non-empty.", "sceneRouting.platforms"));
                    continue;
                }
                if (platformProperty.Value.ValueKind != JsonValueKind.Object) {
                    errors.Add(new ProjectFileReadError(ProjectFileReadErrorCode.InvalidFieldValue, $"Scene routing record for '{platformProperty.Name}' must be an object.", $"sceneRouting.platforms.{platformProperty.Name}"));
                    continue;
                }
                if (!TryGetProperty(platformProperty.Value, "bootSceneId", out JsonElement bootValue) || bootValue.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(bootValue.GetString())) {
                    errors.Add(new ProjectFileReadError(ProjectFileReadErrorCode.InvalidFieldValue, $"Scene routing record for '{platformProperty.Name}' must provide a non-empty bootSceneId.", $"sceneRouting.platforms.{platformProperty.Name}.bootSceneId"));
                    continue;
                }
                ProjectPlatformSceneRoutingDocument platformRouting = new ProjectPlatformSceneRoutingDocument { BootSceneId = bootValue.GetString() };
                if (TryGetProperty(platformProperty.Value, "sceneAliases", out JsonElement aliasesValue)) {
                    if (aliasesValue.ValueKind != JsonValueKind.Object) {
                        errors.Add(new ProjectFileReadError(ProjectFileReadErrorCode.InvalidFieldValue, $"Scene aliases for '{platformProperty.Name}' must be an object.", $"sceneRouting.platforms.{platformProperty.Name}.sceneAliases"));
                        continue;
                    }
                    HashSet<string> aliasIds = new(StringComparer.Ordinal);
                    foreach (JsonProperty aliasProperty in aliasesValue.EnumerateObject()) {
                        if (string.IsNullOrWhiteSpace(aliasProperty.Name) || !aliasIds.Add(aliasProperty.Name) || aliasProperty.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(aliasProperty.Value.GetString())) {
                            errors.Add(new ProjectFileReadError(ProjectFileReadErrorCode.InvalidFieldValue, $"Scene aliases for '{platformProperty.Name}' must contain unique non-empty string mappings.", $"sceneRouting.platforms.{platformProperty.Name}.sceneAliases"));
                            continue;
                        }
                        platformRouting.SceneAliases[aliasProperty.Name] = aliasProperty.Value.GetString();
                    }
                }
                if (HasAliasCycle(platformRouting.SceneAliases)) {
                    errors.Add(new ProjectFileReadError(ProjectFileReadErrorCode.InvalidFieldValue, $"Scene aliases for '{platformProperty.Name}' must not contain cycles.", $"sceneRouting.platforms.{platformProperty.Name}.sceneAliases"));
                    continue;
                }
                routing.Platforms[platformProperty.Name] = platformRouting;
            }
            if (errors.Count == 0 || routing.Platforms.Count > 0) {
                projectDocument.SceneRouting = routing;
            }
        }

        static bool HasAliasCycle(IReadOnlyDictionary<string, string> aliases) {
            foreach (string start in aliases.Keys) {
                HashSet<string> visited = new(StringComparer.Ordinal);
                string current = start;
                while (aliases.TryGetValue(current, out string next)) {
                    if (!visited.Add(current)) {
                        return true;
                    }
                    current = next;
                }
            }
            return false;
        }
        static void TryReadProjectFormatVersion(JsonElement root, ProjectFileDocument projectDocument, List<ProjectFileReadError> errors) {
            if (!TryGetProperty(root, "projectFormatVersion", out JsonElement propertyValue)) {
                errors.Add(new ProjectFileReadError(ProjectFileReadErrorCode.MissingRequiredField, "Missing required field 'projectFormatVersion'.", "projectFormatVersion"));
                return;
            }

            if (!propertyValue.TryGetInt32(out int projectFormatVersion)) {
                errors.Add(new ProjectFileReadError(ProjectFileReadErrorCode.InvalidFieldValue, "Field 'projectFormatVersion' must be an integer.", "projectFormatVersion"));
                return;
            }

            if (projectFormatVersion != ProjectFileDocument.SupportedProjectFormatVersion) {
                errors.Add(new ProjectFileReadError(ProjectFileReadErrorCode.UnsupportedFormatVersion, $"Project format version '{projectFormatVersion}' is not supported.", "projectFormatVersion"));
                return;
            }

            projectDocument.ProjectFormatVersion = projectFormatVersion;
        }

        /// <summary>
        /// Reads one required string field from the canonical project payload.
        /// </summary>
        /// <param name="root">JSON root element representing the canonical project file.</param>
        /// <param name="propertyName">Canonical property name to read.</param>
        /// <param name="assignValue">Assignment callback executed when the property is present and valid.</param>
        /// <param name="errors">Structured error list populated when validation fails.</param>
        static void TryReadString(JsonElement root, string propertyName, Action<string> assignValue, List<ProjectFileReadError> errors) {
            if (!TryGetProperty(root, propertyName, out JsonElement propertyValue)) {
                errors.Add(new ProjectFileReadError(ProjectFileReadErrorCode.MissingRequiredField, $"Missing required field '{propertyName}'.", propertyName));
                return;
            }

            string value = propertyValue.GetString();
            if (string.IsNullOrWhiteSpace(value)) {
                errors.Add(new ProjectFileReadError(ProjectFileReadErrorCode.InvalidFieldValue, $"Field '{propertyName}' must contain a non-empty string.", propertyName));
                return;
            }

            assignValue(value);
        }

        /// <summary>
        /// Reads one optional string field from the canonical project payload.
        /// </summary>
        /// <param name="root">JSON root element representing the canonical project file.</param>
        /// <param name="propertyName">Canonical property name to read.</param>
        /// <param name="assignValue">Assignment callback executed when the property is present and valid.</param>
        static void TryReadOptionalString(JsonElement root, string propertyName, Action<string> assignValue) {
            if (!TryGetProperty(root, propertyName, out JsonElement propertyValue)) {
                return;
            }

            string value = propertyValue.GetString();
            if (!string.IsNullOrWhiteSpace(value)) {
                assignValue(value);
            }
        }

        /// <summary>
        /// Reads the supported platform list while preserving arbitrary platform identifiers and source ordering.
        /// </summary>
        /// <param name="root">JSON root element representing the canonical project file.</param>
        /// <param name="projectDocument">Project document populated when validation succeeds.</param>
        /// <param name="errors">Structured error list populated when validation fails.</param>
        static void TryReadSupportedPlatforms(JsonElement root, ProjectFileDocument projectDocument, List<ProjectFileReadError> errors) {
            if (!TryGetProperty(root, "supportedPlatforms", out JsonElement propertyValue)) {
                errors.Add(new ProjectFileReadError(ProjectFileReadErrorCode.MissingRequiredField, "Missing required field 'supportedPlatforms'.", "supportedPlatforms"));
                return;
            }

            if (propertyValue.ValueKind != JsonValueKind.Array) {
                errors.Add(new ProjectFileReadError(ProjectFileReadErrorCode.InvalidFieldValue, "Field 'supportedPlatforms' must be an array of strings.", "supportedPlatforms"));
                return;
            }

            List<string> supportedPlatforms = [];
            foreach (JsonElement platformValue in propertyValue.EnumerateArray()) {
                string platform = platformValue.GetString();
                if (string.IsNullOrWhiteSpace(platform)) {
                    errors.Add(new ProjectFileReadError(ProjectFileReadErrorCode.InvalidFieldValue, "Field 'supportedPlatforms' must contain only non-empty strings.", "supportedPlatforms"));
                    return;
                }

                supportedPlatforms.Add(platform);
            }

            projectDocument.SupportedPlatforms = supportedPlatforms;
        }

        /// <summary>
        /// Reads one required UTC timestamp from the canonical project payload.
        /// </summary>
        /// <param name="root">JSON root element representing the canonical project file.</param>
        /// <param name="propertyName">Canonical property name to read.</param>
        /// <param name="assignValue">Assignment callback executed when the property is present and valid.</param>
        /// <param name="errors">Structured error list populated when validation fails.</param>
        static void TryReadUtcDateTime(JsonElement root, string propertyName, Action<DateTime> assignValue, List<ProjectFileReadError> errors) {
            if (!TryGetProperty(root, propertyName, out JsonElement propertyValue)) {
                errors.Add(new ProjectFileReadError(ProjectFileReadErrorCode.MissingRequiredField, $"Missing required field '{propertyName}'.", propertyName));
                return;
            }

            string value = propertyValue.GetString();
            if (string.IsNullOrWhiteSpace(value)) {
                errors.Add(new ProjectFileReadError(ProjectFileReadErrorCode.InvalidFieldValue, $"Field '{propertyName}' must contain one UTC date string.", propertyName));
                return;
            }

            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsedValue)) {
                errors.Add(new ProjectFileReadError(ProjectFileReadErrorCode.InvalidFieldValue, $"Field '{propertyName}' must contain one valid UTC date string.", propertyName));
                return;
            }

            assignValue(parsedValue.ToUniversalTime());
        }

        /// <summary>
        /// Resolves one property from the canonical payload using camelCase and PascalCase names.
        /// </summary>
        /// <param name="root">JSON root element representing the canonical project file.</param>
        /// <param name="propertyName">Canonical property name to resolve.</param>
        /// <param name="propertyValue">Resolved property value when the property exists.</param>
        /// <returns><c>true</c> when the property exists; otherwise <c>false</c>.</returns>
        static bool TryGetProperty(JsonElement root, string propertyName, out JsonElement propertyValue) {
            if (root.TryGetProperty(propertyName, out propertyValue)) {
                return true;
            }

            string pascalCasePropertyName = char.ToUpperInvariant(propertyName[0]) + propertyName[1..];
            return root.TryGetProperty(pascalCasePropertyName, out propertyValue);
        }
    }
}

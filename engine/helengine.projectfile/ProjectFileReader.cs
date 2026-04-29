using System.Globalization;
using System.Text.Json;

namespace helengine.projectfile;

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

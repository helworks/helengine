using System.Text.Json;

namespace helengine.projectfile;

/// <summary>
/// Writes canonical `.heproj` project documents in the shared JSON shape consumed by launcher and editor workflows.
/// </summary>
public sealed class ProjectFileWriter {
    /// <summary>
    /// Gets the canonical JSON serializer options used when writing project files.
    /// </summary>
    static JsonSerializerOptions SerializerOptions { get; } = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Writes one canonical project document to the supplied file path.
    /// </summary>
    /// <param name="projectFilePath">Absolute or relative path to the canonical project file.</param>
    /// <param name="document">Canonical project document to persist.</param>
    public async Task WriteAsync(string projectFilePath, ProjectFileDocument document) {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);
        ArgumentNullException.ThrowIfNull(document);

        string directoryPath = Path.GetDirectoryName(Path.GetFullPath(projectFilePath));
        if (!string.IsNullOrWhiteSpace(directoryPath)) {
            Directory.CreateDirectory(directoryPath);
        }

        ProjectFileJsonModel jsonModel = new ProjectFileJsonModel {
            ProjectFormatVersion = document.ProjectFormatVersion,
            Name = document.Name,
            Version = document.Version,
            RequiredEngineVersion = document.RequiredEngineVersion,
            SupportedPlatforms = [.. document.SupportedPlatforms],
            Created = document.Created,
            LastOpened = document.LastOpened,
            Description = document.Description
        };

        string json = JsonSerializer.Serialize(jsonModel, SerializerOptions);
        await File.WriteAllTextAsync(projectFilePath, json);
    }
}

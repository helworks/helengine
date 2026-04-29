using System;
using System.IO;
using System.Threading.Tasks;
using helengine.projectfile;
using helengine.editor.launcher.Models;

namespace helengine.editor.launcher.Services;

/// <summary>
/// Loads launcher recent-project entries from canonical `.heproj` files.
/// </summary>
public sealed class ProjectFileLoader {
    readonly ProjectFileReader ProjectFileReader;

    /// <summary>
    /// Creates the loader with the shared canonical project-file reader.
    /// </summary>
    public ProjectFileLoader() {
        ProjectFileReader = new ProjectFileReader();
    }

    /// <summary>
    /// Loads one recent-project model from the supplied `.heproj` file path.
    /// </summary>
    /// <param name="projectFilePath">Project file path selected by the launcher.</param>
    /// <returns>Recent-project entry populated from project-file metadata and fallback values.</returns>
    public async Task<RecentProject> LoadAsync(string projectFilePath) {
        if (string.IsNullOrWhiteSpace(projectFilePath)) {
            throw new InvalidOperationException("Selected file is not a helengine project.");
        }

        string fullProjectFilePath = Path.GetFullPath(projectFilePath);
        if (!string.Equals(Path.GetExtension(fullProjectFilePath), ".heproj", StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException("Selected file is not a helengine project.");
        }

        if (!File.Exists(fullProjectFilePath)) {
            throw new InvalidOperationException("Selected file is not a helengine project.");
        }

        ProjectFileReadResult readResult = await ProjectFileReader.ReadAsync(fullProjectFilePath);
        if (!readResult.Succeeded) {
            throw new InvalidOperationException(readResult.Errors[0].Message);
        }

        ProjectFileDocument document = readResult.Document;
        return new RecentProject {
            Name = document.Name,
            Path = fullProjectFilePath,
            Created = document.Created,
            LastOpened = document.LastOpened,
            TimesOpened = 1,
            Description = document.Description ?? string.Empty,
            Version = document.Version,
            RequiredEngineVersion = document.RequiredEngineVersion,
            SupportedPlatforms = document.SupportedPlatforms.ToArray()
        };
    }
}

namespace helengine.projectfile;

/// <summary>
/// Resolves editor and launcher project inputs to one validated canonical `.heproj` file path.
/// </summary>
public sealed class ProjectFilePathResolver {
    /// <summary>
    /// Canonical file name expected when one caller supplies a project directory instead of a direct project file path.
    /// </summary>
    public const string CanonicalProjectFileName = "project.heproj";

    /// <summary>
    /// Shared project-file reader used to validate the resolved canonical project file.
    /// </summary>
    readonly ProjectFileReader ProjectFileReader;

    /// <summary>
    /// Creates one resolver backed by the shared canonical project-file reader.
    /// </summary>
    public ProjectFilePathResolver() {
        ProjectFileReader = new ProjectFileReader();
    }

    /// <summary>
    /// Resolves one project directory or `.heproj` file path to one validated canonical project file path.
    /// </summary>
    /// <param name="projectPath">Project directory path or direct `.heproj` file path.</param>
    /// <returns>Validated absolute canonical `.heproj` file path.</returns>
    public string Resolve(string projectPath) {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        string fullPath = Path.GetFullPath(projectPath);
        if (Directory.Exists(fullPath)) {
            fullPath = ResolveDirectoryProjectFilePath(fullPath);
        } else if (File.Exists(fullPath)) {
            ValidateProjectFileExtension(fullPath);
        } else {
            throw new InvalidOperationException($"Project path '{projectPath}' does not exist.");
        }

        ValidateCanonicalProjectFile(fullPath);
        return fullPath;
    }

    /// <summary>
    /// Resolves one project directory to its canonical `project.heproj` file path.
    /// </summary>
    /// <param name="projectDirectoryPath">Absolute project directory path.</param>
    /// <returns>Absolute canonical `.heproj` file path inside the supplied directory.</returns>
    static string ResolveDirectoryProjectFilePath(string projectDirectoryPath) {
        string projectFilePath = Path.Combine(projectDirectoryPath, CanonicalProjectFileName);
        if (!File.Exists(projectFilePath)) {
            throw new InvalidOperationException($"Project path '{projectDirectoryPath}' does not contain '{CanonicalProjectFileName}'.");
        }

        return projectFilePath;
    }

    /// <summary>
    /// Ensures one direct file input points to a `.heproj` file instead of an arbitrary file type.
    /// </summary>
    /// <param name="projectFilePath">Direct file path supplied by the caller.</param>
    static void ValidateProjectFileExtension(string projectFilePath) {
        if (!string.Equals(Path.GetExtension(projectFilePath), ".heproj", StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException("Project path must point to a '.heproj' file.");
        }
    }

    /// <summary>
    /// Ensures the resolved canonical project file parses and validates through the shared `.heproj` contract.
    /// </summary>
    /// <param name="projectFilePath">Canonical `.heproj` file path to validate.</param>
    void ValidateCanonicalProjectFile(string projectFilePath) {
        ProjectFileReadResult readResult = ProjectFileReader.ReadAsync(projectFilePath).GetAwaiter().GetResult();
        if (!readResult.Succeeded) {
            throw new InvalidOperationException(readResult.Errors[0].Message);
        }
    }
}

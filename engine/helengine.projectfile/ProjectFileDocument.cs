namespace helengine.projectfile;

/// <summary>
/// Represents the canonical in-memory `.heproj` project document shared between launcher and editor workflows.
/// </summary>
public sealed class ProjectFileDocument {
    /// <summary>
    /// Defines the current `.heproj` format version supported by this library.
    /// </summary>
    public const int SupportedProjectFormatVersion = 1;

    /// <summary>
    /// Gets or sets the persisted project-file format version used to validate compatibility.
    /// </summary>
    public int ProjectFormatVersion { get; set; } = SupportedProjectFormatVersion;

    /// <summary>
    /// Gets or sets the display name of the project.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the human-visible project version string.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the exact engine version required to open the project.
    /// </summary>
    public string RequiredEngineVersion { get; set; }

    /// <summary>
    /// Gets or sets the arbitrary platform identifiers supported by the project.
    /// </summary>
    public List<string> SupportedPlatforms { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC timestamp describing when the project was created.
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp describing the most recent open time tracked by the canonical project file.
    /// </summary>
    public DateTime LastOpened { get; set; }

    /// <summary>
    /// Gets or sets the optional project description displayed by tools such as the launcher.
    /// </summary>
    public string Description { get; set; }
}

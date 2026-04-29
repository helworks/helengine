namespace helengine.projectfile;

/// <summary>
/// Defines the raw JSON payload shape used to serialize and deserialize canonical `.heproj` project files.
/// </summary>
sealed class ProjectFileJsonModel {
    /// <summary>
    /// Gets or sets the persisted project-file format version.
    /// </summary>
    public int ProjectFormatVersion { get; set; }

    /// <summary>
    /// Gets or sets the project display name.
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
    /// Gets or sets the supported platform identifiers.
    /// </summary>
    public List<string> SupportedPlatforms { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC timestamp describing when the project was created.
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp describing the last open time tracked in the canonical project file.
    /// </summary>
    public DateTime LastOpened { get; set; }

    /// <summary>
    /// Gets or sets the optional project description displayed by tools such as the launcher.
    /// </summary>
    public string Description { get; set; }
}

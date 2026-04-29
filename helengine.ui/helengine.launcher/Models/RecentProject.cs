using System;
using System.Collections.Generic;

namespace helengine.editor.launcher.Models;

/// <summary>
/// Represents one launcher recent-project entry derived from the canonical shared project file.
/// </summary>
public sealed class RecentProject {
    /// <summary>
    /// Gets or sets the display name shown by the launcher.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full canonical `.heproj` path used as the project identity.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the most recent project open time shown by the launcher.
    /// </summary>
    public DateTime LastOpened { get; set; }

    /// <summary>
    /// Gets or sets the project creation time shown by the launcher.
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Gets or sets how many times the launcher has opened the project.
    /// </summary>
    public int TimesOpened { get; set; }

    /// <summary>
    /// Gets or sets the optional descriptive text shown on the recent-project card.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-visible project version string.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the exact engine version required by the canonical project file.
    /// </summary>
    public string RequiredEngineVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the arbitrary platform identifiers supported by the project.
    /// </summary>
    public IReadOnlyList<string> SupportedPlatforms { get; set; } = Array.Empty<string>();
}

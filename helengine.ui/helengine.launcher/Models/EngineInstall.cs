using System;
using System.IO;
using System.Text.Json.Serialization;

namespace helengine.editor.launcher.Models;

/// <summary>
/// Describes one installed engine version that the launcher can offer for project creation or editor startup.
/// </summary>
public sealed class EngineInstall {
    /// <summary>
    /// Gets or sets the exact engine version reported by the installed editor host.
    /// </summary>
    public string Version { get; set; } = "unknown";

    /// <summary>
    /// Gets or sets the absolute folder path where the engine is installed.
    /// </summary>
    public string InstallPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source label that explains how the launcher learned about this install.
    /// </summary>
    public string Source { get; set; } = "local";

    /// <summary>
    /// Gets or sets the timestamp when the launcher recorded or refreshed this install entry.
    /// </summary>
    public DateTime InstalledAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Gets or sets the assembly name or other source marker used to detect the install version.
    /// </summary>
    public string DetectedFrom { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the friendly display name for the install when one is available.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the user-facing engine name, falling back to the install-folder name when no friendly name exists.
    /// </summary>
    [JsonIgnore]
    public string DisplayName {
        get {
            if (!string.IsNullOrWhiteSpace(Name)) {
                return Name;
            }

            string trimmed = InstallPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.IsNullOrWhiteSpace(trimmed) ? "engine" : Path.GetFileName(trimmed);
        }
    }

    /// <summary>
    /// Gets the compact user-facing install summary shown in launcher lists.
    /// </summary>
    [JsonIgnore]
    public string Summary => $"{DisplayName} • v{Version}";
}

using System;
using System.Collections.Generic;

namespace helengine.editor.launcher.Models;

/// <summary>
/// Stores the installed-engine entries managed by the launcher under the engine install root.
/// </summary>
public sealed class EngineInstallManifest {
    /// <summary>
    /// Gets or sets the installed engines currently known to the launcher.
    /// </summary>
    public List<EngineInstall> Engines { get; set; } = new();

    /// <summary>
    /// Gets or sets the last time the manifest file was updated.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the schema version written into the install manifest file.
    /// </summary>
    public string SchemaVersion { get; set; } = "1.0";
}

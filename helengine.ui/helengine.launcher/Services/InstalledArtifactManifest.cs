using System;
using System.Collections.Generic;
using helengine.editor.launcher.Models;

namespace helengine.editor.launcher.Services;

/// <summary>
/// Stores the shared artifacts currently installed under the managed toolchain root.
/// </summary>
public sealed class InstalledArtifactManifest {
    /// <summary>
    /// Gets or sets the shared artifacts currently installed under the managed toolchain root.
    /// </summary>
    public List<InstalledArtifact> Artifacts { get; set; } = new();

    /// <summary>
    /// Gets or sets the last time the artifact manifest was updated.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the schema version written into the artifact manifest.
    /// </summary>
    public string SchemaVersion { get; set; } = "1.0";
}

using System;
using System.Collections.Generic;
using helengine.editor.launcher.Models;

namespace helengine.editor.launcher.Services;

/// <summary>
/// Stores the installed engine-platform bindings tracked under the managed toolchain root.
/// </summary>
public sealed class InstalledBindingManifest {
    /// <summary>
    /// Gets or sets the installed engine-platform bindings tracked by the launcher.
    /// </summary>
    public List<InstalledEnginePlatformBinding> Bindings { get; set; } = new();

    /// <summary>
    /// Gets or sets the last time the binding manifest was updated.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the schema version written into the binding manifest.
    /// </summary>
    public string SchemaVersion { get; set; } = "1.0";
}

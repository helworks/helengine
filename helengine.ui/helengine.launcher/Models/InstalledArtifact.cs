using System;

namespace helengine.editor.launcher.Models;

/// <summary>
/// Describes one shared artifact already installed under the managed toolchain root.
/// </summary>
public sealed class InstalledArtifact {
    /// <summary>
    /// Initializes one installed shared artifact entry.
    /// </summary>
    /// <param name="identity">Exact reusable artifact identity.</param>
    /// <param name="installPath">Absolute install path of the materialized artifact.</param>
    public InstalledArtifact(ArtifactIdentity identity, string installPath) {
        Identity = identity;
        InstallPath = installPath;
        InstalledAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the exact reusable artifact identity.
    /// </summary>
    public ArtifactIdentity Identity { get; }

    /// <summary>
    /// Gets the absolute install path of the materialized artifact.
    /// </summary>
    public string InstallPath { get; }

    /// <summary>
    /// Gets or sets the timestamp when the launcher recorded the installed artifact.
    /// </summary>
    public DateTime InstalledAt { get; set; }
}

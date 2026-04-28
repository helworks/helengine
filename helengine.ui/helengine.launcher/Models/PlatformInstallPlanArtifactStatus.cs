namespace helengine.editor.launcher.Models;

/// <summary>
/// Describes one artifact row produced by install planning for reuse, download, or blocking-state reporting.
/// </summary>
public sealed class PlatformInstallPlanArtifactStatus {
    /// <summary>
    /// Initializes one platform install plan artifact row.
    /// </summary>
    /// <param name="identity">Exact shared artifact identity described by the row.</param>
    /// <param name="platformId">Platform that requires the artifact.</param>
    /// <param name="installPath">Existing install path when the artifact is already present.</param>
    public PlatformInstallPlanArtifactStatus(ArtifactIdentity identity, string platformId, string installPath = "") {
        Identity = identity;
        PlatformId = platformId;
        InstallPath = installPath;
    }

    /// <summary>
    /// Gets the exact shared artifact identity described by the row.
    /// </summary>
    public ArtifactIdentity Identity { get; }

    /// <summary>
    /// Gets the platform that requires the artifact.
    /// </summary>
    public string PlatformId { get; }

    /// <summary>
    /// Gets the existing install path when the artifact is already materialized locally.
    /// </summary>
    public string InstallPath { get; }
}

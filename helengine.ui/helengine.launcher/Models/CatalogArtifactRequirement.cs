namespace helengine.editor.launcher.Models;

/// <summary>
/// Describes one reusable artifact requirement exposed by the central engine-platform catalog.
/// </summary>
public sealed class CatalogArtifactRequirement {
    /// <summary>
    /// Initializes one catalog artifact requirement.
    /// </summary>
    /// <param name="identity">Exact reusable artifact identity required by one platform.</param>
    public CatalogArtifactRequirement(ArtifactIdentity identity) {
        Identity = identity;
    }

    /// <summary>
    /// Gets the exact reusable artifact identity required by the catalog entry.
    /// </summary>
    public ArtifactIdentity Identity { get; }
}

namespace helengine.editor.launcher.Models;

/// <summary>
/// Describes the shared platform dependencies required for one target platform under a specific engine version.
/// </summary>
public sealed class EnginePlatformRequirement {
    /// <summary>
    /// Initializes one engine-platform requirement.
    /// </summary>
    /// <param name="platformId">Stable platform identifier chosen by the catalog.</param>
    /// <param name="sdk">Required platform SDK artifact.</param>
    /// <param name="platformBuilder">Required platform builder artifact.</param>
    /// <param name="platformFiles">Required shared platform files artifact.</param>
    public EnginePlatformRequirement(
        string platformId,
        CatalogArtifactRequirement sdk,
        CatalogArtifactRequirement platformBuilder,
        CatalogArtifactRequirement platformFiles) {
        PlatformId = platformId;
        Sdk = sdk;
        PlatformBuilder = platformBuilder;
        PlatformFiles = platformFiles;
    }

    /// <summary>
    /// Gets the stable platform identifier exposed to the launcher UI.
    /// </summary>
    public string PlatformId { get; }

    /// <summary>
    /// Gets the required platform SDK artifact.
    /// </summary>
    public CatalogArtifactRequirement Sdk { get; }

    /// <summary>
    /// Gets the required platform builder artifact.
    /// </summary>
    public CatalogArtifactRequirement PlatformBuilder { get; }

    /// <summary>
    /// Gets the required shared platform files artifact.
    /// </summary>
    public CatalogArtifactRequirement PlatformFiles { get; }
}

namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes the typed platform metadata a builder exposes to the editor.
/// </summary>
public class PlatformDefinition {
    /// <summary>
    /// Initializes one platform definition.
    /// </summary>
    /// <param name="platformId">Stable platform identifier.</param>
    /// <param name="displayName">Human-readable platform name.</param>
    /// <param name="buildProfiles">Build profiles exposed by the platform.</param>
    /// <param name="graphicsProfiles">Graphics profiles exposed by the platform.</param>
    /// <param name="assetRequirements">Asset requirements exposed by the platform.</param>
    public PlatformDefinition(
        string platformId,
        string displayName,
        PlatformBuildProfileDefinition[] buildProfiles,
        PlatformGraphicsProfileDefinition[] graphicsProfiles,
        PlatformAssetRequirementDefinition[] assetRequirements) {
        if (string.IsNullOrWhiteSpace(platformId)) {
            throw new ArgumentException("Platform id is required.", nameof(platformId));
        } else if (string.IsNullOrWhiteSpace(displayName)) {
            throw new ArgumentException("Platform display name is required.", nameof(displayName));
        } else if (buildProfiles == null) {
            throw new ArgumentNullException(nameof(buildProfiles), "Build profiles are required.");
        } else if (graphicsProfiles == null) {
            throw new ArgumentNullException(nameof(graphicsProfiles), "Graphics profiles are required.");
        } else if (assetRequirements == null) {
            throw new ArgumentNullException(nameof(assetRequirements), "Asset requirements are required.");
        } else if (Array.Exists(buildProfiles, buildProfile => buildProfile == null)) {
            throw new ArgumentException("Build profiles cannot contain null entries.", nameof(buildProfiles));
        } else if (Array.Exists(graphicsProfiles, graphicsProfile => graphicsProfile == null)) {
            throw new ArgumentException("Graphics profiles cannot contain null entries.", nameof(graphicsProfiles));
        } else if (Array.Exists(assetRequirements, assetRequirement => assetRequirement == null)) {
            throw new ArgumentException("Asset requirements cannot contain null entries.", nameof(assetRequirements));
        }

        PlatformId = platformId;
        DisplayName = displayName;
        BuildProfiles = [.. buildProfiles];
        GraphicsProfiles = [.. graphicsProfiles];
        AssetRequirements = [.. assetRequirements];
    }

    /// <summary>
    /// Gets the stable platform identifier.
    /// </summary>
    public string PlatformId { get; }

    /// <summary>
    /// Gets the human-readable platform name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the build profiles exposed by the platform.
    /// </summary>
    public PlatformBuildProfileDefinition[] BuildProfiles { get; }

    /// <summary>
    /// Gets the graphics profiles exposed by the platform.
    /// </summary>
    public PlatformGraphicsProfileDefinition[] GraphicsProfiles { get; }

    /// <summary>
    /// Gets the asset requirements exposed by the platform.
    /// </summary>
    public PlatformAssetRequirementDefinition[] AssetRequirements { get; }
}

namespace helengine.baseplatform.Manifest;

/// <summary>
/// Defines the complete resolved manifest a platform builder consumes to cook game content.
/// </summary>
public class PlatformBuildManifest {
    /// <summary>
    /// Initializes a fully resolved build manifest with first-class scenes and loose assets.
    /// </summary>
    /// <param name="manifestVersion">The manifest schema version.</param>
    /// <param name="projectId">The stable project identity for the build.</param>
    /// <param name="projectVersion">The project version being built.</param>
    /// <param name="requiredEngineVersion">The exact engine version the cooked output targets.</param>
    /// <param name="scenes">The fully resolved scenes the builder must cook.</param>
    /// <param name="looseAssets">The fully resolved loose assets the builder must cook.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the manifest version is less than one.</exception>
    /// <exception cref="ArgumentException">Thrown when any required string value is missing.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the scene or loose-asset collections are missing.</exception>
    /// <exception cref="ArgumentException">Thrown when a collection contains a missing entry.</exception>
    public PlatformBuildManifest(
        int manifestVersion,
        string projectId,
        string projectVersion,
        string requiredEngineVersion,
        PlatformBuildScene[] scenes,
        PlatformBuildAsset[] looseAssets) {
        if (manifestVersion < 1) {
            throw new ArgumentOutOfRangeException(nameof(manifestVersion), "Manifest version must be at least 1.");
        } else if (string.IsNullOrWhiteSpace(projectId)) {
            throw new ArgumentException("Project id is required.", nameof(projectId));
        } else if (string.IsNullOrWhiteSpace(projectVersion)) {
            throw new ArgumentException("Project version is required.", nameof(projectVersion));
        } else if (string.IsNullOrWhiteSpace(requiredEngineVersion)) {
            throw new ArgumentException("Required engine version is required.", nameof(requiredEngineVersion));
        } else if (scenes == null) {
            throw new ArgumentNullException(nameof(scenes), "Scene collection is required.");
        } else if (Array.Exists(scenes, scene => scene == null)) {
            throw new ArgumentException("Scene collection cannot contain null entries.", nameof(scenes));
        } else if (looseAssets == null) {
            throw new ArgumentNullException(nameof(looseAssets), "Loose asset collection is required.");
        } else if (Array.Exists(looseAssets, asset => asset == null)) {
            throw new ArgumentException("Loose asset collection cannot contain null entries.", nameof(looseAssets));
        }

        ManifestVersion = manifestVersion;
        ProjectId = projectId;
        ProjectVersion = projectVersion;
        RequiredEngineVersion = requiredEngineVersion;
        Scenes = [.. scenes];
        LooseAssets = [.. looseAssets];
    }

    /// <summary>
    /// Gets the manifest schema version.
    /// </summary>
    public int ManifestVersion { get; }

    /// <summary>
    /// Gets the stable project identity for the build.
    /// </summary>
    public string ProjectId { get; }

    /// <summary>
    /// Gets the project version being built.
    /// </summary>
    public string ProjectVersion { get; }

    /// <summary>
    /// Gets the exact engine version the cooked output targets.
    /// </summary>
    public string RequiredEngineVersion { get; }

    /// <summary>
    /// Gets the fully resolved scenes the builder must cook.
    /// </summary>
    public PlatformBuildScene[] Scenes { get; }

    /// <summary>
    /// Gets the fully resolved loose assets the builder must cook.
    /// </summary>
    public PlatformBuildAsset[] LooseAssets { get; }
}

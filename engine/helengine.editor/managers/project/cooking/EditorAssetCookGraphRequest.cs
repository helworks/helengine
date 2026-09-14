using helengine;

namespace helengine.editor;

/// <summary>
/// Captures the canonical inputs used to discover and execute one asset-cook graph.
/// </summary>
public sealed class EditorAssetCookGraphRequest {
    /// <summary>
    /// Initializes a cook graph request.
    /// </summary>
    /// <param name="rootReferences">Canonical root references in build order.</param>
    /// <param name="platformId">Target platform identity.</param>
    /// <param name="profileId">Target profile identity.</param>
    /// <param name="engineVersion">Exact engine version pin.</param>
    /// <param name="sourceRootPath">Project source root used for resolution.</param>
    public EditorAssetCookGraphRequest(
        IEnumerable<SceneAssetReference> rootReferences,
        string platformId,
        string profileId,
        string engineVersion,
        string sourceRootPath) {
        if (rootReferences == null) {
            throw new ArgumentNullException(nameof(rootReferences));
        }
        if (string.IsNullOrWhiteSpace(platformId)) {
            throw new ArgumentException("Platform id must be provided.", nameof(platformId));
        }
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Profile id must be provided.", nameof(profileId));
        }
        if (string.IsNullOrWhiteSpace(engineVersion)) {
            throw new ArgumentException("Engine version must be provided.", nameof(engineVersion));
        }
        if (string.IsNullOrWhiteSpace(sourceRootPath)) {
            throw new ArgumentException("Source root path must be provided.", nameof(sourceRootPath));
        }

        RootReferences = Array.AsReadOnly(rootReferences.Select(reference => reference ?? throw new ArgumentException("Root references cannot contain null values.", nameof(rootReferences))).ToArray());
        if (RootReferences.Count == 0) {
            throw new ArgumentException("At least one root reference must be provided.", nameof(rootReferences));
        }
        PlatformId = platformId;
        ProfileId = profileId;
        EngineVersion = engineVersion;
        SourceRootPath = Path.GetFullPath(sourceRootPath);
    }

    /// <summary>Gets root references in caller-provided build order.</summary>
    public IReadOnlyList<SceneAssetReference> RootReferences { get; }
    /// <summary>Gets the target platform identity.</summary>
    public string PlatformId { get; }
    /// <summary>Gets the target profile identity.</summary>
    public string ProfileId { get; }
    /// <summary>Gets the exact engine version pin.</summary>
    public string EngineVersion { get; }
    /// <summary>Gets the canonical source root path.</summary>
    public string SourceRootPath { get; }
}

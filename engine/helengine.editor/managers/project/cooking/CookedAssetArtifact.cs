namespace helengine.editor;

/// <summary>
/// Describes one immutable artifact published by the cook graph.
/// </summary>
public sealed class CookedAssetArtifact {
    /// <summary>
    /// Initializes one cooked artifact receipt.
    /// </summary>
    /// <param name="cookKey">Node key that produced the artifact.</param>
    /// <param name="runtimeKind">Runtime asset kind.</param>
    /// <param name="formatVersion">Current runtime format version.</param>
    /// <param name="contentHash">Content hash of the artifact bytes.</param>
    /// <param name="byteLength">Artifact byte length.</param>
    /// <param name="storePath">Path inside the artifact store.</param>
    /// <param name="dependencyKeys">Dependencies embedded in the artifact.</param>
    /// <param name="platformId">Target platform identity.</param>
    /// <param name="profileId">Target profile identity.</param>
    public CookedAssetArtifact(
        EditorAssetCookNodeKey cookKey,
        AssetEntryKind runtimeKind,
        string formatVersion,
        string contentHash,
        long byteLength,
        string storePath,
        IEnumerable<EditorAssetCookNodeKey> dependencyKeys,
        string platformId,
        string profileId) {
        CookKey = cookKey ?? throw new ArgumentNullException(nameof(cookKey));
        if (string.IsNullOrWhiteSpace(formatVersion)) {
            throw new ArgumentException("Format version must be provided.", nameof(formatVersion));
        }
        if (string.IsNullOrWhiteSpace(contentHash)) {
            throw new ArgumentException("Content hash must be provided.", nameof(contentHash));
        }
        if (byteLength < 0) {
            throw new ArgumentOutOfRangeException(nameof(byteLength));
        }
        if (string.IsNullOrWhiteSpace(storePath)) {
            throw new ArgumentException("Store path must be provided.", nameof(storePath));
        }
        if (dependencyKeys == null) {
            throw new ArgumentNullException(nameof(dependencyKeys));
        }
        if (string.IsNullOrWhiteSpace(platformId)) {
            throw new ArgumentException("Platform id must be provided.", nameof(platformId));
        }
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Profile id must be provided.", nameof(profileId));
        }

        RuntimeKind = runtimeKind;
        FormatVersion = formatVersion;
        ContentHash = contentHash;
        ByteLength = byteLength;
        StorePath = storePath;
        DependencyKeys = Array.AsReadOnly(dependencyKeys.ToArray());
        PlatformId = platformId;
        ProfileId = profileId;
    }

    /// <summary>Gets the producing cook key.</summary>
    public EditorAssetCookNodeKey CookKey { get; }
    /// <summary>Gets the runtime asset kind.</summary>
    public AssetEntryKind RuntimeKind { get; }
    /// <summary>Gets the current serialized format version.</summary>
    public string FormatVersion { get; }
    /// <summary>Gets the artifact content hash.</summary>
    public string ContentHash { get; }
    /// <summary>Gets the artifact byte length.</summary>
    public long ByteLength { get; }
    /// <summary>Gets the relative artifact-store path.</summary>
    public string StorePath { get; }
    /// <summary>Gets immutable embedded dependency keys.</summary>
    public IReadOnlyList<EditorAssetCookNodeKey> DependencyKeys { get; }
    /// <summary>Gets the target platform identity.</summary>
    public string PlatformId { get; }
    /// <summary>Gets the target profile identity.</summary>
    public string ProfileId { get; }
}

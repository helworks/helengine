namespace helengine.baseplatform.Manifest;

/// <summary>
/// Describes one builder-owned platform cook task emitted by the editor-owned build graph.
/// </summary>
public sealed class PlatformCookWorkItem {
    /// <summary>
    /// Creates one builder-owned platform cook work item.
    /// </summary>
    /// <param name="workItemId">Stable work item identifier.</param>
    /// <param name="sourceAssetPath">Source asset path the builder should cook from.</param>
    /// <param name="sourceAssetKind">Generic source asset kind.</param>
    /// <param name="targetPlatformId">Target platform identifier that owns the cook.</param>
    /// <param name="targetArtifactKind">Runtime artifact kind that the cook produces.</param>
    /// <param name="outputRelativePath">Final runtime-relative output path to write.</param>
    /// <param name="outputLogicalArtifactId">Stable logical id for the produced runtime artifact.</param>
    /// <param name="sourceContentHash">Stable source-content hash for cacheability and diagnostics.</param>
    /// <param name="settingsHash">Stable hash for the resolved platform cook settings.</param>
    /// <param name="serializedPlatformSettings">Serialized resolved platform settings payload for the builder cook.</param>
    /// <param name="metadata">Optional platform-neutral metadata entries attached to the work item.</param>
    public PlatformCookWorkItem(
        string workItemId,
        string sourceAssetPath,
        string sourceAssetKind,
        string targetPlatformId,
        string targetArtifactKind,
        string outputRelativePath,
        string outputLogicalArtifactId,
        string sourceContentHash,
        string settingsHash,
        string serializedPlatformSettings,
        PlatformCookWorkItemMetadata[] metadata) {
        if (string.IsNullOrWhiteSpace(workItemId)) {
            throw new ArgumentException("Work item id is required.", nameof(workItemId));
        } else if (string.IsNullOrWhiteSpace(sourceAssetPath)) {
            throw new ArgumentException("Source asset path is required.", nameof(sourceAssetPath));
        } else if (string.IsNullOrWhiteSpace(sourceAssetKind)) {
            throw new ArgumentException("Source asset kind is required.", nameof(sourceAssetKind));
        } else if (string.IsNullOrWhiteSpace(targetPlatformId)) {
            throw new ArgumentException("Target platform id is required.", nameof(targetPlatformId));
        } else if (string.IsNullOrWhiteSpace(targetArtifactKind)) {
            throw new ArgumentException("Target artifact kind is required.", nameof(targetArtifactKind));
        } else if (string.IsNullOrWhiteSpace(outputRelativePath)) {
            throw new ArgumentException("Output relative path is required.", nameof(outputRelativePath));
        } else if (string.IsNullOrWhiteSpace(outputLogicalArtifactId)) {
            throw new ArgumentException("Output logical artifact id is required.", nameof(outputLogicalArtifactId));
        } else if (string.IsNullOrWhiteSpace(sourceContentHash)) {
            throw new ArgumentException("Source content hash is required.", nameof(sourceContentHash));
        } else if (string.IsNullOrWhiteSpace(settingsHash)) {
            throw new ArgumentException("Settings hash is required.", nameof(settingsHash));
        } else if (serializedPlatformSettings == null) {
            throw new ArgumentNullException(nameof(serializedPlatformSettings), "Serialized platform settings are required.");
        } else if (metadata == null) {
            throw new ArgumentNullException(nameof(metadata), "Metadata collection is required.");
        } else if (Array.Exists(metadata, entry => entry == null)) {
            throw new ArgumentException("Metadata collection cannot contain null entries.", nameof(metadata));
        }

        WorkItemId = workItemId;
        SourceAssetPath = sourceAssetPath;
        SourceAssetKind = sourceAssetKind;
        TargetPlatformId = targetPlatformId;
        TargetArtifactKind = targetArtifactKind;
        OutputRelativePath = outputRelativePath;
        OutputLogicalArtifactId = outputLogicalArtifactId;
        SourceContentHash = sourceContentHash;
        SettingsHash = settingsHash;
        SerializedPlatformSettings = serializedPlatformSettings;
        Metadata = [.. metadata];
    }

    /// <summary>
    /// Gets the stable work item identifier.
    /// </summary>
    public string WorkItemId { get; }

    /// <summary>
    /// Gets the source asset path the builder should cook from.
    /// </summary>
    public string SourceAssetPath { get; }

    /// <summary>
    /// Gets the generic source asset kind.
    /// </summary>
    public string SourceAssetKind { get; }

    /// <summary>
    /// Gets the target platform identifier that owns this cook.
    /// </summary>
    public string TargetPlatformId { get; }

    /// <summary>
    /// Gets the runtime artifact kind produced by this cook.
    /// </summary>
    public string TargetArtifactKind { get; }

    /// <summary>
    /// Gets the final runtime-relative output path to write.
    /// </summary>
    public string OutputRelativePath { get; }

    /// <summary>
    /// Gets the stable logical artifact id for the produced output.
    /// </summary>
    public string OutputLogicalArtifactId { get; }

    /// <summary>
    /// Gets the stable source-content hash for cacheability and diagnostics.
    /// </summary>
    public string SourceContentHash { get; }

    /// <summary>
    /// Gets the stable hash for the resolved platform cook settings.
    /// </summary>
    public string SettingsHash { get; }

    /// <summary>
    /// Gets the serialized resolved platform settings payload for the builder cook.
    /// </summary>
    public string SerializedPlatformSettings { get; }

    /// <summary>
    /// Gets the platform-neutral metadata attached to the work item.
    /// </summary>
    public PlatformCookWorkItemMetadata[] Metadata { get; }
}

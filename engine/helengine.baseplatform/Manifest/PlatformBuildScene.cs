namespace helengine.baseplatform.Manifest;

/// <summary>
/// Describes a fully resolved scene and the exact payloads required to cook it for a target platform.
/// </summary>
public class PlatformBuildScene {
    /// <summary>
    /// Initializes a scene entry with resolved payload references and scene metadata.
    /// </summary>
    /// <param name="sceneId">The stable logical scene id.</param>
    /// <param name="sceneName">The display name of the scene.</param>
    /// <param name="sourceIdentity">The source identity or path for the scene definition.</param>
    /// <param name="payloadReferences">The exact payloads the builder must process for this scene.</param>
    /// <param name="resolvedMetadata">The resolved scene metadata already prepared by the caller.</param>
    /// <exception cref="ArgumentException">Thrown when a required string value is missing.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the payload or metadata collections are missing.</exception>
    /// <exception cref="ArgumentException">Thrown when any payload reference entry is missing.</exception>
    public PlatformBuildScene(
        string sceneId,
        string sceneName,
        string sourceIdentity,
        PlatformBuildPayloadReference[] payloadReferences,
        KeyValuePair<string, string>[] resolvedMetadata) {
        if (string.IsNullOrWhiteSpace(sceneId)) {
            throw new ArgumentException("Scene id is required.", nameof(sceneId));
        } else if (string.IsNullOrWhiteSpace(sceneName)) {
            throw new ArgumentException("Scene name is required.", nameof(sceneName));
        } else if (string.IsNullOrWhiteSpace(sourceIdentity)) {
            throw new ArgumentException("Scene source identity is required.", nameof(sourceIdentity));
        } else if (payloadReferences == null) {
            throw new ArgumentNullException(nameof(payloadReferences), "Scene payload references are required.");
        } else if (Array.Exists(payloadReferences, payloadReference => payloadReference == null)) {
            throw new ArgumentException("Scene payload references cannot contain null entries.", nameof(payloadReferences));
        } else if (resolvedMetadata == null) {
            throw new ArgumentNullException(nameof(resolvedMetadata), "Scene metadata is required.");
        }

        SceneId = sceneId;
        SceneName = sceneName;
        SourceIdentity = sourceIdentity;
        PayloadReferences = [.. payloadReferences];
        ResolvedMetadata = [.. resolvedMetadata];
    }

    /// <summary>
    /// Gets the stable logical scene id.
    /// </summary>
    public string SceneId { get; }

    /// <summary>
    /// Gets the display name of the scene.
    /// </summary>
    public string SceneName { get; }

    /// <summary>
    /// Gets the source identity or path for the scene definition.
    /// </summary>
    public string SourceIdentity { get; }

    /// <summary>
    /// Gets the exact payloads the builder must process for this scene.
    /// </summary>
    public PlatformBuildPayloadReference[] PayloadReferences { get; }

    /// <summary>
    /// Gets the resolved scene metadata already prepared by the caller.
    /// </summary>
    public KeyValuePair<string, string>[] ResolvedMetadata { get; }
}

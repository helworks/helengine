namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes one runtime storage profile exposed by a platform builder.
/// </summary>
public sealed class PlatformStorageProfileDefinition {
    /// <summary>
    /// Initializes one storage profile definition.
    /// </summary>
    /// <param name="profileId">Stable storage profile identifier.</param>
    /// <param name="displayName">Human-readable label shown in the editor.</param>
    /// <param name="storageKind">The runtime storage model emitted by the profile.</param>
    /// <param name="runtimeSpecializationId">The runtime specialization id selected by this storage profile.</param>
    /// <param name="allowContainerSegmentation">Whether the profile may split outputs into multiple containers.</param>
    public PlatformStorageProfileDefinition(
        string profileId,
        string displayName,
        PlatformStorageProfileKind storageKind,
        string runtimeSpecializationId,
        bool allowContainerSegmentation) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Storage profile id is required.", nameof(profileId));
        } else if (string.IsNullOrWhiteSpace(displayName)) {
            throw new ArgumentException("Storage profile display name is required.", nameof(displayName));
        } else if (string.IsNullOrWhiteSpace(runtimeSpecializationId)) {
            throw new ArgumentException("Storage profile runtime specialization id is required.", nameof(runtimeSpecializationId));
        }

        ProfileId = profileId;
        DisplayName = displayName;
        StorageKind = storageKind;
        RuntimeSpecializationId = runtimeSpecializationId;
        AllowContainerSegmentation = allowContainerSegmentation;
    }

    /// <summary>
    /// Gets the stable storage profile identifier.
    /// </summary>
    public string ProfileId { get; }

    /// <summary>
    /// Gets the human-readable label shown in the editor.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the runtime storage model emitted by the profile.
    /// </summary>
    public PlatformStorageProfileKind StorageKind { get; }

    /// <summary>
    /// Gets the runtime specialization id selected by this storage profile.
    /// </summary>
    public string RuntimeSpecializationId { get; }

    /// <summary>
    /// Gets whether the profile may split output into multiple containers.
    /// </summary>
    public bool AllowContainerSegmentation { get; }
}

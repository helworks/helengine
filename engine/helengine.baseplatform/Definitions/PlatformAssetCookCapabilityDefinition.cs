namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes one generic asset-kind cook contract a platform publishes to the editor build graph.
/// </summary>
public sealed class PlatformAssetCookCapabilityDefinition {
    /// <summary>
    /// Initializes one platform asset-cook capability.
    /// </summary>
    /// <param name="sourceAssetKind">Generic source asset kind the capability accepts.</param>
    /// <param name="targetArtifactKind">Runtime artifact kind the capability produces.</param>
    /// <param name="ownershipKind">Which side of the build graph owns the final cook for this asset kind.</param>
    /// <param name="settingsContractId">Stable settings-contract identifier the editor resolves for the builder.</param>
    /// <param name="defaultSerializedPlatformSettings">Optional serialized default settings payload used when the source asset has no explicit platform override.</param>
    /// <param name="textureFormatCapabilities">Optional texture format capability metadata used to constrain generic texture settings for this cook capability.</param>
    public PlatformAssetCookCapabilityDefinition(
        string sourceAssetKind,
        string targetArtifactKind,
        PlatformAssetCookOwnershipKind ownershipKind,
        string settingsContractId,
        string defaultSerializedPlatformSettings = "",
        PlatformTextureFormatCapabilityDefinition textureFormatCapabilities = null) {
        if (string.IsNullOrWhiteSpace(sourceAssetKind)) {
            throw new ArgumentException("Source asset kind is required.", nameof(sourceAssetKind));
        } else if (string.IsNullOrWhiteSpace(targetArtifactKind)) {
            throw new ArgumentException("Target artifact kind is required.", nameof(targetArtifactKind));
        } else if (string.IsNullOrWhiteSpace(settingsContractId)) {
            throw new ArgumentException("Settings contract id is required.", nameof(settingsContractId));
        } else if (defaultSerializedPlatformSettings == null) {
            throw new ArgumentNullException(nameof(defaultSerializedPlatformSettings), "Default serialized platform settings are required.");
        }

        SourceAssetKind = sourceAssetKind;
        TargetArtifactKind = targetArtifactKind;
        OwnershipKind = ownershipKind;
        SettingsContractId = settingsContractId;
        DefaultSerializedPlatformSettings = defaultSerializedPlatformSettings;
        TextureFormatCapabilities = textureFormatCapabilities;
    }

    /// <summary>
    /// Gets the generic source asset kind the capability accepts.
    /// </summary>
    public string SourceAssetKind { get; }

    /// <summary>
    /// Gets the runtime artifact kind the capability produces.
    /// </summary>
    public string TargetArtifactKind { get; }

    /// <summary>
    /// Gets which side of the build graph owns the final cook for this asset kind.
    /// </summary>
    public PlatformAssetCookOwnershipKind OwnershipKind { get; }

    /// <summary>
    /// Gets the stable settings-contract identifier the editor resolves for the builder.
    /// </summary>
    public string SettingsContractId { get; }

    /// <summary>
    /// Gets the optional serialized default settings payload used when the source asset has no explicit platform override.
    /// </summary>
    public string DefaultSerializedPlatformSettings { get; }

    /// <summary>
    /// Gets the optional generic texture format capability metadata used to constrain texture settings for this cook capability.
    /// </summary>
    public PlatformTextureFormatCapabilityDefinition TextureFormatCapabilities { get; }
}

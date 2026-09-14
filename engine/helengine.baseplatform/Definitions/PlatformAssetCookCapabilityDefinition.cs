namespace helengine.baseplatform.Definitions;

/// <summary>Describes one generic asset-kind cook contract a platform publishes to the editor build graph.</summary>
public sealed class PlatformAssetCookCapabilityDefinition {
    /// <summary>Initializes one platform asset-cook capability with default naming and no audio limit.</summary>
    public PlatformAssetCookCapabilityDefinition(
        string sourceAssetKind,
        string targetArtifactKind,
        PlatformAssetCookOwnershipKind ownershipKind,
        string settingsContractId,
        string defaultSerializedPlatformSettings = "",
        PlatformTextureFormatCapabilityDefinition textureFormatCapabilities = null)
        : this(sourceAssetKind, targetArtifactKind, ownershipKind, settingsContractId, defaultSerializedPlatformSettings, textureFormatCapabilities, string.Empty, PlatformAssetNamingPolicy.PreserveAssetId, null) { }

    /// <summary>Initializes one platform asset-cook capability with an explicit output extension.</summary>
    public PlatformAssetCookCapabilityDefinition(
        string sourceAssetKind,
        string targetArtifactKind,
        PlatformAssetCookOwnershipKind ownershipKind,
        string settingsContractId,
        string defaultSerializedPlatformSettings,
        PlatformTextureFormatCapabilityDefinition textureFormatCapabilities,
        string outputFileExtension)
        : this(sourceAssetKind, targetArtifactKind, ownershipKind, settingsContractId, defaultSerializedPlatformSettings, textureFormatCapabilities, outputFileExtension, PlatformAssetNamingPolicy.PreserveAssetId, null) { }

    /// <summary>Initializes one capability with explicit naming and audio policies.</summary>
    public PlatformAssetCookCapabilityDefinition(
        string sourceAssetKind,
        string targetArtifactKind,
        PlatformAssetCookOwnershipKind ownershipKind,
        string settingsContractId,
        string defaultSerializedPlatformSettings,
        PlatformTextureFormatCapabilityDefinition textureFormatCapabilities,
        string outputFileExtension,
        PlatformAssetNamingPolicy namingPolicy,
        PlatformAudioLimits audioLimits) {
        if (string.IsNullOrWhiteSpace(sourceAssetKind)) throw new ArgumentException("Source asset kind is required.", nameof(sourceAssetKind));
        if (string.IsNullOrWhiteSpace(targetArtifactKind)) throw new ArgumentException("Target artifact kind is required.", nameof(targetArtifactKind));
        if (string.IsNullOrWhiteSpace(settingsContractId)) throw new ArgumentException("Settings contract id is required.", nameof(settingsContractId));
        if (defaultSerializedPlatformSettings == null) throw new ArgumentNullException(nameof(defaultSerializedPlatformSettings));
        if (outputFileExtension == null) throw new ArgumentNullException(nameof(outputFileExtension));
        SourceAssetKind = sourceAssetKind;
        TargetArtifactKind = targetArtifactKind;
        OwnershipKind = ownershipKind;
        SettingsContractId = settingsContractId;
        DefaultSerializedPlatformSettings = defaultSerializedPlatformSettings;
        TextureFormatCapabilities = textureFormatCapabilities;
        OutputFileExtension = outputFileExtension;
        NamingPolicy = namingPolicy;
        AudioLimits = audioLimits;
    }

    /// <summary>Gets the generic source asset kind the capability accepts.</summary>
    public string SourceAssetKind { get; }
    /// <summary>Gets the runtime artifact kind the capability produces.</summary>
    public string TargetArtifactKind { get; }
    /// <summary>Gets which side of the build graph owns the final cook for this asset kind.</summary>
    public PlatformAssetCookOwnershipKind OwnershipKind { get; }
    /// <summary>Gets the stable settings-contract identifier.</summary>
    public string SettingsContractId { get; }
    /// <summary>Gets optional serialized default platform settings.</summary>
    public string DefaultSerializedPlatformSettings { get; }
    /// <summary>Gets optional texture format metadata.</summary>
    public PlatformTextureFormatCapabilityDefinition TextureFormatCapabilities { get; }
    /// <summary>Gets the platform-owned output file extension.</summary>
    public string OutputFileExtension { get; }
    /// <summary>Gets the runtime naming policy.</summary>
    public PlatformAssetNamingPolicy NamingPolicy { get; }
    /// <summary>Gets optional maximum audio limits.</summary>
    public PlatformAudioLimits AudioLimits { get; }
}
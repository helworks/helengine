using helengine;

namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes the generic texture color formats, alpha precisions, and valid combinations supported by a platform cook capability.
/// </summary>
public sealed class PlatformTextureFormatCapabilityDefinition {
    /// <summary>
    /// Initializes one platform texture format capability definition.
    /// </summary>
    /// <param name="supportedColorFormatIds">Platform-published texture color-format identifiers the platform supports for this cook capability.</param>
    /// <param name="supportedAlphaPrecisions">Texture alpha precisions the platform supports for this cook capability.</param>
    /// <param name="supportedCombinations">Valid color-format and alpha-precision combinations the platform supports for this cook capability.</param>
    public PlatformTextureFormatCapabilityDefinition(
        IReadOnlyList<string> supportedColorFormatIds,
        IReadOnlyList<TextureAssetAlphaPrecision> supportedAlphaPrecisions,
        IReadOnlyList<PlatformTextureFormatCombinationDefinition> supportedCombinations) {
        if (supportedColorFormatIds == null) {
            throw new ArgumentNullException(nameof(supportedColorFormatIds));
        } else if (supportedAlphaPrecisions == null) {
            throw new ArgumentNullException(nameof(supportedAlphaPrecisions));
        } else if (supportedCombinations == null) {
            throw new ArgumentNullException(nameof(supportedCombinations));
        }

        SupportedColorFormatIds = [.. supportedColorFormatIds];
        SupportedAlphaPrecisions = [.. supportedAlphaPrecisions];
        SupportedCombinations = [.. supportedCombinations];
    }

    /// <summary>
    /// Gets the platform-published texture color-format identifiers the platform supports for this cook capability.
    /// </summary>
    public string[] SupportedColorFormatIds { get; }

    /// <summary>
    /// Gets the texture alpha precisions the platform supports for this cook capability.
    /// </summary>
    public TextureAssetAlphaPrecision[] SupportedAlphaPrecisions { get; }

    /// <summary>
    /// Gets the valid color-format and alpha-precision combinations the platform supports for this cook capability.
    /// </summary>
    public PlatformTextureFormatCombinationDefinition[] SupportedCombinations { get; }
}

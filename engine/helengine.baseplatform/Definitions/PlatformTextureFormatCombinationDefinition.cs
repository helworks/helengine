using helengine;

namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes one valid texture color-format and alpha-precision pair supported by a platform cook capability.
/// </summary>
public sealed class PlatformTextureFormatCombinationDefinition {
    /// <summary>
    /// Initializes one valid platform texture format combination.
    /// </summary>
    /// <param name="colorFormat">Supported texture color format.</param>
    /// <param name="alphaPrecision">Supported texture alpha precision for the color format.</param>
    public PlatformTextureFormatCombinationDefinition(TextureAssetColorFormat colorFormat, TextureAssetAlphaPrecision alphaPrecision) {
        ColorFormat = colorFormat;
        AlphaPrecision = alphaPrecision;
    }

    /// <summary>
    /// Gets the supported texture color format.
    /// </summary>
    public TextureAssetColorFormat ColorFormat { get; }

    /// <summary>
    /// Gets the supported texture alpha precision for the color format.
    /// </summary>
    public TextureAssetAlphaPrecision AlphaPrecision { get; }
}

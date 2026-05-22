using helengine;

namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes one valid texture color-format and alpha-precision pair supported by a platform cook capability.
/// </summary>
public sealed class PlatformTextureFormatCombinationDefinition {
    /// <summary>
    /// Initializes one valid platform texture format combination.
    /// </summary>
    /// <param name="colorFormatId">Supported platform-published texture color-format identifier.</param>
    /// <param name="alphaPrecision">Supported texture alpha precision for the color format.</param>
    public PlatformTextureFormatCombinationDefinition(string colorFormatId, TextureAssetAlphaPrecision alphaPrecision) {
        if (string.IsNullOrWhiteSpace(colorFormatId)) {
            throw new ArgumentException("Texture color format id must be provided.", nameof(colorFormatId));
        }

        ColorFormatId = colorFormatId;
        AlphaPrecision = alphaPrecision;
    }

    /// <summary>
    /// Gets the supported platform-published texture color-format identifier.
    /// </summary>
    public string ColorFormatId { get; }

    /// <summary>
    /// Gets the supported texture alpha precision for the color format.
    /// </summary>
    public TextureAssetAlphaPrecision AlphaPrecision { get; }
}

using helengine.baseplatform.Definitions;
using Xunit;

namespace helengine.baseplatform.tests.Definitions;

/// <summary>
/// Verifies generic platform texture format capability metadata definitions.
/// </summary>
public sealed class PlatformTextureFormatCapabilityDefinitionTests {
    /// <summary>
    /// Verifies one texture format capability preserves declared format arrays and valid combinations in order.
    /// </summary>
    [Fact]
    public void Constructor_preserves_declared_formats_and_combinations() {
        PlatformTextureFormatCapabilityDefinition definition = new(
            [TextureAssetColorFormat.Rgba4444, TextureAssetColorFormat.Indexed8],
            [TextureAssetAlphaPrecision.A4, TextureAssetAlphaPrecision.A8],
            [
                new PlatformTextureFormatCombinationDefinition(TextureAssetColorFormat.Rgba4444, TextureAssetAlphaPrecision.A4),
                new PlatformTextureFormatCombinationDefinition(TextureAssetColorFormat.Indexed8, TextureAssetAlphaPrecision.A8)
            ]);

        Assert.Equal(
            [TextureAssetColorFormat.Rgba4444, TextureAssetColorFormat.Indexed8],
            definition.SupportedColorFormats);
        Assert.Equal(
            [TextureAssetAlphaPrecision.A4, TextureAssetAlphaPrecision.A8],
            definition.SupportedAlphaPrecisions);
        Assert.Collection(
            definition.SupportedCombinations,
            combination => {
                Assert.Equal(TextureAssetColorFormat.Rgba4444, combination.ColorFormat);
                Assert.Equal(TextureAssetAlphaPrecision.A4, combination.AlphaPrecision);
            },
            combination => {
                Assert.Equal(TextureAssetColorFormat.Indexed8, combination.ColorFormat);
                Assert.Equal(TextureAssetAlphaPrecision.A8, combination.AlphaPrecision);
            });
    }
}

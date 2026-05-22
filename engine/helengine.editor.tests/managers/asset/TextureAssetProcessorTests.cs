using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies texture asset processor behavior for per-platform texture conversions.
    /// </summary>
    public sealed class TextureAssetProcessorTests {
        /// <summary>
        /// Verifies the texture processor converts RGBA32 source pixels into an indexed DS payload with palette data.
        /// </summary>
        [Fact]
        public void Apply_WhenIndexed4IsRequested_ProducesPaletteAndPackedIndices() {
            TextureAsset source = new TextureAsset {
                Id = "menu/logo",
                Width = 4,
                Height = 1,
                ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8,
                Colors = [
                    255, 0, 0, 255,
                    0, 255, 0, 255,
                    0, 0, 255, 255,
                    0, 0, 0, 0
                ]
            };

            TextureAsset processed = new TextureAssetProcessor().Apply(source, new TextureAssetProcessorSettings {
                ColorFormat = TextureAssetColorFormat.Indexed4,
                AlphaPrecision = TextureAssetAlphaPrecision.Binary,
                MaxResolution = 0
            });

            Assert.Equal(TextureAssetColorFormat.Indexed4, processed.ColorFormat);
            Assert.Equal(TextureAssetAlphaPrecision.Binary, processed.AlphaPrecision);
            Assert.NotNull(processed.PaletteColors);
            Assert.Equal(16, processed.PaletteColors.Length);
            Assert.Equal(2, processed.Colors.Length);
        }

    }
}

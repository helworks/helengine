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
            Assert.Equal(64, processed.PaletteColors.Length);
            Assert.Equal(2, processed.Colors.Length);
        }

        /// <summary>
        /// Verifies indexed8 processing quantizes source images that contain more than 256 unique colors.
        /// </summary>
        [Fact]
        public void Apply_WhenIndexed8QuantizedIsRequestedOnMoreThan256Colors_Succeeds() {
            TextureAsset source = new TextureAsset {
                Id = "ui/logo",
                Width = 17,
                Height = 17,
                ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8,
                Colors = BuildUniqueColors(289)
            };

            TextureAsset processed = new TextureAssetProcessor().Apply(source, new TextureAssetProcessorSettings {
                ColorFormat = TextureAssetColorFormat.Indexed8,
                AlphaPrecision = TextureAssetAlphaPrecision.A8,
                IndexingMethodId = TextureAssetIndexingMethod.QuantizedIndexed.ToString()
            });

            Assert.Equal(TextureAssetColorFormat.Indexed8, processed.ColorFormat);
            Assert.Equal(256 * 4, processed.PaletteColors.Length);
            Assert.Equal(289, processed.Colors.Length);
        }

        /// <summary>
        /// Verifies indexed4 processing quantizes source images that contain more than 16 unique colors.
        /// </summary>
        [Fact]
        public void Apply_WhenIndexed4QuantizedIsRequestedOnMoreThan16Colors_Succeeds() {
            TextureAsset source = new TextureAsset {
                Id = "ui/badge",
                Width = 5,
                Height = 4,
                ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8,
                Colors = BuildUniqueColors(20)
            };

            TextureAsset processed = new TextureAssetProcessor().Apply(source, new TextureAssetProcessorSettings {
                ColorFormat = TextureAssetColorFormat.Indexed4,
                AlphaPrecision = TextureAssetAlphaPrecision.A8,
                IndexingMethodId = TextureAssetIndexingMethod.QuantizedIndexed.ToString()
            });

            Assert.Equal(TextureAssetColorFormat.Indexed4, processed.ColorFormat);
            Assert.Equal(16 * 4, processed.PaletteColors.Length);
            Assert.Equal(10, processed.Colors.Length);
        }

        /// <summary>
        /// Verifies semi-transparent UI edge colors are preserved preferentially when indexed palette capacity is exceeded.
        /// </summary>
        [Fact]
        public void Apply_WhenIndexed4QuantizedIsRequested_PreservesSemiTransparentEdgePaletteEntries() {
            TextureAsset source = new TextureAsset {
                Id = "ui/antialias",
                Width = 18,
                Height = 1,
                ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8,
                Colors = BuildEdgePriorityColors()
            };

            TextureAsset processed = new TextureAssetProcessor().Apply(source, new TextureAssetProcessorSettings {
                ColorFormat = TextureAssetColorFormat.Indexed4,
                AlphaPrecision = TextureAssetAlphaPrecision.A8,
                IndexingMethodId = TextureAssetIndexingMethod.QuantizedIndexed.ToString()
            });

            Assert.Contains(processed.PaletteColors.Chunk(4), color => color[0] == 240 && color[1] == 240 && color[2] == 240 && color[3] == 96);
            Assert.Contains(processed.PaletteColors.Chunk(4), color => color[0] == 250 && color[1] == 250 && color[2] == 250 && color[3] == 64);
        }

        /// <summary>
        /// Builds one RGBA32 texture payload with the requested number of unique colors.
        /// </summary>
        /// <param name="colorCount">Number of unique RGBA entries to emit.</param>
        /// <returns>RGBA32 color payload.</returns>
        byte[] BuildUniqueColors(int colorCount) {
            byte[] colors = new byte[colorCount * 4];
            for (int pixelIndex = 0; pixelIndex < colorCount; pixelIndex++) {
                int colorIndex = pixelIndex * 4;
                colors[colorIndex] = (byte)(pixelIndex & 0xFF);
                colors[colorIndex + 1] = (byte)((255 - pixelIndex) & 0xFF);
                colors[colorIndex + 2] = (byte)((pixelIndex * 37) & 0xFF);
                colors[colorIndex + 3] = 255;
            }

            return colors;
        }

        /// <summary>
        /// Builds one RGBA32 texture payload whose opaque colors exceed indexed4 capacity while preserving two semi-transparent edge colors.
        /// </summary>
        /// <returns>RGBA32 color payload.</returns>
        byte[] BuildEdgePriorityColors() {
            byte[] colors = new byte[18 * 4];
            for (int pixelIndex = 0; pixelIndex < 16; pixelIndex++) {
                int colorIndex = pixelIndex * 4;
                colors[colorIndex] = (byte)(pixelIndex * 8);
                colors[colorIndex + 1] = (byte)(16 + (pixelIndex * 8));
                colors[colorIndex + 2] = (byte)(32 + (pixelIndex * 8));
                colors[colorIndex + 3] = 255;
            }

            colors[64] = 240;
            colors[65] = 240;
            colors[66] = 240;
            colors[67] = 96;
            colors[68] = 250;
            colors[69] = 250;
            colors[70] = 250;
            colors[71] = 64;
            return colors;
        }
    }
}

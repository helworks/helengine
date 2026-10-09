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
        /// Checks known black/white texels against each storage layout, including
        /// the per-row padding of odd-width nibble and YUV textures.
        /// </summary>
        /// <param name="format">Requested generic storage format.</param>
        /// <param name="alpha">Alpha policy supported by that format.</param>
        /// <param name="expectedHex">Independent expected bytes for three identical scanlines.</param>
        [Theory]
        [InlineData(TextureAssetColorFormat.Rgba5551, TextureAssetAlphaPrecision.Binary, "0000FFFF00000000FFFF00000000FFFF0000")]
        [InlineData(TextureAssetColorFormat.Ia4, TextureAssetAlphaPrecision.Binary, "0F000F000F00")]
        [InlineData(TextureAssetColorFormat.Ia8, TextureAssetAlphaPrecision.A4, "00FF0000FF0000FF00")]
        [InlineData(TextureAssetColorFormat.Ia16, TextureAssetAlphaPrecision.A8, "0000FFFF00000000FFFF00000000FFFF0000")]
        [InlineData(TextureAssetColorFormat.I4, TextureAssetAlphaPrecision.A4, "0F000F000F00")]
        [InlineData(TextureAssetColorFormat.I8, TextureAssetAlphaPrecision.A8, "00FF0000FF0000FF00")]
        [InlineData(TextureAssetColorFormat.Yuv16, TextureAssetAlphaPrecision.Opaque, "1080EB80108010801080EB80108010801080EB8010801080")]
        public void Apply_NewFormatsEncodeKnownOddWidthRows(TextureAssetColorFormat format, TextureAssetAlphaPrecision alpha, string expectedHex) {
            TextureAsset source = CreateBlackWhiteRows();
            TextureAsset processed = new TextureAssetProcessor().Apply(source, new TextureAssetProcessorSettings {
                ColorFormat = format,
                AlphaPrecision = alpha
            });

            Assert.Equal(format, processed.ColorFormat);
            Assert.Equal(alpha, processed.AlphaPrecision);
            Assert.Equal(Convert.FromHexString(expectedHex), processed.Colors);
            Assert.Equal(source.Id, processed.Id);
            Assert.Equal(source.RuntimeAssetId, processed.RuntimeAssetId);
            Assert.Equal(source.AuthoringAssetId, processed.AuthoringAssetId);
            Assert.Equal(source.FormerAuthoringAssetIds, processed.FormerAuthoringAssetIds);
            Assert.Equal(source.IsEngineOwned, processed.IsEngineOwned);
            Assert.Equal(3, processed.Width);
            Assert.Equal(3, processed.Height);
        }

        /// <summary>
        /// Resizing a packed source samples decoded texels rather than indexing
        /// compact storage as RGBA bytes, and retains both asset identities.
        /// </summary>
        /// <param name="format">Source packed format to resize.</param>
        /// <param name="alpha">Supported alpha policy used to create that source.</param>
        [Theory]
        [InlineData(TextureAssetColorFormat.Rgba5551, TextureAssetAlphaPrecision.Binary)]
        [InlineData(TextureAssetColorFormat.Ia4, TextureAssetAlphaPrecision.Binary)]
        [InlineData(TextureAssetColorFormat.Ia8, TextureAssetAlphaPrecision.A4)]
        [InlineData(TextureAssetColorFormat.Ia16, TextureAssetAlphaPrecision.A8)]
        [InlineData(TextureAssetColorFormat.I4, TextureAssetAlphaPrecision.A4)]
        [InlineData(TextureAssetColorFormat.I8, TextureAssetAlphaPrecision.A8)]
        [InlineData(TextureAssetColorFormat.Yuv16, TextureAssetAlphaPrecision.Opaque)]
        public void Apply_ResizesPackedSourceUsingDecodedTexels(TextureAssetColorFormat format, TextureAssetAlphaPrecision alpha) {
            TextureAsset source = TextureAssetPixelCodec.EncodeFromRgba32(CreateBlackWhiteRows(), format, alpha);
            TextureAsset resized = new TextureAssetProcessor().Apply(source, new TextureAssetProcessorSettings {
                MaxResolution = 2,
                ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8
            });

            Assert.Equal(2, resized.Width);
            Assert.Equal(2, resized.Height);
            Assert.Equal(source.Id, resized.Id);
            Assert.Equal(source.RuntimeAssetId, resized.RuntimeAssetId);
            Assert.Equal(source.AuthoringAssetId, resized.AuthoringAssetId);
            Assert.Equal(source.FormerAuthoringAssetIds, resized.FormerAuthoringAssetIds);
            Assert.Equal(source.IsEngineOwned, resized.IsEngineOwned);
            Assert.Equal(TextureAssetColorFormat.Rgba32, resized.ColorFormat);
            Assert.Equal(new byte[] {0, 0, 0}, resized.Colors.Take(3));
            Assert.Equal(new byte[] {255, 255, 255}, resized.Colors.Skip(4).Take(3));
            Assert.Equal(resized.Colors.Take(8), resized.Colors.Skip(8));
        }

        /// <summary>
        /// A matching format keeps validated compact bytes unchanged; malformed
        /// payloads and unsupported policies cannot escape through that fast path.
        /// </summary>
        [Fact]
        public void Apply_ValidatesMatchingPackedPayloadBeforeReturningIt() {
            TextureAsset source = TextureAssetPixelCodec.EncodeFromRgba32(CreateBlackWhiteRows(), TextureAssetColorFormat.Ia4, TextureAssetAlphaPrecision.Binary);
            TextureAssetProcessorSettings settings = new TextureAssetProcessorSettings {
                ColorFormat = TextureAssetColorFormat.Ia4,
                AlphaPrecision = TextureAssetAlphaPrecision.Binary
            };
            TextureAssetProcessor processor = new TextureAssetProcessor();
            Assert.Same(source, processor.Apply(source, settings));
            source.Colors = source.Colors[..^1];
            Assert.Throws<ArgumentException>(() => processor.Apply(source, settings));
        }

        /// <summary>
        /// Existing RGBA4444 and indexed payloads also resize through their
        /// established packing conventions, retaining the black/white texels.
        /// </summary>
        /// <param name="format">Previously supported packed or indexed format.</param>
        [Theory]
        [InlineData(TextureAssetColorFormat.Rgba4444)]
        [InlineData(TextureAssetColorFormat.Indexed4)]
        [InlineData(TextureAssetColorFormat.Indexed8)]
        public void Apply_ResizesLegacyPackedAndIndexedSources(TextureAssetColorFormat format) {
            TextureAssetProcessor processor = new TextureAssetProcessor();
            TextureAsset packed = processor.Apply(CreateBlackWhiteRows(), new TextureAssetProcessorSettings {
                ColorFormat = format,
                AlphaPrecision = TextureAssetAlphaPrecision.A8
            });
            TextureAsset resized = processor.Apply(packed, new TextureAssetProcessorSettings {
                ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8,
                MaxResolution = 2
            });

            Assert.Equal(2, resized.Width);
            Assert.Equal(2, resized.Height);
            Assert.Equal(new byte[] {0, 0, 0, 0, 255, 255, 255, 255, 0, 0, 0, 0, 255, 255, 255, 255}, resized.Colors);
        }

        /// <summary>
        /// Packed-to-RGBA conversion without resize passes through the decoded
        /// intermediate and legacy alpha path without losing authoring ownership.
        /// </summary>
        [Fact]
        public void Apply_ConvertsPackedSourceWithoutResizePreservingIdentity() {
            TextureAsset source = TextureAssetPixelCodec.EncodeFromRgba32(CreateBlackWhiteRows(), TextureAssetColorFormat.Ia16, TextureAssetAlphaPrecision.A8);
            byte[] originalPixels = (byte[])source.Colors.Clone();
            TextureAsset converted = new TextureAssetProcessor().Apply(source, new TextureAssetProcessorSettings {
                ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8
            });

            Assert.Equal(source.Id, converted.Id);
            Assert.Equal(source.RuntimeAssetId, converted.RuntimeAssetId);
            Assert.Equal(source.AuthoringAssetId, converted.AuthoringAssetId);
            Assert.Equal(source.FormerAuthoringAssetIds, converted.FormerAuthoringAssetIds);
            Assert.NotSame(source.FormerAuthoringAssetIds, converted.FormerAuthoringAssetIds);
            Assert.Equal(source.IsEngineOwned, converted.IsEngineOwned);
            Assert.Equal(CreateBlackWhiteRows().Colors, converted.Colors);
            Assert.Equal(originalPixels, source.Colors);
        }

        /// <summary>
        /// Generic conversions reject a platform-owned target and an undefined
        /// source format instead of interpreting arbitrary bytes as RGBA pixels.
        /// </summary>
        [Fact]
        public void Apply_RejectsUnknownAndPlatformOwnedFormats() {
            TextureAsset source = CreateBlackWhiteRows();
            Assert.Throws<InvalidOperationException>(() => new TextureAssetProcessor().Apply(source, new TextureAssetProcessorSettings {
                ColorFormatId = "GxRgb5A3",
                AlphaPrecision = TextureAssetAlphaPrecision.A8
            }));
            source.ColorFormat = (TextureAssetColorFormat)255;
            Assert.Throws<NotSupportedException>(() => new TextureAssetProcessor().Apply(source, new TextureAssetProcessorSettings {
                ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8
            }));
        }

        /// <summary>
        /// Creates three odd-width black/white/black rows with corresponding zero
        /// and full alpha, making channel order and padding visible in fixtures.
        /// </summary>
        /// <returns>A source texture with nine independently known RGBA texels.</returns>
        TextureAsset CreateBlackWhiteRows() {
            return new TextureAsset {
                Id = "ui/known-rows",
                RuntimeAssetId = 37,
                AuthoringAssetId = "known-rows-authoring",
                FormerAuthoringAssetIds = ["known-rows-former"],
                IsEngineOwned = true,
                Width = 3,
                Height = 3,
                ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8,
                Colors = [
                    0, 0, 0, 0, 255, 255, 255, 255, 0, 0, 0, 0,
                    0, 0, 0, 0, 255, 255, 255, 255, 0, 0, 0, 0,
                    0, 0, 0, 0, 255, 255, 255, 255, 0, 0, 0, 0
                ]
            };
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

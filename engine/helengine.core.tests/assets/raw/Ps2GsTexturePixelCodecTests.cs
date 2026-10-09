using helengine;

namespace helengine.core.tests.assets.raw {
    /// <summary>Checks compact GS transfers, native alpha conventions and CSM1 palette order against independent fixtures.</summary>
    public sealed class Ps2GsTexturePixelCodecTests {
        /// <summary>Enumerates all native texture and CLUT combinations rather than only the three legacy modes.</summary>
        public static TheoryData<string> Formats {
            get {
                TheoryData<string> result = new();
                foreach (Ps2GsTextureFormatDefinition format in Ps2GsTextureFormatCatalog.Formats) result.Add(format.Id);
                return result;
            }
        }
        /// <summary>Verifies thirteen GS codes, twenty-three selections and the preserved platform enum mapping.</summary>
        [Fact]
        public void Catalog_CoversAllNativeModesAndThreeIndexedCluts() {
            Assert.Equal(23, Ps2GsTextureFormatCatalog.Formats.Length);
            Assert.Equal(new[] { 0, 1, 2, 10, 19, 20, 27, 36, 44, 48, 49, 50, 58 }, Ps2GsTextureFormatCatalog.Formats.Select(value => value.FormatCode).Distinct().OrderBy(value => value));
            foreach (int code in new[] { 19, 20, 27, 36, 44 }) Assert.Equal(new[] { 0, 2, 10 }, Ps2GsTextureFormatCatalog.Formats.Where(value => value.FormatCode == code).Select(value => value.ClutFormatCode));
            Assert.True(Ps2GsTextureFormatCatalog.TryGetPlatformFormat(0, 0, out Ps2GsTextureFormatDefinition rgba)); Assert.Equal("PS2.PSMCT32", rgba.Id);
            Assert.True(Ps2GsTextureFormatCatalog.TryGetPlatformFormat(1, 0, out Ps2GsTextureFormatDefinition index8)); Assert.Equal("PS2.PSMT8", index8.Id);
            Assert.True(Ps2GsTextureFormatCatalog.TryGetPlatformFormat(2, 0, out Ps2GsTextureFormatDefinition index4)); Assert.Equal("PS2.PSMT4", index4.Id);
            Assert.False(Ps2GsTextureFormatCatalog.TryGetFormat("PS2.DXT1", out _));
            Assert.All(Ps2GsTextureFormatCatalog.Formats, value => Assert.False(value.SupportsAlpha(TextureAssetAlphaPrecision.A2)));
        }
        /// <summary>Exercises every actual compact representation while preserving source-owned buffers.</summary>
        [Theory]
        [MemberData(nameof(Formats))]
        public void Encode_All23CombinationsHaveExactOwnedBuffers(string id) {
            Assert.True(Ps2GsTextureFormatCatalog.TryGetFormat(id, out Ps2GsTextureFormatDefinition format));
            using TextureAsset source = format.IsIndexed ? IndexedSource() : RgbaSource();
            byte[] original = source.Colors.ToArray();
            byte[] pixels = Ps2GsTexturePixelCodec.EncodePixels(source, id, format.DefaultAlphaPrecision);
            byte[] palette = Ps2GsTexturePixelCodec.EncodePalette(source, id, format.DefaultAlphaPrecision);
            Assert.Equal(format.GetPixelByteLength(3, 2), pixels.Length);
            Assert.Equal(format.PaletteByteLength, palette.Length);
            Assert.Equal(24, Ps2GsTexturePixelCodec.DecodeToRgba32(pixels, palette, 3, 2, id).Length);
            Assert.Equal(original, source.Colors);
            Assert.NotSame(source.Colors, pixels);
            Assert.Equal(8192 + (format.IsIndexed ? 8192 : 0), format.GetVramByteLength(3, 2));
        }
        /// <summary>Checks independently authored color words, including the raw color interpretation of depth PSMs.</summary>
        [Theory]
        [InlineData("PSMCT32", "12345640", 18, 52, 86, 128)]
        [InlineData("PSMZ32", "12345640", 18, 52, 86, 128)]
        [InlineData("PSMCT24", "123456", 18, 52, 86, 255)]
        [InlineData("PSMZ24", "123456", 18, 52, 86, 255)]
        [InlineData("PSMCT16", "1F80", 248, 0, 0, 255)]
        [InlineData("PSMCT16S", "1F00", 248, 0, 0, 0)]
        [InlineData("PSMZ16", "0084", 0, 0, 8, 255)]
        [InlineData("PSMZ16S", "E083", 0, 248, 0, 255)]
        public void Decode_IndependentGsWordsMatchActualSampledChannels(string name, string hex, byte red, byte green, byte blue, byte alpha) {
            Assert.Equal(new byte[] { red, green, blue, alpha }, Ps2GsTexturePixelCodec.DecodeToRgba32(Convert.FromHexString(hex), null, 1, 1, "PS2." + name));
        }
        /// <summary>Checks encoded RGB byte order, compact twenty-four-bit size and native zero-through-128 blending alpha.</summary>
        [Theory]
        [InlineData("PSMCT32", "12345640")]
        [InlineData("PSMZ32", "12345640")]
        [InlineData("PSMCT24", "123456")]
        [InlineData("PSMZ24", "123456")]
        public void Encode_ColorAndDepthWordsHaveIndependentGoldenBytes(string name, string hex) {
            using TextureAsset source = new() { Width = 1, Height = 1, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = new byte[] { 0x12, 0x34, 0x56, 128 } };
            Assert.True(Ps2GsTextureFormatCatalog.TryGetFormat("PS2." + name, out Ps2GsTextureFormatDefinition format));
            Assert.Equal(Convert.FromHexString(hex), Ps2GsTexturePixelCodec.EncodePixels(source, format.Id, format.DefaultAlphaPrecision));
        }
        /// <summary>Proves four-bit transfers remain continuous across odd-width rows and high-index modes retain compact host streams.</summary>
        [Theory]
        [InlineData("PSMT4")]
        [InlineData("PSMT4HL")]
        [InlineData("PSMT4HH")]
        public void Encode_FourBitTransfersUseLowNibblesAcrossOddRows(string name) {
            using TextureAsset source = IndexedSource();
            Assert.Equal(new byte[] { 0x10, 0x32, 0x54 }, Ps2GsTexturePixelCodec.EncodePixels(source, "PS2." + name, TextureAssetAlphaPrecision.A8));
        }
        /// <summary>Checks actual CSM1 host-coordinate entry eight/sixteen swaps for every palette component storage mode.</summary>
        [Theory]
        [InlineData("PS2.PSMT8", 4)]
        [InlineData("PS2.PSMT8.CLUT16", 2)]
        [InlineData("PS2.PSMT8.CLUT16S", 2)]
        public void Encode_Clut256UsesIndependentCsm1SwapFixture(string id, int entryBytes) {
            using TextureAsset source = new() { Width = 2, Height = 1, ColorFormat = TextureAssetColorFormat.Indexed8, AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = new byte[] { 8, 16 }, PaletteColors = new byte[17 * 4] };
            source.PaletteColors[8 * 4] = 248; source.PaletteColors[8 * 4 + 3] = 255;
            source.PaletteColors[16 * 4 + 1] = 248; source.PaletteColors[16 * 4 + 3] = 255;
            Assert.True(Ps2GsTextureFormatCatalog.TryGetFormat(id, out Ps2GsTextureFormatDefinition format));
            byte[] palette = Ps2GsTexturePixelCodec.EncodePalette(source, id, format.DefaultAlphaPrecision);
            Assert.Equal(entryBytes * 256, palette.Length);
            Assert.Equal(entryBytes == 4 ? new byte[] { 248, 0, 0, 128 } : new byte[] { 31, 128 }, palette.Skip(16 * entryBytes).Take(entryBytes));
            Assert.Equal(entryBytes == 4 ? new byte[] { 0, 248, 0, 128 } : new byte[] { 224, 131 }, palette.Skip(8 * entryBytes).Take(entryBytes));
            byte[] pixels = Ps2GsTexturePixelCodec.EncodePixels(source, id, format.DefaultAlphaPrecision);
            Assert.Equal(new byte[] { 248, 0, 0, 255, 0, 248, 0, 255 }, Ps2GsTexturePixelCodec.DecodeToRgba32(pixels, palette, 2, 1, id));
        }
        /// <summary>Distinguishes compact CPU length from GS full-page allocation for direct, indexed and high-index PSMs.</summary>
        [Theory]
        [InlineData("PS2.PSMCT24", 3, 64, 32, 8192)]
        [InlineData("PS2.PSMCT16S", 2, 64, 64, 8192)]
        [InlineData("PS2.PSMT8", 1, 128, 64, 16384)]
        [InlineData("PS2.PSMT8H", 1, 64, 32, 16384)]
        [InlineData("PS2.PSMT4HH", 1, 64, 32, 16384)]
        public void Layout_TransferAndVramSizesUseTheirActualUnits(string id, int singlePixelBytes, int pageWidth, int pageHeight, int total) {
            Assert.True(Ps2GsTextureFormatCatalog.TryGetFormat(id, out Ps2GsTextureFormatDefinition format));
            Assert.Equal(singlePixelBytes, format.GetPixelByteLength(1, 1)); Assert.Equal(pageWidth, format.PageWidth); Assert.Equal(pageHeight, format.PageHeight);
            Assert.Equal(total, format.GetVramByteLength(1, 1)); Assert.Equal(16384, format.GetTextureVramByteLength(pageWidth + 1, pageHeight));
            Assert.Throws<ArgumentOutOfRangeException>(() => format.GetPixelByteLength(1025, 1));
        }
        /// <summary>Rejects malformed payload lengths, incompatible alpha policies and unquantized indexed input.</summary>
        [Fact]
        public void Codec_RejectsInvalidNativeInputs() {
            using TextureAsset source = RgbaSource();
            Assert.Throws<ArgumentException>(() => Ps2GsTexturePixelCodec.EncodePixels(source, "PS2.PSMT8", TextureAssetAlphaPrecision.A8));
            Assert.Throws<ArgumentException>(() => Ps2GsTexturePixelCodec.EncodePixels(source, "PS2.PSMCT16", TextureAssetAlphaPrecision.A8));
            Assert.Throws<ArgumentException>(() => Ps2GsTexturePixelCodec.DecodeToRgba32(new byte[4], null, 1, 1, "PS2.PSMCT24"));
            Assert.Throws<ArgumentException>(() => Ps2GsTexturePixelCodec.DecodeToRgba32(new byte[1], new byte[4], 1, 1, "PS2.PSMT8"));
            using TextureAsset indexed = IndexedSource(); indexed.Colors[0] = 0xff;
            Assert.Throws<ArgumentException>(() => Ps2GsTexturePixelCodec.EncodePixels(indexed, "PS2.PSMT4", TextureAssetAlphaPrecision.A8));
        }
        /// <summary>Rejects public descriptions that would otherwise invent a hardware mode or incompatible palette layout.</summary>
        [Theory]
        [InlineData(63, 0, 0)] [InlineData(2, 0, 0)] [InlineData(0, 0, 2)] [InlineData(19, 1, 1)]
        public void Definition_RejectsUnknownOrInconsistentHardware(int code, int platform, int clut) {
            Assert.Throws<ArgumentException>(() => new Ps2GsTextureFormatDefinition("invalid", code, platform, clut));
        }
        /// <summary>Releases directly constructed policy storage exactly once without invalidating the static catalog.</summary>
        [Fact]
        public void Definition_DisposeReleasesItsOwnedPolicies() {
            using Ps2GsTextureFormatDefinition definition = new("test", 0, 0, 0);
            definition.Dispose(); definition.Dispose(); Assert.Null(definition.SupportedAlphaPrecisions);
            Assert.False(definition.SupportsAlpha(TextureAssetAlphaPrecision.A8));
            Assert.True(Ps2GsTextureFormatCatalog.TryGetFormat("PS2.PSMCT32", out Ps2GsTextureFormatDefinition permanent)); Assert.True(permanent.SupportsAlpha(TextureAssetAlphaPrecision.A8));
        }
        /// <summary>Creates six authored RGBA texels covering transparent, intermediate and opaque channels.</summary>
        static TextureAsset RgbaSource() {
            return new TextureAsset { Width = 3, Height = 2, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8,
                Colors = new byte[] { 255, 0, 0, 255, 0, 255, 0, 128, 0, 0, 255, 0, 255, 255, 255, 255, 0, 0, 0, 0, 128, 128, 128, 128 } };
        }
        /// <summary>Creates the continuous generic low-nibble-first sequence zero through five with six logical colors.</summary>
        static TextureAsset IndexedSource() {
            return new TextureAsset { Width = 3, Height = 2, ColorFormat = TextureAssetColorFormat.Indexed4, AlphaPrecision = TextureAssetAlphaPrecision.A8,
                Colors = new byte[] { 0x10, 0x32, 0x54 }, PaletteColors = new byte[] { 255, 0, 0, 255, 0, 255, 0, 128, 0, 0, 255, 0, 255, 255, 255, 255, 0, 0, 0, 0, 128, 128, 128, 128 } };
        }
    }
}

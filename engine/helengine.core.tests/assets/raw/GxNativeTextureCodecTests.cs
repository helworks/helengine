using helengine;
using System.Buffers.Binary;

namespace helengine.core.tests.assets.raw {
    /// <summary>Checks native GX tile layouts and sampler semantics against independent GPU byte fixtures.</summary>
    public sealed class GxNativeTextureCodecTests {
        /// <summary>Enumerates every native format and TLUT selection.</summary>
        public static TheoryData<string> Formats {
            get {
                TheoryData<string> result = new();
                foreach (GxNativeTextureFormatDefinition format in GxNativeTextureFormatCatalog.Formats) result.Add(format.Id);
                return result;
            }
        }
        /// <summary>Verifies all samplers and depth aliases while excluding EFB copy-only conversion codes.</summary>
        [Fact]
        public void Catalog_Has20SelectionsAndPreservesExistingTags() {
            Assert.Equal(20, GxNativeTextureFormatCatalog.Formats.Length);
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 8, 9, 10, 14, 17, 19, 22 }, GxNativeTextureFormatCatalog.Formats.Select(value => value.NativeFormat).Distinct().OrderBy(value => value));
            Assert.Equal(4, (int)TextureAssetColorFormat.GxRgb5A3);
            Assert.Equal(15, (int)TextureAssetColorFormat.PspNative);
            Assert.Equal(16, (int)TextureAssetColorFormat.Nintendo3DsNative);
            Assert.Equal(17, (int)TextureAssetColorFormat.GxNative);
            foreach (int code in new[] { 8, 9, 10 }) Assert.Equal(new[] { 0, 1, 2 }, GxNativeTextureFormatCatalog.Formats.Where(value => value.NativeFormat == code).Select(value => value.TlutFormat));
            foreach (int code in new[] { 7, 11, 12, 13, 0x20, 0x22, 0x23, 0x26, 0x27, 0x28, 0x29, 0x2a, 0x2b, 0x2c, 0x30, 0x39, 0x3a, 0x3c }) Assert.False(GxNativeTextureFormatCatalog.TryGetHardwareFormat(code, -1, out _));
        }
        /// <summary>Exercises every compact native representation with odd dimensions and unchanged source buffers.</summary>
        [Theory]
        [MemberData(nameof(Formats))]
        public void Encode_All20SelectionsProduceCanonicalOwnedPayloads(string id) {
            Assert.True(GxNativeTextureFormatCatalog.TryGetFormat(id, out GxNativeTextureFormatDefinition format));
            using TextureAsset source = Source(format.IsIndexed);
            byte[] original = source.Colors.ToArray();
            using TextureAsset encoded = GxNativeTextureCodec.Encode(source, id, format.DefaultAlphaPrecision);
            GxNativeTextureLayout layout = GxNativeTextureCodec.ReadLayout(encoded);
            Assert.Equal(format.NativeFormat, layout.Format.NativeFormat);
            Assert.Equal(format.GetPixelByteLength(3, 2) + 48, encoded.Colors.Length);
            Assert.Equal(original, source.Colors);
            Assert.NotSame(source.Colors, encoded.Colors);
            Assert.Equal(24, TextureAssetPixelCodec.DecodeToRgba32(encoded).Length);
            Assert.Equal("gx-fixture", encoded.Id);
            Assert.Equal(source.FormerAuthoringAssetIds, encoded.FormerAuthoringAssetIds);
            Assert.NotSame(source.FormerAuthoringAssetIds, encoded.FormerAuthoringAssetIds);
            Assert.Equal(format.IsIndexed ? 16 : 0, layout.PaletteEntryCount);
            Assert.Equal(format.IsIndexed ? 32 : 0, layout.PaletteLength);
            if (!format.IsIndexed) Assert.Null(encoded.PaletteColors);
        }
        /// <summary>Uses hand-authored bytes to verify nibble order, IA byte order, RGB words and planar RGBA8.</summary>
        [Theory]
        [InlineData(0, 2, "ab", "aaaaaaaa")]
        [InlineData(1, 3, "83", "83838383")]
        [InlineData(2, 2, "a3", "333333aa")]
        [InlineData(3, 3, "a321", "212121a3")]
        [InlineData(4, 0, "f800", "ff0000ff")]
        [InlineData(5, 5, "3f00", "ff00006d")]
        [InlineData(5, 5, "83e0", "00ff00ff")]
        public void Decode_IndependentColorFixtures(int code, int alpha, string bytes, string expected) {
            using TextureAsset asset = Fixture(code, alpha, Convert.FromHexString(bytes));
            Assert.Equal(Convert.FromHexString(expected), GxNativeTextureCodec.Decode(asset));
        }
        /// <summary>Checks the independent alpha/red and green/blue planes in a sixty-four-byte tile.</summary>
        [Fact]
        public void Decode_Rgba8UsesSeparateArAndGbPlanes() {
            byte[] tile = new byte[64]; tile[0] = 0x44; tile[1] = 0x11; tile[32] = 0x22; tile[33] = 0x33;
            using TextureAsset asset = Fixture(6, 3, tile);
            Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44 }, GxNativeTextureCodec.Decode(asset));
        }
        /// <summary>Checks that depth aliases preserve raw source R, RG and RGB bytes with their actual sampler alpha.</summary>
        [Theory]
        [InlineData("Gx.Z8", 3, "12121212")]
        [InlineData("Gx.Z16", 3, "34343412")]
        [InlineData("Gx.Z24X8", 0, "123456ff")]
        public void Encode_DepthViewsPreserveRawChannels(string id, int alpha, string expected) {
            using TextureAsset source = new() { Width = 1, Height = 1, Colors = new byte[] { 0x12, 0x34, 0x56, 0x78 }, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8 };
            using TextureAsset encoded = GxNativeTextureCodec.Encode(source, id, (TextureAssetAlphaPrecision)alpha);
            Assert.True(GxNativeTextureCodec.ReadLayout(encoded).Format.IsDepthView);
            Assert.Equal(Convert.FromHexString(expected), GxNativeTextureCodec.Decode(encoded));
        }
        /// <summary>Verifies GX three-eighths interpolation and transparent average color rather than PC BC1 semantics.</summary>
        [Fact]
        public void Decode_CmprUsesGxInterpolationAndTransparentRgb() {
            byte[] tile = new byte[32]; tile[0] = 0xf8; tile[1] = 0; tile[2] = 0; tile[3] = 0x1f; tile[4] = 0x80;
            using TextureAsset asset = Fixture(14, 1, tile);
            Assert.Equal(new byte[] { 159, 0, 95, 255 }, GxNativeTextureCodec.Decode(asset));
            asset.Colors[48] = 0; asset.Colors[49] = 0x1f; asset.Colors[50] = 0xf8; asset.Colors[51] = 0; asset.Colors[52] = 0xc0;
            Assert.Equal(new byte[] { 127, 0, 127, 0 }, GxNativeTextureCodec.Decode(asset));
        }
        /// <summary>Checks CMPR subblock order and high-first two-bit selectors using four authored endpoint colors.</summary>
        [Fact]
        public void Decode_CmprSubblocksAreTopLeftTopRightBottomLeftBottomRight() {
            byte[] tile = new byte[32];
            foreach (int block in Enumerable.Range(0, 4)) { int word = new[] { 0xf800, 0x07e0, 0x001f, 0xffff }[block]; tile[block * 8] = (byte)(word >> 8); tile[block * 8 + 1] = (byte)word; }
            using TextureAsset asset = Fixture(14, 1, tile, 8, 8);
            byte[] decoded = GxNativeTextureCodec.Decode(asset);
            Assert.Equal(new byte[] { 255, 0, 0, 255 }, decoded[0..4]);
            Assert.Equal(new byte[] { 0, 255, 0, 255 }, decoded[16..20]);
            Assert.Equal(new byte[] { 0, 0, 255, 255 }, decoded[128..132]);
            Assert.Equal(new byte[] { 255, 255, 255, 255 }, decoded[144..148]);
        }
        /// <summary>Exercises the complete CI14X2 range, retaining index 16383 and a full 16384-entry TLUT.</summary>
        [Fact]
        public void EncodeIndexed_Ci14RetainsFullFourteenBitIndices() {
            using TextureAsset identity = new() { Width = 2, Height = 1 };
            byte[] palette = new byte[16384 * 4]; palette[16383 * 4] = 255; palette[16383 * 4 + 3] = 255;
            using TextureAsset asset = GxNativeTextureCodec.EncodeIndexed(identity, new ushort[] { 256, 16383 }, palette, "Gx.CI14X2.TLUT.RGB565", TextureAssetAlphaPrecision.Opaque);
            Assert.Equal(new byte[] { 1, 0, 0x3f, 0xff }, asset.Colors[48..52]);
            Assert.Equal(32768, asset.PaletteColors.Length);
            Assert.Equal(16384, GxNativeTextureCodec.ReadLayout(asset).PaletteEntryCount);
            Assert.Equal(new byte[] { 255, 0, 0, 255 }, GxNativeTextureCodec.Decode(asset)[4..8]);
            Assert.Equal(32 + 32768, GxNativeTextureCodec.ReadLayout(asset).Format.GetVramByteLength(2, 1));
        }
        /// <summary>Checks generic low-first nibble input becomes native high-first nibble tiles and clamps odd-row edges.</summary>
        [Fact]
        public void EncodeIndexed_Ci4ReordersNibblesAndClampsEdges() {
            using TextureAsset source = new() { Width = 3, Height = 2, ColorFormat = TextureAssetColorFormat.Indexed4, AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = new byte[] { 0x10, 0x02, 0x21 }, PaletteColors = new byte[] { 0, 0, 0, 255, 127, 127, 127, 85, 255, 255, 255, 170 } };
            using TextureAsset asset = GxNativeTextureCodec.Encode(source, "Gx.CI4.TLUT.IA8", TextureAssetAlphaPrecision.A8);
            Assert.Equal(new byte[] { 0x01, 0x22, 0x22, 0x22, 0x01, 0x22, 0x22, 0x22 }, asset.Colors[48..56]);
            Assert.Equal(new byte[] { 255, 0, 85, 127, 170, 255 }, asset.PaletteColors[0..6]);
            Assert.All(asset.PaletteColors[6..], value => Assert.Equal(0, value));
        }
        /// <summary>Checks tile row-major placement across both axes and repeated edge pixels in the final tile.</summary>
        [Fact]
        public void Encode_Rgba8TilesClampAcrossBothAxes() {
            using TextureAsset source = new() { Width = 5, Height = 5, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = Enumerable.Range(0, 25).SelectMany(value => new byte[] { (byte)value, 0, 0, 255 }).ToArray() };
            using TextureAsset asset = GxNativeTextureCodec.Encode(source, "Gx.RGBA8", TextureAssetAlphaPrecision.A8);
            Assert.Equal(304, asset.Colors.Length);
            Assert.Equal(0, asset.Colors[49]); Assert.Equal(4, asset.Colors[113]); Assert.Equal(20, asset.Colors[177]); Assert.Equal(24, asset.Colors[241]);
            Assert.Equal(24, asset.Colors[271]);
            Assert.Equal(source.Colors, GxNativeTextureCodec.Decode(asset));
        }
        /// <summary>Verifies all eight alpha codes are reachable and match hardware bit replication.</summary>
        [Fact]
        public void Encode_Rgb5A3PreservesAllEightAlphaLevels() {
            int[] levels = { 0, 36, 73, 109, 146, 182, 219, 255 };
            using TextureAsset source = new() { Width = 8, Height = 1, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = levels.SelectMany(value => new byte[] { 255, 0, 0, (byte)value }).ToArray() };
            using TextureAsset asset = GxNativeTextureCodec.Encode(source, "Gx.RGB5A3", TextureAssetAlphaPrecision.A3);
            byte[] decoded = GxNativeTextureCodec.Decode(asset);
            Assert.Equal(levels, Enumerable.Range(0, 8).Select(value => (int)decoded[value * 4 + 3]));
            Assert.Equal(new byte[] { 0x0f, 0, 0x1f, 0, 0x2f, 0, 0x3f, 0 }, asset.Colors[48..56]);
            Assert.False(GxNativeTextureCodec.ReadLayout(asset).Format.SupportsAlpha(TextureAssetAlphaPrecision.A8));
            Assert.False(GxNativeTextureCodec.ReadLayout(asset).Format.SupportsAlpha(TextureAssetAlphaPrecision.A2));
        }
        /// <summary>Rejects altered descriptor words instead of accepting contradictory or unsupported metadata.</summary>
        [Theory]
        [InlineData(0, 0)] [InlineData(4, 2)] [InlineData(8, 7)] [InlineData(12, 2)] [InlineData(16, 2)] [InlineData(20, 1)]
        [InlineData(24, 0)] [InlineData(28, 16)] [InlineData(32, 2)] [InlineData(36, 0)] [InlineData(40, 1)] [InlineData(44, 1)]
        public void ReadLayout_RejectsMalformedHeader(int offset, uint value) {
            using TextureAsset asset = Fixture(1, 3, new byte[] { 127 });
            BinaryPrimitives.WriteUInt32LittleEndian(asset.Colors.AsSpan(offset, 4), value);
            Assert.Throws<ArgumentException>(() => GxNativeTextureCodec.ReadLayout(asset));
        }
        /// <summary>Rejects reserved CI14 bits and out-of-range indices in invisible padded tiles too.</summary>
        [Theory]
        [InlineData(0x40, 0)] [InlineData(0, 16)]
        public void ReadLayout_ValidatesEveryPaddedCi14Index(byte high, byte low) {
            using TextureAsset identity = new() { Width = 1, Height = 1 };
            using TextureAsset asset = GxNativeTextureCodec.EncodeIndexed(identity, new ushort[] { 0 }, new byte[] { 0, 0, 0, 255 }, "Gx.CI14X2.TLUT.IA8", TextureAssetAlphaPrecision.A8);
            asset.Colors[^2] = high; asset.Colors[^1] = low;
            Assert.Throws<ArgumentException>(() => GxNativeTextureCodec.ReadLayout(asset));
        }
        /// <summary>Rejects invalid native dimensions, mismatched palettes and unsupported intrinsic alpha policies.</summary>
        [Fact]
        public void Encode_RejectsInvalidInputsBeforePacking() {
            using TextureAsset source = Source(false);
            Assert.Throws<ArgumentException>(() => GxNativeTextureCodec.Encode(source, "Gx.I4", TextureAssetAlphaPrecision.Opaque));
            Assert.Throws<ArgumentException>(() => GxNativeTextureCodec.Encode(source, "Gx.CI8.TLUT.IA8", TextureAssetAlphaPrecision.A8));
            Assert.Throws<ArgumentException>(() => GxNativeTextureCodec.EncodeIndexed(source, new ushort[6], new byte[17 * 4], "Gx.CI4.TLUT.IA8", TextureAssetAlphaPrecision.A8));
            source.Width = 1025;
            Assert.Throws<ArgumentOutOfRangeException>(() => GxNativeTextureCodec.Encode(source, "Gx.RGBA8", TextureAssetAlphaPrecision.A8));
            Assert.Throws<ArgumentException>(() => new GxNativeTextureFormatDefinition("invalid", 7, -1));
            Assert.Throws<ArgumentException>(() => new GxNativeTextureFormatDefinition("invalid", 0, 0));
            Assert.Throws<ArgumentException>(() => new GxNativeTextureFormatDefinition("invalid", 8, -1));
        }
        /// <summary>Constructs a small independent generic source with stable asset identities.</summary>
        static TextureAsset Source(bool indexed) {
            return new TextureAsset { Id = "gx-fixture", Width = 3, Height = 2, FormerAuthoringAssetIds = new[] { "former" }, ColorFormat = indexed ? TextureAssetColorFormat.Indexed8 : TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8,
                Colors = indexed ? new byte[] { 0, 1, 2, 2, 1, 0 } : new byte[] { 255, 0, 0, 0, 0, 255, 0, 85, 0, 0, 255, 170, 255, 255, 255, 255, 0, 0, 0, 255, 128, 128, 128, 127 },
                PaletteColors = indexed ? new byte[] { 255, 0, 0, 0, 0, 255, 0, 85, 0, 0, 255, 170 } : null };
        }
        /// <summary>Creates a native record from hand-authored hardware bytes without using the encoder.</summary>
        static TextureAsset Fixture(int code, int alpha, byte[] prefix, ushort width = 1, ushort height = 1) {
            Assert.True(GxNativeTextureFormatCatalog.TryGetHardwareFormat(code, -1, out GxNativeTextureFormatDefinition format));
            byte[] colors = new byte[48 + format.GetPixelByteLength(width, height)];
            uint[] words = { 0x314e5847, 1, (uint)code, width, height, (uint)(colors.Length - 48), uint.MaxValue, 0, 0, (uint)alpha, 0, 0 };
            for (int word = 0; word < words.Length; word++) BinaryPrimitives.WriteUInt32LittleEndian(colors.AsSpan(word * 4, 4), words[word]);
            prefix.CopyTo(colors, 48);
            return new TextureAsset { Width = width, Height = height, ColorFormat = TextureAssetColorFormat.GxNative, AlphaPrecision = (TextureAssetAlphaPrecision)alpha, Colors = colors };
        }
    }
}

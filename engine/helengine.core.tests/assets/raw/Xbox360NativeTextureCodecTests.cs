using helengine;

namespace helengine.core.tests.assets.raw {
    /// <summary>Checks Xenos layout and sampling against independent byte fixtures rather than encoder roundtrips alone.</summary>
    public sealed class Xbox360NativeTextureCodecTests {
        /// <summary>Enumerates all implemented hardware codes, including aliases with distinct fetch identities.</summary>
        public static TheoryData<string> Formats {
            get {
                TheoryData<string> result = new();
                foreach (Xbox360NativeTextureFormatDefinition format in Xbox360NativeTextureFormatCatalog.SupportedFormats) result.Add(format.Id);
                return result;
            }
        }
        /// <summary>Separates every known code from actual cooking and pinned emulator loader support.</summary>
        [Fact]
        public void Catalog_All64CodesHaveHonestCapabilities() {
            Assert.Equal(Enumerable.Range(0, 64), Xbox360NativeTextureFormatCatalog.Formats.Select(value => value.HardwareFormat));
            Assert.Equal(44, Xbox360NativeTextureFormatCatalog.SupportedFormats.Length);
            Assert.Equal(42, Xbox360NativeTextureFormatCatalog.Formats.Count(value => value.IsSupportedByXenia));
            Assert.Equal(14, (int)TextureAssetColorFormat.Xbox360Native);
            Assert.Equal(13, (int)TextureAssetColorFormat.XboxNative);
            foreach (Xbox360NativeTextureFormatDefinition format in Xbox360NativeTextureFormatCatalog.Formats.Where(value => !value.SupportsCooking)) {
                Assert.NotEmpty(format.UnsupportedReason);
                using TextureAsset source = Pixel(1, 2, 3, 4);
                Assert.Throws<ArgumentException>(() => Xbox360NativeTextureCodec.Encode(source, format.Id, TextureAssetAlphaPrecision.Opaque));
            }
        }
        /// <summary>Exercises every published code without mutating source pixels or identity buffers.</summary>
        [Theory]
        [MemberData(nameof(Formats))]
        public void Encode_All44CodesOwnTheirCanonicalGpuStorage(string id) {
            Assert.True(Xbox360NativeTextureFormatCatalog.TryGetFormat(id, out Xbox360NativeTextureFormatDefinition format));
            using TextureAsset source = new() {
                Id = "native-source", RuntimeAssetId = 19, AuthoringAssetId = "authored", FormerAuthoringAssetIds = new[] { "previous" },
                Width = 3, Height = 2, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8,
                Colors = new byte[] { 255, 0, 0, 255, 0, 255, 0, 128, 0, 0, 255, 0, 255, 255, 255, 255, 0, 0, 0, 0, 128, 128, 128, 128 }
            };
            byte[] original = source.Colors.ToArray();
            using TextureAsset native = Xbox360NativeTextureCodec.Encode(source, id, format.DefaultAlphaPrecision);
            Xbox360NativeTextureLayout layout = Xbox360NativeTextureCodec.ReadLayout(native);
            Assert.Equal(0, layout.StorageWidth % (32 * format.BlockWidth));
            Assert.Equal(0, layout.PitchBytes % 256);
            Assert.Equal(layout.PitchBytes * (layout.StorageHeight / format.BlockHeight), layout.TexelLength);
            Assert.Equal(56 + layout.TexelLength, native.Colors.Length);
            Assert.Equal(new byte[] { 0x58, 0x33, 0x54, 0x31 }, native.Colors.Take(4));
            Assert.Equal((uint)format.HardwareFormat, Word(native, 8));
            Assert.Equal((uint)format.Swizzle, Word(native, 40));
            Assert.Equal(original, source.Colors);
            Assert.Equal(source.FormerAuthoringAssetIds, native.FormerAuthoringAssetIds);
            Assert.NotSame(source.FormerAuthoringAssetIds, native.FormerAuthoringAssetIds);
            Assert.Null(native.PaletteColors);
            Assert.Equal(24, TextureAssetPixelCodec.DecodeToRgba32(native).Length);
        }
        /// <summary>Verifies compact native component bytes and the exact float-depth white fixture.</summary>
        [Theory]
        [InlineData("8_8_8_8", "12345678", 18, 52, 86, 120)]
        [InlineData("1_5_5_5", "00FC", 0, 0, 255, 255)]
        [InlineData("5_6_5", "1F00", 255, 0, 0, 255)]
        [InlineData("6_5_5", "00FC", 0, 0, 255, 255)]
        [InlineData("2_10_10_10", "FF0300C0", 255, 0, 0, 255)]
        [InlineData("4_4_4_4", "0FF0", 255, 0, 0, 255)]
        [InlineData("10_11_11", "FF070000", 255, 0, 0, 255)]
        [InlineData("11_11_10", "0000E0FF", 0, 0, 255, 255)]
        [InlineData("16", "8080", 128, 128, 128, 255)]
        [InlineData("16_16", "FFFF0000", 255, 0, 0, 255)]
        [InlineData("16_16_16_16", "FFFF000000008080", 255, 0, 0, 128)]
        [InlineData("24_8", "00FFFFFF", 255, 255, 255, 255)]
        [InlineData("24_8_FLOAT", "000000F0", 255, 255, 255, 255)]
        [InlineData("16_FLOAT", "003C", 255, 255, 255, 255)]
        [InlineData("32_FLOAT", "0000803F", 255, 255, 255, 255)]
        public void Decode_IndependentComponentFixtureMatchesCanonicalSwizzle(string name, string hex, byte red, byte green, byte blue, byte alpha) {
            using TextureAsset source = Pixel(0, 0, 0, 255);
            Assert.True(Xbox360NativeTextureFormatCatalog.TryGetFormat("Xbox360.Linear." + name, out Xbox360NativeTextureFormatDefinition format));
            using TextureAsset native = Xbox360NativeTextureCodec.Encode(source, format.Id, format.DefaultAlphaPrecision);
            Convert.FromHexString(hex).CopyTo(native.Colors, 56);
            Assert.Equal(new byte[] { red, green, blue, alpha }, Xbox360NativeTextureCodec.Decode(native));
        }
        /// <summary>Checks encoded normalized white never overflows depth24 and uses the documented float exponent biases.</summary>
        [Theory]
        [InlineData("24_8", "00FFFFFF")]
        [InlineData("24_8_FLOAT", "000000F0")]
        [InlineData("16_FLOAT", "003C")]
        [InlineData("32_FLOAT", "0000803F")]
        public void Encode_NormalizedWhiteHasIndependentGoldenBytes(string name, string hex) {
            using TextureAsset source = Pixel(255, 255, 255, 255);
            using TextureAsset native = Xbox360NativeTextureCodec.Encode(source, "Xbox360.Linear." + name, TextureAssetAlphaPrecision.Opaque);
            byte[] expected = Convert.FromHexString(hex);
            Assert.Equal(expected, native.Colors.Skip(56).Take(expected.Length));
            Assert.Equal(source.Colors, Xbox360NativeTextureCodec.Decode(native));
        }
        /// <summary>Checks replicated green pairs are RGB storage rather than a YUV matrix conversion.</summary>
        [Theory]
        [InlineData("Cr_Y1_Cb_Y0_REP", "20406080")]
        [InlineData("Y1_Cr_Y0_Cb_REP", "40208060")]
        public void Decode_PairFormatsShareRedBlueAndRetainGreen(string name, string hex) {
            using TextureAsset source = new() { Width = 2, Height = 1, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.Opaque, Colors = new byte[8] };
            using TextureAsset native = Xbox360NativeTextureCodec.Encode(source, "Xbox360.Linear." + name, TextureAssetAlphaPrecision.Opaque);
            Convert.FromHexString(hex).CopyTo(native.Colors, 56);
            Assert.Equal(new byte[] { 128, 32, 64, 255, 128, 96, 64, 255 }, Xbox360NativeTextureCodec.Decode(native));
        }
        /// <summary>Uses independently authored BC and CTX endpoint blocks to check compact-channel sampling.</summary>
        [Theory]
        [InlineData("DXT1", "00F8000000000000", 255, 0, 0, 255)]
        [InlineData("DXT2_3", "AAAAAAAAAAAAAAAA00F8000000000000", 255, 0, 0, 170)]
        [InlineData("DXT4_5", "800000000000000000F8000000000000", 255, 0, 0, 128)]
        [InlineData("DXN", "FF000000000000008000000000000000", 255, 128, 0, 255)]
        [InlineData("DXT3A", "7777777777777777", 255, 255, 255, 119)]
        [InlineData("DXT5A", "8000000000000000", 255, 255, 255, 128)]
        [InlineData("CTX1", "4080206000000000", 128, 64, 0, 255)]
        [InlineData("DXT3A_AS_1_1_1_1", "9999999999999999", 255, 0, 0, 255)]
        public void Decode_IndependentCompactBlockFixture(string name, string hex, byte red, byte green, byte blue, byte alpha) {
            using TextureAsset source = Pixel(0, 0, 0, 255);
            Assert.True(Xbox360NativeTextureFormatCatalog.TryGetFormat("Xbox360.Linear." + name, out Xbox360NativeTextureFormatDefinition format));
            using TextureAsset native = Xbox360NativeTextureCodec.Encode(source, format.Id, format.DefaultAlphaPrecision);
            Convert.FromHexString(hex).CopyTo(native.Colors, 56);
            Assert.Equal(new byte[] { red, green, blue, alpha }, Xbox360NativeTextureCodec.Decode(native));
        }
        /// <summary>Rejects every modified canonical header word, including fetch controls and authored extents.</summary>
        [Theory]
        [InlineData(0)] [InlineData(4)] [InlineData(8)] [InlineData(12)] [InlineData(16)] [InlineData(20)] [InlineData(24)]
        [InlineData(28)] [InlineData(32)] [InlineData(36)] [InlineData(40)] [InlineData(44)] [InlineData(48)] [InlineData(52)]
        public void Decode_RejectsNoncanonicalHeaderWord(int offset) {
            using TextureAsset source = Pixel(1, 2, 3, 4);
            using TextureAsset native = Xbox360NativeTextureCodec.Encode(source, "Xbox360.Linear.8_8_8_8", TextureAssetAlphaPrecision.A8);
            native.Colors[offset] ^= 0x80;
            Assert.Throws<ArgumentException>(() => Xbox360NativeTextureCodec.Decode(native));
        }
        /// <summary>Checks the final pitch limit independently of texel allocation and preserves DXT block-row alignment.</summary>
        [Fact]
        public void Layout_Uses32BlocksAnd256BytesWithoutPowerOfTwoPadding() {
            Assert.True(Xbox360NativeTextureFormatCatalog.TryGetHardwareFormat(19, out Xbox360NativeTextureFormatDefinition dxt));
            Xbox360NativeTextureLayout layout = new(dxt, 65, 5);
            Assert.Equal(128, layout.StorageWidth); Assert.Equal(8, layout.StorageHeight); Assert.Equal(512, layout.PitchBytes); Assert.Equal(1024, layout.TexelLength);
            Assert.True(Xbox360NativeTextureFormatCatalog.TryGetHardwareFormat(6, out Xbox360NativeTextureFormatDefinition rgba));
            Assert.Equal(8192, new Xbox360NativeTextureLayout(rgba, 8192, 8192).StorageWidth);
            Assert.Throws<ArgumentOutOfRangeException>(() => new Xbox360NativeTextureLayout(rgba, 8193, 1));
        }
        /// <summary>Checks source-alpha encoding and each representable precision using independent 85/170 coverage fixtures.</summary>
        [Theory]
        [InlineData(TextureAssetAlphaPrecision.A8, 85, 170)]
        [InlineData(TextureAssetAlphaPrecision.A4, 85, 170)]
        [InlineData(TextureAssetAlphaPrecision.Binary, 0, 255)]
        [InlineData(TextureAssetAlphaPrecision.Opaque, 255, 255)]
        public void Encode_8AStoresAlphaRatherThanSourceLuminance(TextureAssetAlphaPrecision precision, byte firstAlpha, byte secondAlpha) {
            using TextureAsset source = new() { Width = 2, Height = 1, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8,
                Colors = new byte[] { 0, 0, 0, 85, 255, 0, 0, 170 } };
            using TextureAsset native = Xbox360NativeTextureCodec.Encode(source, "Xbox360.Linear.8_A", precision);
            Assert.Equal(0x16du, Word(native, 40));
            Assert.Equal(new byte[] { firstAlpha, secondAlpha }, native.Colors.Skip(56).Take(2));
            Assert.Equal(new byte[] { 255, 255, 255, firstAlpha, 255, 255, 255, secondAlpha }, Xbox360NativeTextureCodec.Decode(native));
        }
        /// <summary>Checks independently supplied single-channel alpha bytes use white RGB and survive canonical 111R sampling.</summary>
        [Fact]
        public void Decode_8AIndependentAlphaFixtureSamplesWhiteCoverage() {
            using TextureAsset source = new() { Width = 2, Height = 1, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = new byte[8] };
            using TextureAsset native = Xbox360NativeTextureCodec.Encode(source, "Xbox360.Linear.8_A", TextureAssetAlphaPrecision.A8);
            native.Colors[56] = 0x55; native.Colors[57] = 0xaa;
            Assert.Equal(new byte[] { 255, 255, 255, 85, 255, 255, 255, 170 }, Xbox360NativeTextureCodec.Decode(native));
        }
        /// <summary>Preserves the four actual hardware alpha levels in native RGB10A2 and its expanded sampling alias.</summary>
        [Theory]
        [InlineData("2_10_10_10")]
        [InlineData("2_10_10_10_AS_16_16_16_16")]
        public void Encode_Rgb10A2PreservesAllFourIndependentAlphaLevels(string name) {
            Assert.True(Xbox360NativeTextureFormatCatalog.TryGetFormat("Xbox360.Linear." + name, out Xbox360NativeTextureFormatDefinition format));
            Assert.Equal(TextureAssetAlphaPrecision.A2, format.DefaultAlphaPrecision);
            Assert.Equal(new[] { TextureAssetAlphaPrecision.Opaque, TextureAssetAlphaPrecision.Binary, TextureAssetAlphaPrecision.A2 }, format.SupportedAlphaPrecisions);
            using TextureAsset source = new() { Width = 4, Height = 1, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8,
                Colors = new byte[] { 0, 0, 0, 0, 0, 0, 0, 85, 0, 0, 0, 170, 0, 0, 0, 255 } };
            using TextureAsset native = Xbox360NativeTextureCodec.Encode(source, format.Id, TextureAssetAlphaPrecision.A2);
            Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0x40, 0, 0, 0, 0x80, 0, 0, 0, 0xc0 }, native.Colors.Skip(56).Take(16));
            Assert.Equal(source.Colors, Xbox360NativeTextureCodec.Decode(native));
            Assert.False(format.SupportsAlpha(TextureAssetAlphaPrecision.A4));
            Assert.False(format.SupportsAlpha(TextureAssetAlphaPrecision.A8));
        }
        /// <summary>Checks rounded two-bit thresholds independently of the source's exact four representable levels.</summary>
        [Theory]
        [InlineData(42, 0)] [InlineData(43, 85)] [InlineData(127, 85)]
        [InlineData(128, 170)] [InlineData(212, 170)] [InlineData(213, 255)]
        public void Encode_Rgb10A2RoundsToNearestLevel(byte input, byte expected) {
            using TextureAsset source = Pixel(0, 0, 0, input);
            using TextureAsset native = Xbox360NativeTextureCodec.Encode(source, "Xbox360.Linear.2_10_10_10", TextureAssetAlphaPrecision.A2);
            Assert.Equal(expected, Xbox360NativeTextureCodec.Decode(native)[3]);
        }
        /// <summary>Prevents appended two-bit precision from expanding generic, Original Xbox or unrelated Xenos capabilities.</summary>
        [Fact]
        public void AlphaA2_IsRestrictedToTheTwoNativeRgb10A2Codes() {
            Assert.Equal(4, (int)TextureAssetAlphaPrecision.A2);
            Assert.Equal(3, (int)TextureAssetAlphaPrecision.A8);
            for (int format = 0; format <= 13; format++) Assert.False(TextureAssetPixelCodec.IsAlphaPrecisionSupported((TextureAssetColorFormat)format, TextureAssetAlphaPrecision.A2));
            foreach (XboxNativeTextureFormatDefinition format in XboxNativeTextureFormatCatalog.Formats) Assert.False(format.SupportsAlpha(TextureAssetAlphaPrecision.A2));
            foreach (Xbox360NativeTextureFormatDefinition format in Xbox360NativeTextureFormatCatalog.Formats) Assert.Equal(format.HardwareFormat == 7 || format.HardwareFormat == 54, format.SupportsAlpha(TextureAssetAlphaPrecision.A2));
            using TextureAsset source = Pixel(0, 0, 0, 128);
            using TextureAsset unrelated = Xbox360NativeTextureCodec.Encode(source, "Xbox360.Linear.8_8_8_8", TextureAssetAlphaPrecision.A8);
            unrelated.AlphaPrecision = TextureAssetAlphaPrecision.A2;
            Assert.Throws<ArgumentException>(() => Xbox360NativeTextureCodec.Decode(unrelated));
        }
        /// <summary>Verifies binary16 normal and denormal boundaries through exact IEEE binary32 bit fixtures.</summary>
        [Theory]
        [InlineData(0x0001, 0x33800000u)]
        [InlineData(0x0400, 0x38800000u)]
        [InlineData(0x3c00, 0x3f800000u)]
        [InlineData(0x7bff, 0x477fe000u)]
        [InlineData(0xbc00, 0xbf800000u)]
        public void Decode_HalfBoundariesUseExactBinaryPowers(int half, uint expectedBits) {
            Assert.Equal(expectedBits, (uint)BitConverter.SingleToInt32Bits(Xbox360NativeTexturePixelCodec.DecodeHalf(half)));
        }
        /// <summary>Checks the compact-depth denormal scale and normal extremes without a mathematical power routine.</summary>
        [Theory]
        [InlineData(0x000001u, 0x2e800000u)]
        [InlineData(0x100000u, 0x38800000u)]
        [InlineData(0xf00000u, 0x3f800000u)]
        [InlineData(0xffffffu, 0x3ffffff8u)]
        public void Depth_FloatBoundariesUseExactBinaryPowers(uint depth, uint expectedBits) {
            Assert.Equal(expectedBits, (uint)BitConverter.SingleToInt32Bits(Xbox360NativeTexturePixelCodec.DecodeDepthFloat(depth)));
            Assert.Equal(depth, Xbox360NativeTexturePixelCodec.EncodeDepthFloat(BitConverter.Int32BitsToSingle((int)expectedBits)));
        }
        /// <summary>Constructs a single authored RGBA texel with owned storage.</summary>
        static TextureAsset Pixel(byte red, byte green, byte blue, byte alpha) {
            return new TextureAsset { Width = 1, Height = 1, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = new byte[] { red, green, blue, alpha } };
        }
        /// <summary>Reads a little-endian native header word.</summary>
        static uint Word(TextureAsset asset, int offset) { return BitConverter.ToUInt32(asset.Colors, offset); }
    }
}

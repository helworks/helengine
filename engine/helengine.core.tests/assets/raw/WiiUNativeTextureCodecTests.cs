using helengine;

namespace helengine.core.tests.assets.raw {
    /// <summary>Checks exact GX2 packing and sampler semantics independently of container endianness and GPU tiling.</summary>
    public sealed class WiiUNativeTextureCodecTests {
        /// <summary>Enumerates every valid surface format declared by the pinned SDK.</summary>
        public static TheoryData<string> Formats { get { TheoryData<string> cases = new(); foreach (WiiUNativeTextureFormatDefinition format in WiiUNativeTextureFormatCatalog.Formats) cases.Add(format.Id); return cases; } }
        /// <summary>Verifies complete SDK coverage and preserves concurrent platform tags.</summary>
        [Fact]
        public void Catalog_ContainsAll65ValidFormatsAndRejectsInvalidSentinel() {
            Assert.Equal(65, WiiUNativeTextureFormatCatalog.Formats.Length); Assert.Equal(65, WiiUNativeTextureFormatCatalog.Formats.Select(value => value.HardwareFormat).Distinct().Count());
            Assert.False(WiiUNativeTextureFormatCatalog.TryGetHardwareFormat(0, out _)); Assert.False(WiiUNativeTextureFormatCatalog.TryGetFormat("WiiU.INVALID", out _));
            Assert.Equal(19, (int)TextureAssetColorFormat.VitaNative); Assert.Equal(20, (int)TextureAssetColorFormat.SwitchNative); Assert.Equal(21, (int)TextureAssetColorFormat.WiiUNative);
            foreach (var format in WiiUNativeTextureFormatCatalog.Formats) { Assert.False(format.IsIndexed); Assert.True(format.SupportsRawImport); Assert.True(format.SupportsAlpha(format.DefaultAlphaPrecision)); Assert.False(format.SupportsAlpha(TextureAssetAlphaPrecision.A3)); Assert.False(format.SupportsAlpha(TextureAssetAlphaPrecision.A5)); }
        }
        /// <summary>Exercises every exact GX2 word with canonical independent buffers and honest raw-only image capability.</summary>
        [Theory]
        [MemberData(nameof(Formats))]
        public void AllSdkFormats_EncodeOrImportCanonicalOwnedStorage(string id) {
            Assert.True(WiiUNativeTextureFormatCatalog.TryGetFormat(id, out var format)); using TextureAsset source = Source(3, 5); byte[] original = source.Colors.ToArray(); WiiUNativeTextureLayout layout = new(format, 3, 5);
            using TextureAsset native = format.SupportsCooking ? WiiUNativeTextureCodec.Encode(source, id, format.DefaultAlphaPrecision) : WiiUNativeTextureCodec.WrapRaw(source, id, format.DefaultAlphaPrecision, new byte[layout.TexelLength]);
            WiiUNativeTextureLayout actual = WiiUNativeTextureCodec.ReadLayout(native); Assert.Equal(format.HardwareFormat, actual.Format.HardwareFormat); Assert.Equal(64 + layout.TexelLength, native.Colors.Length); Assert.Null(native.PaletteColors);
            Assert.Equal(original, source.Colors); Assert.NotSame(source.Colors, native.Colors); Assert.Equal(new byte[] { 0x57, 0x47, 0x54, 0x31 }, native.Colors.Take(4));
            if (format.SupportsPreview) Assert.Equal(60, TextureAssetPixelCodec.DecodeToRgba32(native).Length);
            else { Assert.NotEmpty(format.UnsupportedReason); Assert.Throws<NotSupportedException>(() => WiiUNativeTextureCodec.Encode(source, id, format.DefaultAlphaPrecision)); Assert.Throws<NotSupportedException>(() => TextureAssetPixelCodec.DecodeToRgba32(native)); }
        }
        /// <summary>Uses literal AMD component bytes to verify exact packed fields, floats, signed normalization and compressed samples.</summary>
        [Theory]
        [InlineData("UNORM_R4_G4", "a3", "33aa00ff")]
        [InlineData("UNORM_R4_G4_B4_A4", "2143", "11223344")]
        [InlineData("UNORM_R5_G6_B5", "1f00", "ff0000ff")]
        [InlineData("UNORM_R5_G5_B5_A1", "1f80", "ff0000ff")]
        [InlineData("UNORM_A1_B5_G5_R5", "01f8", "ff0000ff")]
        [InlineData("UNORM_R10_G10_B10_A2", "ff030040", "ff000055")]
        [InlineData("UNORM_A2_B10_G10_R10", "0100c0ff", "ff000055")]
        [InlineData("UNORM_R24_X8", "00ffffff", "ff0000ff")]
        [InlineData("SNORM_R8", "80", "000000ff")]
        [InlineData("SNORM_R8", "7f", "ff0000ff")]
        [InlineData("FLOAT_R16", "0038", "800000ff")]
        [InlineData("FLOAT_R32", "0000003f", "800000ff")]
        [InlineData("FLOAT_R11_G11_B10", "c0030000", "ff0000ff")]
        [InlineData("UNORM_BC1", "00f8000000000000", "ff0000ff")]
        [InlineData("UNORM_BC1", "00000000ffffffff", "00000000")]
        [InlineData("SNORM_BC4", "817f000000000000", "000000ff")]
        [InlineData("SRGB_R8_G8_B8_A8", "bc890055", "80400055")]
        public void Decode_IndependentNativePackingFixtures(string suffix, string bytes, string expected) {
            using TextureAsset native = Raw("WiiU." + suffix, Convert.FromHexString(bytes)); Assert.Equal(Convert.FromHexString(expected), WiiUNativeTextureCodec.Decode(native));
        }
        /// <summary>Preserves all four unsigned two-bit alpha levels in both native physical placements.</summary>
        [Theory]
        [InlineData("WiiU.UNORM_R10_G10_B10_A2", 30)]
        [InlineData("WiiU.UNORM_A2_B10_G10_R10", 0)]
        public void Alpha2_PreservesFourNativeLevels(string id, int shift) {
            using TextureAsset source = Source(4, 1); for (int x = 0; x < 4; x++) source.Colors[x * 4 + 3] = (byte)(x * 85);
            using TextureAsset native = WiiUNativeTextureCodec.Encode(source, id, TextureAssetAlphaPrecision.A2);
            for (int x = 0; x < 4; x++) Assert.Equal((uint)x, (VitaNativeTextureCodec.ReadWord(native.Colors, 64 + x * 4) >> shift) & 3);
            Assert.Equal(new byte[] { 0, 85, 170, 255 }, WiiUNativeTextureCodec.Decode(native).Where((value, index) => index % 4 == 3));
            Assert.True(WiiUNativeTextureFormatCatalog.TryGetFormat("WiiU.SNORM_R10_G10_B10_A2", out var signed)); Assert.Equal(TextureAssetAlphaPrecision.Binary, signed.DefaultAlphaPrecision); Assert.False(signed.SupportsAlpha(TextureAssetAlphaPrecision.A2));
        }
        /// <summary>Checks standard sRGB transfer thresholds, independent alpha and every eight-bit transfer entry.</summary>
        [Fact]
        public void Srgb_TransfersOnlyRgbAndMatchesStandardFunctions() {
            for (int value = 0; value < 256; value++) {
                double normalized = value / 255.0; int encoded = (int)Math.Floor((normalized <= 0.0031308 ? normalized * 12.92 : 1.055 * Math.Pow(normalized, 1 / 2.4) - 0.055) * 255 + 0.5); int decoded = (int)Math.Floor((normalized <= 0.04045 ? normalized / 12.92 : Math.Pow((normalized + 0.055) / 1.055, 2.4)) * 255 + 0.5);
                Assert.Equal(encoded, WiiUNativeTextureSrgbCodec.Encode(value)); Assert.Equal(decoded, WiiUNativeTextureSrgbCodec.Decode(value)); Assert.InRange(Math.Abs(WiiUNativeTextureSrgbCodec.Decode(WiiUNativeTextureSrgbCodec.Encode(value)) - value), 0, 1);
            }
            using TextureAsset source = Source(1, 1); source.Colors = new byte[] { 128, 64, 0, 85 }; using TextureAsset native = WiiUNativeTextureCodec.Encode(source, "WiiU.SRGB_R8_G8_B8_A8", TextureAssetAlphaPrecision.A8);
            Assert.Equal(new byte[] { 188, 137, 0, 85 }, native.Colors.Skip(64)); Assert.Equal(new byte[] { 128, 64, 0, 85 }, WiiUNativeTextureCodec.Decode(native));
        }
        /// <summary>Checks all four 32-bit float components survive native 128-bit vector storage.</summary>
        [Fact]
        public void Float128_StoresEveryComponentWithoutTruncatingAt64Bits() {
            using TextureAsset source = Source(1, 1); source.Colors = new byte[] { 255, 0, 255, 255 }; using TextureAsset native = WiiUNativeTextureCodec.Encode(source, "WiiU.FLOAT_R32_G32_B32_A32", TextureAssetAlphaPrecision.A8);
            Assert.Equal(new byte[] { 0, 0, 128, 63, 0, 0, 0, 0, 0, 0, 128, 63, 0, 0, 128, 63 }, native.Colors.Skip(64)); Assert.Equal(source.Colors, WiiUNativeTextureCodec.Decode(native));
        }
        /// <summary>Verifies the reused BC block codec does not import Vita's smaller maximum image extent.</summary>
        [Fact]
        public void BcCodec_SupportsFullWiiUHorizontalExtent() {
            using TextureAsset source = Source(8192, 1); Array.Fill(source.Colors, (byte)255); using TextureAsset native = WiiUNativeTextureCodec.Encode(source, "WiiU.UNORM_BC1", TextureAssetAlphaPrecision.Opaque);
            Assert.Equal(64 + 8192 / 4 * 8, native.Colors.Length); Assert.Equal(source.Colors, WiiUNativeTextureCodec.Decode(native));
        }
        /// <summary>Checks even NV12 plane extents while preserving logical odd dimensions and exact raw bytes.</summary>
        [Fact]
        public void Nv12_ImportsOddLogicalExtentsAsEvenNativePlanes() {
            Assert.True(WiiUNativeTextureFormatCatalog.TryGetFormat("WiiU.UNORM_NV12", out var format)); WiiUNativeTextureLayout layout = new(format, 3, 5);
            Assert.Equal(12, format.BitsPerPixel); Assert.Equal(4, layout.StorageWidth); Assert.Equal(6, layout.StorageHeight); Assert.Equal(24, layout.UvPlaneOffset); Assert.Equal(36, layout.TexelLength);
            using TextureAsset source = Source(3, 5); byte[] bytes = Enumerable.Range(0, 36).Select(value => (byte)value).ToArray(); using TextureAsset native = WiiUNativeTextureCodec.WrapRaw(source, format.Id, TextureAssetAlphaPrecision.Opaque, bytes);
            Assert.Equal(bytes, native.Colors.Skip(64)); Assert.False(format.SupportsCooking); Assert.Throws<NotSupportedException>(() => WiiUNativeTextureCodec.Decode(native));
        }
        /// <summary>Rejects descriptor contradictions, palettes, unsupported alpha and excess dimensions.</summary>
        [Fact]
        public void Header_RejectsNoncanonicalMetadata() {
            using TextureAsset source = Source(3, 5); using TextureAsset native = WiiUNativeTextureCodec.Encode(source, "WiiU.UNORM_R8_G8_B8_A8", TextureAssetAlphaPrecision.A8);
            native.Colors[40] ^= 1; Assert.Throws<ArgumentException>(() => WiiUNativeTextureCodec.ReadLayout(native)); native.Colors[40] ^= 1;
            native.PaletteColors = new byte[4]; Assert.Throws<ArgumentException>(() => WiiUNativeTextureCodec.ReadLayout(native)); native.PaletteColors = null;
            native.Colors[52] = 1; Assert.Throws<ArgumentException>(() => WiiUNativeTextureCodec.ReadLayout(native));
            using TextureAsset oversized = Source(8193, 1); Assert.Throws<ArgumentOutOfRangeException>(() => WiiUNativeTextureCodec.Encode(oversized, "WiiU.UNORM_R8_G8_B8_A8", TextureAssetAlphaPrecision.A8));
        }
        /// <summary>Creates deterministic normalized RGBA source pixels without shared buffers.</summary>
        static TextureAsset Source(int width, int height) { TextureAsset source = new() { Width = (ushort)width, Height = (ushort)height, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = new byte[width * height * 4] }; for (int pixel = 0; pixel < width * height; pixel++) { source.Colors[pixel * 4] = (byte)(pixel * 7); source.Colors[pixel * 4 + 1] = 64; source.Colors[pixel * 4 + 2] = 128; source.Colors[pixel * 4 + 3] = (byte)(pixel % 2 == 0 ? 85 : 170); } return source; }
        /// <summary>Wraps a literal native scalar or BC block without using the image encoder.</summary>
        static TextureAsset Raw(string id, byte[] bytes) { Assert.True(WiiUNativeTextureFormatCatalog.TryGetFormat(id, out var format)); using TextureAsset source = Source(1, 1); WiiUNativeTextureLayout layout = new(format, 1, 1); byte[] texels = new byte[layout.TexelLength]; Array.Copy(bytes, texels, bytes.Length); return WiiUNativeTextureCodec.WrapRaw(source, id, format.DefaultAlphaPrecision, texels); }
    }
}

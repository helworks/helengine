using helengine;

namespace helengine.core.tests.assets.raw {
    /// <summary>Validates native GXM storage with independent component fixtures and exhaustive declared-format contracts.</summary>
    public sealed class VitaNativeTextureCodecTests {
        /// <summary>Enumerates exact SDK format identifiers, including spelling aliases.</summary>
        public static TheoryData<string> Formats {
            get { TheoryData<string> data = new(); foreach (VitaNativeTextureFormatDefinition format in VitaNativeTextureFormatCatalog.Formats) data.Add(format.Id); return data; }
        }
        /// <summary>Enumerates declared YUV plane and byte-order choices in both independently configured conversion banks.</summary>
        public static TheoryData<string> YuvFormats {
            get { TheoryData<string> data = new(); foreach (VitaNativeTextureFormatDefinition format in VitaNativeTextureFormatCatalog.Formats) if (format.NumericKind == VitaNativeTextureNumericKind.Yuv) data.Add(format.Id); return data; }
        }
        /// <summary>Checks complete base coverage, stable tags and explicit raw-only shader semantics.</summary>
        [Fact]
        public void Catalog_Covers312SdkDeclarationsAnd51PhysicalBases() {
            Assert.Equal(312, VitaNativeTextureFormatCatalog.Formats.Length);
            Assert.Equal(51, VitaNativeTextureFormatCatalog.Formats.Select(value => value.BaseFormat).Distinct().Count());
            Assert.Equal(18, (int)TextureAssetColorFormat.NintendoDsNative);
            Assert.Equal(19, (int)TextureAssetColorFormat.VitaNative);
            foreach (VitaNativeTextureFormatDefinition format in VitaNativeTextureFormatCatalog.Formats) {
                Assert.True(format.SupportsRawImport); Assert.True(format.SupportsAlpha(format.DefaultAlphaPrecision));
                Assert.False(format.SupportsAlpha(TextureAssetAlphaPrecision.A3));
                if (!format.SupportsCooking) { Assert.False(format.SupportsPreview); Assert.NotEmpty(format.UnsupportedReason); }
            }
        }
        /// <summary>Exercises every SDK declaration while keeping specialized shader records byte-exact and image capability honest.</summary>
        [Theory]
        [MemberData(nameof(Formats))]
        public void AllSdkFormats_EncodeOrImportCanonicalOwnedStorage(string id) {
            Assert.True(VitaNativeTextureFormatCatalog.TryGetFormat(id, out VitaNativeTextureFormatDefinition format));
            using TextureAsset source = Source(format.IsIndexed, 3, 2);
            VitaNativeTextureLayout layout = new(format, 3, 2, format.DefaultLayoutType);
            byte[] original = source.Colors.ToArray();
            using TextureAsset asset = format.SupportsCooking ? VitaNativeTextureCodec.Encode(source, id, format.DefaultAlphaPrecision) : VitaNativeTextureCodec.WrapRaw(source, id, format.DefaultAlphaPrecision, format.DefaultLayoutType, new byte[layout.TexelLength], null);
            VitaNativeTextureLayout actual = VitaNativeTextureCodec.ReadLayout(asset);
            Assert.Equal(format.HardwareFormat, actual.Format.HardwareFormat); Assert.Equal(64 + layout.TexelLength, asset.Colors.Length);
            Assert.Equal(original, source.Colors); Assert.NotSame(source.Colors, asset.Colors);
            Assert.Equal(0x31544756u, VitaNativeTextureCodec.ReadWord(asset.Colors, 0)); Assert.Equal(format.HardwareFormat, VitaNativeTextureCodec.ReadWord(asset.Colors, 8));
            if (format.SupportsPreview) Assert.Equal(24, TextureAssetPixelCodec.DecodeToRgba32(asset).Length);
            else { Assert.Throws<NotSupportedException>(() => VitaNativeTextureCodec.Encode(source, id, format.DefaultAlphaPrecision)); Assert.Throws<NotSupportedException>(() => TextureAssetPixelCodec.DecodeToRgba32(asset)); }
            Assert.Equal(format.GetVramByteLength(3, 2), actual.TexelLength + actual.PaletteLength);
        }
        /// <summary>Uses literal native component words to verify signed normalization, IEEE floats and sampled swizzles.</summary>
        [Theory]
        [InlineData("S8_R", "80", "000000ff")]
        [InlineData("S8_R", "7f", "ff0000ff")]
        [InlineData("U8_R111", "55", "ffffff55")]
        [InlineData("F16_R", "0038", "800000ff")]
        [InlineData("F16_R", "003c", "ff0000ff")]
        [InlineData("F16_R", "00bc", "000000ff")]
        [InlineData("F32_R", "0000003f", "800000ff")]
        [InlineData("F32M_R", "000080bf", "ff0000ff")]
        [InlineData("U8U8U8U8_ARGB", "11223344", "33221144")]
        [InlineData("U8U8U8U8_RGBA", "11223344", "44332211")]
        [InlineData("U8U8_GRRR", "55aa", "555555aa")]
        [InlineData("U8U3U3U2_ARGB", "e055", "ff000055")]
        [InlineData("U2F10F10F10_ABGR", "e0010000", "ff000000")]
        [InlineData("U1U5U5U5_ABGR", "1f80", "ff0000ff")]
        [InlineData("U5U5U5U1_RGBA", "01f8", "ff0000ff")]
        [InlineData("U6S5S5_BGR", "0f00", "ff0000ff")]
        [InlineData("S5S5U6_RGB", "0078", "ff0000ff")]
        [InlineData("F10F11F11_BGR", "c0030000", "ff0000ff")]
        [InlineData("F11F11F10_RGB", "00000078", "ff0000ff")]
        [InlineData("SE5M9M9M9_BGR", "00010284", "ffffffff")]
        public void Decode_IndependentNativeComponentFixtures(string suffix, string bytes, string expected) {
            using TextureAsset asset = Raw("Vita." + suffix, 1, 1, Convert.FromHexString(bytes));
            Assert.Equal(Convert.FromHexString(expected), VitaNativeTextureCodec.Decode(asset));
        }
        /// <summary>Verifies shader-only integer values remain unmodified instead of being normalized through image cooking.</summary>
        [Fact]
        public void RawInteger_PreservesNativeBytesAndRejectsImagePreview() {
            using TextureAsset asset = Raw("Vita.U32_R", 1, 1, new byte[] { 0xef, 0xbe, 0xad, 0xde });
            Assert.Equal(new byte[] { 0xef, 0xbe, 0xad, 0xde }, asset.Colors.Skip(64).Take(4));
            Assert.Throws<NotSupportedException>(() => TextureAssetPixelCodec.DecodeToRgba32(asset));
        }
        /// <summary>Verifies packed P4 minimum allocations and rectangular Y-first Morton addressing.</summary>
        [Fact]
        public void Layout_P4SingleTexelAndMortonYFirstAreExact() {
            Assert.True(VitaNativeTextureFormatCatalog.TryGetFormat("Vita.P4_ABGR", out var format));
            VitaNativeTextureLayout layout = new(format, 1, 1, VitaNativeTextureLayoutType.Swizzled);
            Assert.Equal(1, layout.TexelLength); Assert.Equal(64, layout.PaletteLength);
            Assert.Equal(new[] { 0, 2, 4, 6, 1, 3, 5, 7 }, Enumerable.Range(0, 8).Select(value => VitaNativeTextureLayout.MortonIndex(value % 4, value / 4, 4, 2)));
            Assert.Throws<ArgumentException>(() => new VitaNativeTextureLayout(format, 3, 2, VitaNativeTextureLayoutType.Swizzled));
        }
        /// <summary>Checks full native palettes and independent low-first nibbles for a logical odd row.</summary>
        [Fact]
        public void Indexed_PaletteSwizzleAndOddNibblesAreNative() {
            using TextureAsset source = Source(true, 3, 1); source.Colors = new byte[] { 0x10, 0x01 }; source.PaletteColors = new byte[] { 1, 2, 3, 4, 10, 20, 30, 40 };
            using TextureAsset asset = VitaNativeTextureCodec.Encode(source, "Vita.P4_ARGB", TextureAssetAlphaPrecision.A8);
            Assert.Equal(64, asset.PaletteColors.Length); Assert.Equal(new byte[] { 3, 2, 1, 4, 30, 20, 10, 40 }, asset.PaletteColors.Take(8));
            Assert.Equal(0x10, asset.Colors[64]); Assert.Equal(0x11, asset.Colors[65]);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 10, 20, 30, 40, 10, 20, 30, 40 }, VitaNativeTextureCodec.Decode(asset));
        }
        /// <summary>Checks A2's actual four alpha levels and rejects unrelated alpha metadata.</summary>
        [Theory]
        [InlineData("Vita.U2U10U10U10_ABGR")]
        [InlineData("Vita.U2F10F10F10_ABGR")]
        public void PackedAlpha2_PreservesAllFourCoverageLevels(string id) {
            using TextureAsset source = Source(false, 4, 1);
            for (int x = 0; x < 4; x++) source.Colors[x * 4 + 3] = (byte)(x * 85);
            using TextureAsset asset = VitaNativeTextureCodec.Encode(source, id, TextureAssetAlphaPrecision.A2);
            for (int x = 0; x < 4; x++) Assert.Equal((uint)x, VitaNativeTextureCodec.ReadWord(asset.Colors, 64 + x * 4) >> 30);
            Assert.Equal(new byte[] { 0, 85, 170, 255 }, VitaNativeTextureCodec.Decode(asset).Where((value, index) => index % 4 == 3));
            asset.AlphaPrecision = TextureAssetAlphaPrecision.A8; Assert.Throws<ArgumentException>(() => VitaNativeTextureCodec.ReadLayout(asset));
        }
        /// <summary>Checks header length, reserved words and strict agreement with outer asset metadata.</summary>
        [Fact]
        public void Header_RejectsNoncanonicalWordsWithoutMutatingSource() {
            using TextureAsset source = Source(false, 3, 2); using TextureAsset asset = VitaNativeTextureCodec.Encode(source, "Vita.U8U8U8U8_ABGR", TextureAssetAlphaPrecision.A8);
            asset.Colors[52] = 1; Assert.Throws<ArgumentException>(() => VitaNativeTextureCodec.ReadLayout(asset)); asset.Colors[52] = 0;
            asset.Colors[24]++; Assert.Throws<ArgumentException>(() => VitaNativeTextureCodec.ReadLayout(asset));
        }
        /// <summary>Checks independent ETC1, BC1 and signed BC4 literals rather than comparing an encoder to itself.</summary>
        [Theory]
        [InlineData("ETC1_1BGR", "ff00000000000000", "ff0202ff")]
        [InlineData("UBC1_ABGR", "00f8000000000000", "ff0000ff")]
        [InlineData("UBC1_ABGR", "00000000ffffffff", "00000000")]
        [InlineData("UBC4_R", "ff00000000000000", "ff0000ff")]
        [InlineData("SBC4_R", "817f000000000000", "000000ff")]
        public void Compressed_IndependentBlocksUseGpuInterpolation(string suffix, string bytes, string expected) {
            using TextureAsset asset = Raw("Vita." + suffix, 1, 1, Convert.FromHexString(bytes));
            Assert.Equal(Convert.FromHexString(expected), VitaNativeTextureCodec.Decode(asset));
        }
        /// <summary>Uses independent BT.601 limited-range black and white samples for every pair/plane ordering and both profile banks.</summary>
        [Theory]
        [MemberData(nameof(YuvFormats))]
        public void Yuv_AllOrdersUseCanonicalBt601Profile(string id) {
            Assert.True(VitaNativeTextureFormatCatalog.TryGetFormat(id, out var format)); using TextureAsset source = Source(false, 2, 1);
            source.Colors = new byte[] { 0, 0, 0, 255, 255, 255, 255, 255 }; VitaNativeTextureLayout layout = new(format, 2, 1, format.DefaultLayoutType);
            byte[] native = new byte[layout.TexelLength];
            if (format.BaseFormat == 0x92) {
                byte[] pair = id.Contains("YUYV") ? new byte[] { 16, 128, 235, 128 } : id.Contains("YVYU") ? new byte[] { 16, 128, 235, 128 } : new byte[] { 128, 16, 128, 235 };
                pair.CopyTo(native, 0);
            } else {
                Array.Fill(native, (byte)128); native[0] = 16; native[1] = 235;
            }
            using TextureAsset imported = VitaNativeTextureCodec.WrapRaw(source, id, TextureAssetAlphaPrecision.Opaque, format.DefaultLayoutType, native, null);
            Assert.Equal(source.Colors, VitaNativeTextureCodec.Decode(imported));
            using TextureAsset encoded = VitaNativeTextureCodec.Encode(source, id, TextureAssetAlphaPrecision.Opaque);
            if (format.BaseFormat == 0x92) Assert.Equal(native.Take(4), encoded.Colors.Skip(64).Take(4));
            else { Assert.Equal(16, encoded.Colors[64]); Assert.Equal(235, encoded.Colors[65]); Assert.Equal(128, encoded.Colors[64 + layout.StorageWidth * layout.StorageHeight]); }
            if (format.BaseFormat == 0x92) {
                byte[] redPair = id.Contains("YUYV") ? new byte[] { 82, 90, 82, 240 } : id.Contains("YVYU") ? new byte[] { 82, 240, 82, 90 } : id.Contains("UYVY") ? new byte[] { 90, 82, 240, 82 } : new byte[] { 240, 82, 90, 82 };
                redPair.CopyTo(native, 0);
            } else {
                Array.Fill(native, (byte)82); int plane = layout.StorageWidth * layout.StorageHeight; bool swapped = id.StartsWith("Vita.YVU");
                if (format.BaseFormat == 0x90) { native[plane] = swapped ? (byte)240 : (byte)90; native[plane + 1] = swapped ? (byte)90 : (byte)240; }
                else { native[plane] = swapped ? (byte)240 : (byte)90; native[plane + plane / 4] = swapped ? (byte)90 : (byte)240; }
            }
            using TextureAsset red = VitaNativeTextureCodec.WrapRaw(source, id, TextureAssetAlphaPrecision.Opaque, format.DefaultLayoutType, native, null);
            Assert.Equal(new byte[] { 255, 1, 0, 255, 255, 1, 0, 255 }, VitaNativeTextureCodec.Decode(red));
        }
        /// <summary>Builds owned source pixels with deterministic channels and an already quantized palette when requested.</summary>
        static TextureAsset Source(bool indexed, int width, int height) {
            TextureAsset source = new() { Id = "vita-fixture", Width = (ushort)width, Height = (ushort)height, ColorFormat = indexed ? TextureAssetColorFormat.Indexed4 : TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8, FormerAuthoringAssetIds = new[] { "old-vita" }, Colors = new byte[indexed ? (width * height + 1) / 2 : width * height * 4] };
            if (indexed) source.PaletteColors = new byte[] { 255, 0, 0, 255 };
            else for (int pixel = 0; pixel < width * height; pixel++) { source.Colors[pixel * 4] = (byte)(17 + pixel * 7); source.Colors[pixel * 4 + 1] = (byte)(63 + pixel * 3); source.Colors[pixel * 4 + 2] = (byte)(191 - pixel * 5); source.Colors[pixel * 4 + 3] = (byte)(pixel % 2 == 0 ? 85 : 170); }
            return source;
        }
        /// <summary>Imports independent physical bytes into a canonical one-level payload.</summary>
        static TextureAsset Raw(string id, int width, int height, byte[] bytes) {
            Assert.True(VitaNativeTextureFormatCatalog.TryGetFormat(id, out var format)); using TextureAsset source = Source(false, width, height);
            VitaNativeTextureLayout layout = new(format, width, height, format.DefaultLayoutType); byte[] texels = new byte[layout.TexelLength]; Array.Copy(bytes, texels, bytes.Length);
            return VitaNativeTextureCodec.WrapRaw(source, id, format.DefaultAlphaPrecision, format.DefaultLayoutType, texels, format.IsIndexed ? new byte[layout.PaletteLength] : null);
        }
    }
}

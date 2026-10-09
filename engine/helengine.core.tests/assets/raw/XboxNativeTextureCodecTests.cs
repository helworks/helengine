using helengine;

namespace helengine.core.tests.assets.raw {
    /// <summary>Verifies native Xbox byte layouts, sampling semantics and strict payload validation with independent golden values.</summary>
    public sealed class XboxNativeTextureCodecTests {
        /// <summary>Enumerates every pinned hardware code and its canonical platform identifier.</summary>
        public static TheoryData<string> Formats {
            get {
                TheoryData<string> formats = new();
                foreach (XboxNativeTextureFormatDefinition definition in XboxNativeTextureFormatCatalog.Formats) formats.Add(definition.Id);
                return formats;
            }
        }

        /// <summary>Proves the catalog covers all 42 pinned codes once and does not renumber the existing generic formats.</summary>
        [Fact]
        public void Catalog_CoversPinnedCodesAndStableGenericIds() {
            int[] expected = { 0, 1, 2, 3, 4, 5, 6, 7, 11, 12, 14, 15, 16, 17, 18, 19, 22, 23, 25, 26, 27, 28, 29, 30, 31,
                32, 36, 37, 39, 40, 41, 44, 46, 48, 49, 53, 58, 59, 60, 63, 64, 65 };
            Assert.Equal(expected, XboxNativeTextureFormatCatalog.Formats.Select(format => format.HardwareFormat));
            Assert.Equal(13, (int)TextureAssetColorFormat.XboxNative);
            Assert.Equal(12, (int)TextureAssetColorFormat.Ps1Bgr555);
            Assert.False(XboxNativeTextureFormatCatalog.TryGetFormat("Xbox.DXT2", out _));
            Assert.False(XboxNativeTextureFormatCatalog.TryGetHardwareFormat(0x2f, out _));
        }

        /// <summary>Releases independently constructed policy arrays exactly once while permanent catalog entries remain usable.</summary>
        [Fact]
        public void Definition_DisposeReleasesOwnedPoliciesWithoutChangingCatalog() {
            using XboxNativeTextureFormatDefinition definition = new("test-definition", 0x12, 4, true, TextureAssetAlphaPrecision.A8, false);
            Assert.True(definition.SupportsAlpha(TextureAssetAlphaPrecision.A8));
            Assert.Equal(4, definition.SupportedAlphaPrecisions.Length);
            definition.Dispose();
            definition.Dispose();
            Assert.Null(definition.SupportedAlphaPrecisions);
            Assert.False(definition.SupportsAlpha(TextureAssetAlphaPrecision.A8));
            Assert.True(XboxNativeTextureFormatCatalog.TryGetFormat("Xbox.Linear.A8R8G8B8", out XboxNativeTextureFormatDefinition permanent));
            Assert.True(permanent.SupportsAlpha(TextureAssetAlphaPrecision.A8));
        }

        /// <summary>Exercises encode, canonical header validation and generic preview dispatch for every published native format.</summary>
        [Theory]
        [MemberData(nameof(Formats))]
        public void Encode_AllNativeFormatsProduceCanonicalOwnedStorage(string id) {
            Assert.True(XboxNativeTextureFormatCatalog.TryGetFormat(id, out XboxNativeTextureFormatDefinition format));
            using TextureAsset source = format.IsPaletted ? CreateIndexedSource() : CreateRgba(3, 2, new byte[] {
                255, 0, 0, 255, 0, 255, 0, 128, 0, 0, 255, 0, 255, 255, 255, 255, 0, 0, 0, 0, 128, 128, 128, 128 });
            byte[] original = source.Colors.ToArray();
            using TextureAsset native = XboxNativeTextureCodec.Encode(source, id, format.DefaultAlphaPrecision);
            XboxNativeTextureLayout layout = XboxNativeTextureCodec.ReadLayout(native);
            Assert.Equal(source.Width, native.Width);
            Assert.Equal(source.Height, native.Height);
            Assert.Equal(source.Id, native.Id);
            Assert.Equal(source.RuntimeAssetId, native.RuntimeAssetId);
            Assert.Equal(source.AuthoringAssetId, native.AuthoringAssetId);
            Assert.Equal(source.FormerAuthoringAssetIds, native.FormerAuthoringAssetIds);
            Assert.NotSame(source.FormerAuthoringAssetIds, native.FormerAuthoringAssetIds);
            Assert.Equal(original, source.Colors);
            Assert.Equal(0x31585458u, ReadHeader(native, 0));
            Assert.Equal(1u, ReadHeader(native, 4));
            Assert.Equal(0u, ReadHeader(native, 12));
            Assert.Equal((uint)format.HardwareFormat, ReadHeader(native, 8));
            Assert.Equal(48 + layout.TexelLength, native.Colors.Length);
            Assert.Equal(source.Width * source.Height * 4, TextureAssetPixelCodec.DecodeToRgba32(native).Length);
            if (format.IsLinear) Assert.Equal(0, layout.PitchBytes % 64);
            else Assert.Equal(0, layout.PitchBytes);
            if (format.IsPaletted) Assert.Equal(layout.PaletteLength, native.PaletteColors.Length);
            else Assert.Null(native.PaletteColors);
        }

        /// <summary>Checks exact native ARGB bytes and linear row padding rather than only testing an encoder-decoder roundtrip.</summary>
        [Fact]
        public void Encode_LinearArgbUsesBgraAnd64ByteRows() {
            using TextureAsset source = CreateRgba(2, 2, new byte[] {
                0x12, 0x34, 0x56, 0x78, 0x9a, 0xbc, 0xde, 0xf0, 1, 2, 3, 4, 5, 6, 7, 8 });
            using TextureAsset native = XboxNativeTextureCodec.Encode(source, "Xbox.Linear.A8R8G8B8", TextureAssetAlphaPrecision.A8);
            Assert.Equal(64u, ReadHeader(native, 32));
            Assert.Equal(128u, ReadHeader(native, 36));
            Assert.Equal(new byte[] { 0x56, 0x34, 0x12, 0x78, 0xde, 0xbc, 0x9a, 0xf0 }, native.Colors.Skip(48).Take(8));
            Assert.All(native.Colors.Skip(56).Take(56), value => Assert.Equal(0, value));
            Assert.Equal(new byte[] { 3, 2, 1, 4, 7, 6, 5, 8 }, native.Colors.Skip(112).Take(8));
            byte[] preview = XboxNativeTextureCodec.Decode(native);
            Assert.Equal(source.Colors, preview);
            preview[0] = 99;
            Assert.Equal(0x56, native.Colors[48]);
        }

        /// <summary>Verifies the rectangular Morton permutation and NPOT edge replication using explicitly ordered expected texels.</summary>
        [Fact]
        public void Encode_SwizzledRectangularMortonReplicatesEdges() {
            using TextureAsset source = CreateRgba(3, 2, new byte[] {
                1, 0, 0, 255, 2, 0, 0, 255, 3, 0, 0, 255, 4, 0, 0, 255, 5, 0, 0, 255, 6, 0, 0, 255 });
            using TextureAsset native = XboxNativeTextureCodec.Encode(source, "Xbox.Swizzled.A8R8G8B8", TextureAssetAlphaPrecision.A8);
            Assert.Equal(4u, ReadHeader(native, 24));
            Assert.Equal(2u, ReadHeader(native, 28));
            Assert.Equal(32u, ReadHeader(native, 36));
            Assert.Equal(new byte[] { 1, 2, 4, 5, 3, 3, 6, 6 }, Enumerable.Range(0, 8).Select(index => native.Colors[48 + index * 4 + 2]));
            Assert.Equal(source.Colors, XboxNativeTextureCodec.Decode(native));
        }

        /// <summary>Checks the little-endian channel permutations independently of the preview decoder.</summary>
        [Theory]
        [InlineData("Xbox.Linear.A8B8G8R8", 0x12, 0x34, 0x56, 0x78)]
        [InlineData("Xbox.Linear.B8G8R8A8", 0x78, 0x12, 0x34, 0x56)]
        [InlineData("Xbox.Linear.R8G8B8A8", 0x78, 0x56, 0x34, 0x12)]
        public void Encode_ChannelOrdersHaveNativeGoldenBytes(string id, byte first, byte second, byte third, byte fourth) {
            using TextureAsset source = CreateRgba(1, 1, new byte[] { 0x12, 0x34, 0x56, 0x78 });
            using TextureAsset native = XboxNativeTextureCodec.Encode(source, id, TextureAssetAlphaPrecision.A8);
            Assert.Equal(new byte[] { first, second, third, fourth }, native.Colors.Skip(48).Take(4));
            Assert.Equal(source.Colors, XboxNativeTextureCodec.Decode(native));
        }

        /// <summary>Proves ARGB1555, ARGB4444 and RGB565 words use their native channel positions.</summary>
        [Theory]
        [InlineData("Xbox.Linear.A1R5G5B5", TextureAssetAlphaPrecision.Binary, 0xfe02)]
        [InlineData("Xbox.Linear.A4R4G4B4", TextureAssetAlphaPrecision.A4, 0xff81)]
        [InlineData("Xbox.Linear.R5G6B5", TextureAssetAlphaPrecision.Opaque, 0xfc02)]
        public void Encode_PackedWordsMatchNativeBits(string id, TextureAssetAlphaPrecision alpha, int word) {
            using TextureAsset source = CreateRgba(1, 1, new byte[] { 255, 128, 16, 255 });
            using TextureAsset native = XboxNativeTextureCodec.Encode(source, id, alpha);
            Assert.Equal(word, XboxNativeTexturePixelCodec.ReadWord(native.Colors, 48));
        }

        /// <summary>Checks luma, coupled intensity-alpha, alpha-only white RGB and independent alpha-luma sampling.</summary>
        [Theory]
        [InlineData("Xbox.Linear.Y8", TextureAssetAlphaPrecision.Opaque, 76, 76, 76, 255)]
        [InlineData("Xbox.Linear.AY8", TextureAssetAlphaPrecision.A8, 76, 76, 76, 76)]
        [InlineData("Xbox.Linear.A8", TextureAssetAlphaPrecision.A8, 255, 255, 255, 128)]
        [InlineData("Xbox.Linear.A8Y8", TextureAssetAlphaPrecision.A8, 76, 76, 76, 128)]
        [InlineData("Xbox.Linear.G8B8", TextureAssetAlphaPrecision.A8, 16, 0, 16, 0)]
        [InlineData("Xbox.Linear.R8B8", TextureAssetAlphaPrecision.A8, 255, 16, 16, 255)]
        public void Decode_SpecialChannelSemanticsArePreserved(string id, TextureAssetAlphaPrecision alpha, byte red, byte green, byte blue, byte sampledAlpha) {
            using TextureAsset source = CreateRgba(1, 1, new byte[] { 255, 0, 16, 128 });
            // The luma fixture uses pure red; paired-channel fixtures retain their authored blue channel.
            if (id.Contains("Y8", StringComparison.Ordinal) || id.EndsWith(".A8", StringComparison.Ordinal)) source.Colors[2] = 0;
            using TextureAsset native = XboxNativeTextureCodec.Encode(source, id, alpha);
            Assert.Equal(new byte[] { red, green, blue, sampledAlpha }, XboxNativeTextureCodec.Decode(native));
        }

        /// <summary>Confirms fixed and NV float depth words encode normalized depth, rather than IEEE half or an incorrectly scaled maximum.</summary>
        [Theory]
        [InlineData("Xbox.Linear.DEPTH_Y16_FIXED", 0xffffu)]
        [InlineData("Xbox.Linear.DEPTH_Y16_FLOAT", 0x7000u)]
        [InlineData("Xbox.Linear.Y16", 0xffffu)]
        public void Encode_WhiteNormalizedDepthHasCorrectNativeWord(string id, uint word) {
            using TextureAsset source = CreateRgba(1, 1, new byte[] { 255, 255, 255, 0 });
            using TextureAsset native = XboxNativeTextureCodec.Encode(source, id, TextureAssetAlphaPrecision.Opaque);
            Assert.Equal(word, (uint)XboxNativeTexturePixelCodec.ReadWord(native.Colors, 48));
            Assert.Equal(new byte[] { 255, 255, 255, 255 }, XboxNativeTextureCodec.Decode(native));
        }

        /// <summary>Checks the low ignored byte and high normalized 24-bit depth channel.</summary>
        [Fact]
        public void Encode_Depth24UsesHighBitsAndLowIgnoredByte() {
            using TextureAsset source = CreateRgba(1, 1, new byte[] { 128, 128, 128, 255 });
            using TextureAsset native = XboxNativeTextureCodec.Encode(source, "Xbox.Linear.DEPTH_X8_Y24_FIXED", TextureAssetAlphaPrecision.Opaque);
            Assert.Equal(0x80808000u, ReadHeader(native, 48));
            Assert.Equal(new byte[] { 128, 128, 128, 255 }, XboxNativeTextureCodec.Decode(native));
        }

        /// <summary>Verifies signed bump packing and unsigned preview clipping of negative stored samples.</summary>
        [Fact]
        public void Encode_SignedBumpUsesFiveBitTwosComplement() {
            using TextureAsset source = CreateRgba(1, 1, new byte[] { 255, 128, 0, 255 });
            using TextureAsset native = XboxNativeTextureCodec.Encode(source, "Xbox.Swizzled.R6G5B5", TextureAssetAlphaPrecision.Opaque);
            Assert.Equal(0xfc10, XboxNativeTexturePixelCodec.ReadWord(native.Colors, 48));
            Assert.Equal(new byte[] { 255, 0, 0, 255 }, XboxNativeTextureCodec.Decode(native));
        }

        /// <summary>Checks native YUY2 and UYVY order and the duplicated final horizontal pair for odd widths.</summary>
        [Theory]
        [InlineData("Xbox.Linear.CR8YB8CB8YA8", true)]
        [InlineData("Xbox.Linear.YB8CR8YA8CB8", false)]
        public void Encode_YuvPairsMatchBt601AndRepeatOddEdge(string id, bool yuy2) {
            using TextureAsset source = CreateRgba(3, 1, new byte[] { 0, 0, 0, 255, 255, 255, 255, 255, 255, 255, 255, 255 });
            using TextureAsset native = XboxNativeTextureCodec.Encode(source, id, TextureAssetAlphaPrecision.Opaque);
            byte[] expected = yuy2 ? new byte[] { 16, 128, 235, 128, 235, 128, 235, 128 }
                : new byte[] { 128, 16, 128, 235, 128, 235, 128, 235 };
            Assert.Equal(expected, native.Colors.Skip(48).Take(8));
            Assert.Equal(source.Colors, XboxNativeTextureCodec.Decode(native));
        }

        /// <summary>Proves Indexed4 expands only indices to P8 and palettes retain native byte order with hardware-sized padding.</summary>
        [Fact]
        public void Encode_P8PreservesIndicesAndQuantizesPaletteAlpha() {
            using TextureAsset source = new() { Width = 3, Height = 1, ColorFormat = TextureAssetColorFormat.Indexed4,
                AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = new byte[] { 0x10, 0x01 },
                PaletteColors = new byte[] { 0x10, 0x20, 0x30, 0x40, 0xaa, 0xbb, 0xcc, 0xdd } };
            using TextureAsset native = XboxNativeTextureCodec.Encode(source, "Xbox.Swizzled.I8_A8R8G8B8", TextureAssetAlphaPrecision.A4);
            Assert.Equal(4u, ReadHeader(native, 36));
            Assert.Equal(32u, ReadHeader(native, 40));
            Assert.Equal(new byte[] { 0, 1, 1, 1 }, native.Colors.Skip(48));
            Assert.Equal(new byte[] { 0x30, 0x20, 0x10, 0x44, 0xcc, 0xbb, 0xaa, 0xdd }, native.PaletteColors.Take(8));
            Assert.Equal(128, native.PaletteColors.Length);
            Assert.Equal(new byte[] { 0x10, 0x20, 0x30, 0x44, 0xaa, 0xbb, 0xcc, 0xdd, 0xaa, 0xbb, 0xcc, 0xdd }, XboxNativeTextureCodec.Decode(native));
            native.Colors[48] = 32;
            Assert.Throws<ArgumentException>(() => XboxNativeTextureCodec.Decode(native));
        }

        /// <summary>Checks compression output size and both opaque endpoint colors across padded NPOT storage.</summary>
        [Theory]
        [InlineData("Xbox.DXT1", TextureAssetAlphaPrecision.Binary, 8)]
        [InlineData("Xbox.DXT3", TextureAssetAlphaPrecision.A4, 16)]
        [InlineData("Xbox.DXT5", TextureAssetAlphaPrecision.A8, 16)]
        public void Encode_DxtRetainsBlocksAndActualEndpointColors(string id, TextureAssetAlphaPrecision alpha, int length) {
            using TextureAsset source = CreateRgba(3, 2, new byte[] {
                255, 0, 0, 255, 0, 255, 0, 255, 255, 0, 0, 255, 0, 255, 0, 255, 255, 0, 0, 255, 0, 255, 0, 255 });
            using TextureAsset native = XboxNativeTextureCodec.Encode(source, id, alpha);
            Assert.Equal(4u, ReadHeader(native, 24));
            Assert.Equal(4u, ReadHeader(native, 28));
            Assert.Equal((uint)length, ReadHeader(native, 36));
            Assert.Equal(48 + length, native.Colors.Length);
            Assert.Equal(source.Colors, XboxNativeTextureCodec.Decode(native));
        }

        /// <summary>Decodes an independently authored BC1 block with all interpolation indices and its transparent three-color mode.</summary>
        [Fact]
        public void Decode_Dxt1GoldenInterpolationAndTransparentIndex() {
            using TextureAsset native = CreateDxt("Xbox.DXT1", TextureAssetAlphaPrecision.Binary);
            byte[] block = { 0x00, 0xf8, 0xe0, 0x07, 0xe4, 0xe4, 0xe4, 0xe4 };
            Array.Copy(block, 0, native.Colors, 48, block.Length);
            Assert.Equal(new byte[] { 255, 0, 0, 255, 0, 255, 0, 255, 170, 85, 0, 255, 85, 170, 0, 255 },
                XboxNativeTextureCodec.Decode(native).Take(16));
            native.Colors[48] = 0xe0; native.Colors[49] = 0x07; native.Colors[50] = 0x00; native.Colors[51] = 0xf8;
            Assert.Equal(new byte[] { 0, 255, 0, 255, 255, 0, 0, 255, 127, 127, 0, 255, 0, 0, 0, 0 },
                XboxNativeTextureCodec.Decode(native).Take(16));
        }

        /// <summary>Checks BC2 low-first alpha nibbles without trusting the DXT encoder's output.</summary>
        [Fact]
        public void Decode_Dxt3GoldenExplicitAlphaNibbles() {
            using TextureAsset native = CreateDxt("Xbox.DXT3", TextureAssetAlphaPrecision.A4);
            for (int pair = 0; pair < 8; pair++) native.Colors[48 + pair] = (byte)(pair * 2 | ((pair * 2 + 1) << 4));
            byte[] rgba = XboxNativeTextureCodec.Decode(native);
            Assert.Equal(Enumerable.Range(0, 16).Select(index => (byte)(index * 17)), Enumerable.Range(0, 16).Select(index => rgba[index * 4 + 3]));
        }

        /// <summary>Checks BC3 ascending alpha endpoints including the explicit zero and 255 palette entries.</summary>
        [Fact]
        public void Decode_Dxt5GoldenSixAlphaMode() {
            using TextureAsset native = CreateDxt("Xbox.DXT5", TextureAssetAlphaPrecision.A8);
            native.Colors[48] = 10;
            native.Colors[49] = 20;
            ulong indices = 0;
            for (int pixel = 0; pixel < 16; pixel++) indices |= (ulong)(pixel % 8) << (pixel * 3);
            for (int index = 0; index < 6; index++) native.Colors[50 + index] = (byte)(indices >> (index * 8));
            byte[] rgba = XboxNativeTextureCodec.Decode(native);
            Assert.Equal(new byte[] { 10, 20, 12, 14, 16, 18, 0, 255 }, Enumerable.Range(0, 8).Select(index => rgba[index * 4 + 3]));
        }

        /// <summary>Rejects mutated structural fields before addressing the GPU texel payload.</summary>
        [Theory]
        [InlineData(0)] [InlineData(4)] [InlineData(8)] [InlineData(12)] [InlineData(16)] [InlineData(20)]
        [InlineData(24)] [InlineData(28)] [InlineData(32)] [InlineData(36)] [InlineData(40)] [InlineData(44)]
        public void Decode_MalformedHeaderRejectsField(int offset) {
            using TextureAsset source = CreateRgba(1, 1, new byte[] { 1, 2, 3, 4 });
            using TextureAsset native = XboxNativeTextureCodec.Encode(source, "Xbox.Linear.A8R8G8B8", TextureAssetAlphaPrecision.A8);
            XboxNativeTexturePixelCodec.WriteUInt32(native.Colors, offset, uint.MaxValue);
            Assert.ThrowsAny<ArgumentException>(() => XboxNativeTextureCodec.Decode(native));
        }

        /// <summary>Rejects truncation, oversized dimensions, incompatible intrinsic alpha and unquantized P8 input.</summary>
        [Fact]
        public void EncodeAndDecode_RejectInvalidPayloadAndPolicy() {
            using TextureAsset source = CreateRgba(1, 1, new byte[] { 1, 2, 3, 4 });
            Assert.Throws<ArgumentException>(() => XboxNativeTextureCodec.Encode(source, "Xbox.Linear.AY8", TextureAssetAlphaPrecision.Opaque));
            Assert.Throws<ArgumentException>(() => XboxNativeTextureCodec.Encode(source, "Xbox.Swizzled.I8_A8R8G8B8", TextureAssetAlphaPrecision.A8));
            using TextureAsset native = XboxNativeTextureCodec.Encode(source, "Xbox.Linear.A8R8G8B8", TextureAssetAlphaPrecision.A8);
            native.Colors = native.Colors[..^1];
            Assert.Throws<ArgumentException>(() => XboxNativeTextureCodec.Decode(native));
            source.Width = 4097;
            Assert.Throws<ArgumentOutOfRangeException>(() => XboxNativeTextureCodec.Encode(source, "Xbox.Linear.A8R8G8B8", TextureAssetAlphaPrecision.A8));
        }

        /// <summary>Creates a stable identified RGBA source whose ownership and pixel bytes remain under test.</summary>
        static TextureAsset CreateRgba(ushort width, ushort height, byte[] colors) {
            return new TextureAsset { Id = "native-texture", RuntimeAssetId = 17, AuthoringAssetId = "authored-texture",
                FormerAuthoringAssetIds = new string[] { "former-texture" }, IsEngineOwned = true,
                Width = width, Height = height, ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = colors };
        }

        /// <summary>Creates a six-texel, two-entry generic palette fixture for the P8 catalog path.</summary>
        static TextureAsset CreateIndexedSource() {
            return new TextureAsset { Id = "native-texture", RuntimeAssetId = 17, AuthoringAssetId = "authored-texture",
                FormerAuthoringAssetIds = new string[] { "former-texture" }, Width = 3, Height = 2,
                ColorFormat = TextureAssetColorFormat.Indexed8, AlphaPrecision = TextureAssetAlphaPrecision.A8,
                Colors = new byte[] { 0, 1, 0, 1, 0, 1 }, PaletteColors = new byte[] { 255, 0, 0, 255, 0, 255, 0, 128 } };
        }

        /// <summary>Builds a valid compressed container before replacing its block with an independent golden fixture.</summary>
        static TextureAsset CreateDxt(string id, TextureAssetAlphaPrecision alpha) {
            using TextureAsset source = CreateRgba(4, 4, Enumerable.Range(0, 64).Select(index => (byte)(index % 4 == 3 ? 255 : 0)).ToArray());
            return XboxNativeTextureCodec.Encode(source, id, alpha);
        }

        /// <summary>Reads one test header field as little-endian independently of stream serialization metadata.</summary>
        static uint ReadHeader(TextureAsset texture, int offset) {
            return XboxNativeTexturePixelCodec.ReadUInt32(texture.Colors, offset);
        }
    }
}

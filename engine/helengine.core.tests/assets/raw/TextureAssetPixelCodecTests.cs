using helengine;

namespace helengine.core.tests.assets.raw {
    /// <summary>Checks independent packed-byte examples, malformed assets, alpha policies and legacy compatibility.</summary>
    public class TextureAssetPixelCodecTests {
        /// <summary>Protects persisted enum values and the explicit alpha capabilities used by editor selections.</summary>
        [Fact]
        public void EnumAndAlphaCapabilitiesPreserveLegacyAndRejectUnknownValues() {
            Assert.Equal(0, (byte)TextureAssetColorFormat.Rgba32);
            Assert.Equal(4, (byte)TextureAssetColorFormat.GxRgb5A3);
            Assert.Equal(5, (byte)TextureAssetColorFormat.Rgba5551);
            Assert.Equal(11, (byte)TextureAssetColorFormat.Yuv16);
            Assert.True(TextureAssetPixelCodec.IsAlphaPrecisionSupported(TextureAssetColorFormat.Rgba4444, TextureAssetAlphaPrecision.A8));
            Assert.False(TextureAssetPixelCodec.IsAlphaPrecisionSupported(TextureAssetColorFormat.GxRgb5A3, TextureAssetAlphaPrecision.A8));
            Assert.False(TextureAssetPixelCodec.IsAlphaPrecisionSupported((TextureAssetColorFormat)255, TextureAssetAlphaPrecision.A8));
            Assert.False(TextureAssetPixelCodec.IsAlphaPrecisionSupported(TextureAssetColorFormat.Rgba32, (TextureAssetAlphaPrecision)255));
            Assert.Equal(TextureAssetAlphaPrecision.Binary, TextureAssetPixelCodec.GetMaximumAlphaPrecision(TextureAssetColorFormat.Rgba5551));
            Assert.Equal(TextureAssetAlphaPrecision.A4, TextureAssetPixelCodec.GetMaximumAlphaPrecision(TextureAssetColorFormat.Ia8));
            Assert.Equal(TextureAssetAlphaPrecision.A8, TextureAssetPixelCodec.GetMaximumAlphaPrecision(TextureAssetColorFormat.Ia16));
            Assert.Equal(TextureAssetAlphaPrecision.A4, TextureAssetPixelCodec.GetMaximumAlphaPrecision(TextureAssetColorFormat.I4));
            Assert.Equal(TextureAssetAlphaPrecision.A8, TextureAssetPixelCodec.GetMaximumAlphaPrecision(TextureAssetColorFormat.I8));
            Assert.Equal(TextureAssetAlphaPrecision.Opaque, TextureAssetPixelCodec.GetMaximumAlphaPrecision(TextureAssetColorFormat.Yuv16));
            Assert.Throws<NotSupportedException>(() => TextureAssetPixelCodec.GetMaximumAlphaPrecision(TextureAssetColorFormat.GxRgb5A3));
        }

        /// <summary>RGBA5551 stores red in the high word bits, alpha in bit zero, and words in little-endian order.</summary>
        [Fact]
        public void Rgba5551GoldenBytesAndBinaryThresholdAreExact() {
            TextureAsset source = Rgba(3, 1, [255, 0, 0, 127, 0, 255, 0, 128, 0, 0, 255, 255]);
            TextureAsset packed = TextureAssetPixelCodec.EncodeFromRgba32(source, TextureAssetColorFormat.Rgba5551, TextureAssetAlphaPrecision.Binary);
            Assert.Equal(new byte[] { 0x00, 0xF8, 0xC1, 0x07, 0x3F, 0x00 }, packed.Colors);
            Assert.Equal(new byte[] { 255, 0, 0, 0, 0, 255, 0, 255, 0, 0, 255, 255 }, TextureAssetPixelCodec.DecodeToRgba32(packed));
            TextureAsset opaque = TextureAssetPixelCodec.EncodeFromRgba32(source, TextureAssetColorFormat.Rgba5551, TextureAssetAlphaPrecision.Opaque);
            Assert.Equal((byte)1, opaque.Colors[0]);
            Assert.Equal((byte)127, source.Colors[3]);
        }

        /// <summary>IA4 places I3 above A1 and pads the last nibble separately for each odd-width row.</summary>
        [Fact]
        public void Ia4GoldenOddRowsDoNotConsumePreviousRowPadding() {
            TextureAsset source = Rgba(3, 2, [255, 255, 255, 255, 0, 0, 0, 0, 128, 128, 128, 128,
                0, 0, 0, 255, 255, 255, 255, 0, 32, 32, 32, 127]);
            TextureAsset packed = TextureAssetPixelCodec.EncodeFromRgba32(source, TextureAssetColorFormat.Ia4, TextureAssetAlphaPrecision.Binary);
            Assert.Equal(new byte[] { 0xF0, 0x90, 0x1E, 0x20 }, packed.Colors);
            Assert.Equal(new byte[] { 255, 255, 255, 255, 0, 0, 0, 0, 146, 146, 146, 255,
                0, 0, 0, 255, 255, 255, 255, 0, 36, 36, 36, 0 }, TextureAssetPixelCodec.DecodeToRgba32(packed));
        }

        /// <summary>IA8 uses intensity in the high nibble; IA16 uses a high intensity byte in a little-endian word.</summary>
        [Fact]
        public void Ia8AndIa16GoldenChannelsAreInTheDocumentedOrder() {
            TextureAsset ia8 = TextureAssetPixelCodec.EncodeFromRgba32(Rgba(1, 1, [128, 128, 128, 127]),
                TextureAssetColorFormat.Ia8, TextureAssetAlphaPrecision.A4);
            Assert.Equal(new byte[] { 0x87 }, ia8.Colors);
            Assert.Equal(new byte[] { 136, 136, 136, 119 }, TextureAssetPixelCodec.DecodeToRgba32(ia8));
            TextureAsset ia16 = TextureAssetPixelCodec.EncodeFromRgba32(Rgba(1, 1, [100, 100, 100, 200]),
                TextureAssetColorFormat.Ia16, TextureAssetAlphaPrecision.A8);
            Assert.Equal(new byte[] { 200, 100 }, ia16.Colors);
            Assert.Equal(new byte[] { 100, 100, 100, 200 }, TextureAssetPixelCodec.DecodeToRgba32(ia16));
        }

        /// <summary>I4 stores high-first row-aligned intensity and decodes that intensity into alpha as well as RGB.</summary>
        [Fact]
        public void IntensityGoldenRowsAndRec601LumaIgnoreIndependentSourceAlpha() {
            TextureAsset i4 = TextureAssetPixelCodec.EncodeFromRgba32(Rgba(3, 2,
                [16, 16, 16, 0, 255, 255, 255, 0, 128, 128, 128, 255, 0, 0, 0, 255, 32, 32, 32, 255, 64, 64, 64, 0]),
                TextureAssetColorFormat.I4, TextureAssetAlphaPrecision.A4);
            Assert.Equal(new byte[] { 0x1F, 0x80, 0x02, 0x40 }, i4.Colors);
            Assert.Equal(new byte[] { 17, 17, 17, 17, 255, 255, 255, 255, 136, 136, 136, 136,
                0, 0, 0, 0, 34, 34, 34, 34, 68, 68, 68, 68 }, TextureAssetPixelCodec.DecodeToRgba32(i4));
            TextureAsset i8 = TextureAssetPixelCodec.EncodeFromRgba32(Rgba(3, 1,
                [255, 0, 0, 0, 0, 255, 0, 255, 0, 0, 255, 128]), TextureAssetColorFormat.I8, TextureAssetAlphaPrecision.A8);
            Assert.Equal(new byte[] { 76, 150, 29 }, i8.Colors);
            Assert.Equal(new byte[] { 76, 76, 76, 76, 150, 150, 150, 150, 29, 29, 29, 29 }, TextureAssetPixelCodec.DecodeToRgba32(i8));
        }

        /// <summary>Limited-range YUYV black/white and red vectors fix byte ordering and chroma conversion independently.</summary>
        [Fact]
        public void YuvGoldenVectorsUseLimitedRangeAndOpaqueAlpha() {
            TextureAsset neutral = TextureAssetPixelCodec.EncodeFromRgba32(Rgba(2, 1,
                [0, 0, 0, 0, 255, 255, 255, 128]), TextureAssetColorFormat.Yuv16, TextureAssetAlphaPrecision.Opaque);
            Assert.Equal(new byte[] { 16, 128, 235, 128 }, neutral.Colors);
            Assert.Equal(new byte[] { 0, 0, 0, 255, 255, 255, 255, 255 }, TextureAssetPixelCodec.DecodeToRgba32(neutral));
            TextureAsset red = TextureAssetPixelCodec.EncodeFromRgba32(Rgba(2, 1,
                [255, 0, 0, 0, 255, 0, 0, 0]), TextureAssetColorFormat.Yuv16, TextureAssetAlphaPrecision.Opaque);
            Assert.Equal(new byte[] { 81, 90, 81, 240 }, red.Colors);
            Assert.Equal(new byte[] { 254, 0, 0, 255, 254, 0, 0, 255 }, TextureAssetPixelCodec.DecodeToRgba32(red));
        }

        /// <summary>Odd YUV widths repeat their final pixel within each row instead of pairing with the next row.</summary>
        [Fact]
        public void YuvOddRowsHaveTheirOwnPaddedPairs() {
            TextureAsset packed = TextureAssetPixelCodec.EncodeFromRgba32(Rgba(3, 2,
                [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                 255, 255, 255, 0, 255, 255, 255, 0, 255, 255, 255, 0]),
                TextureAssetColorFormat.Yuv16, TextureAssetAlphaPrecision.Opaque);
            Assert.Equal(new byte[] { 16, 128, 16, 128, 16, 128, 16, 128,
                235, 128, 235, 128, 235, 128, 235, 128 }, packed.Colors);
            Assert.Equal(24, TextureAssetPixelCodec.DecodeToRgba32(packed).Length);
        }

        /// <summary>Legacy RGBA4444 red-low words and Indexed4 low-first continuous pixels keep their serialized meanings.</summary>
        [Fact]
        public void LegacyPackedAndIndexedLayoutsRemainUnchanged() {
            TextureAsset legacy = Packed(1, 1, TextureAssetColorFormat.Rgba4444, TextureAssetAlphaPrecision.A4, [0x21, 0x43]);
            Assert.Equal(new byte[] { 17, 34, 51, 68 }, TextureAssetPixelCodec.DecodeToRgba32(legacy));
            TextureAsset indexed4 = Packed(1, 3, TextureAssetColorFormat.Indexed4, TextureAssetAlphaPrecision.A8, [0x10, 0x00]);
            indexed4.PaletteColors = [11, 12, 13, 14, 21, 22, 23, 24];
            Assert.Equal(new byte[] { 11, 12, 13, 14, 21, 22, 23, 24, 11, 12, 13, 14 }, TextureAssetPixelCodec.DecodeToRgba32(indexed4));
            TextureAsset indexed8 = Packed(2, 1, TextureAssetColorFormat.Indexed8, TextureAssetAlphaPrecision.Binary, [1, 0]);
            indexed8.PaletteColors = indexed4.PaletteColors;
            Assert.Equal(new byte[] { 21, 22, 23, 24, 11, 12, 13, 14 }, TextureAssetPixelCodec.DecodeToRgba32(indexed8));
            TextureAsset rgba = Rgba(1, 1, [1, 2, 3, 4]);
            byte[] decodedRgba = TextureAssetPixelCodec.DecodeToRgba32(rgba);
            Assert.NotSame(rgba.Colors, decodedRgba);
            Assert.Equal(rgba.Colors, decodedRgba);
            decodedRgba[0] = 255;
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, rgba.Colors);
            Assert.Equal(new byte[] { 0x10, 0x00 }, indexed4.Colors);
        }

        /// <summary>Encoded assets retain every identity while owning separate pixels and former-identity containers.</summary>
        [Fact]
        public void EncodingPreservesIdentityAndDoesNotMutateOrBorrowSourceBuffers() {
            TextureAsset source = Rgba(1, 1, [255, 0, 0, 127]);
            source.Id = "texture-id";
            source.RuntimeAssetId = 9001;
            source.AuthoringAssetId = "authoring-id";
            source.FormerAuthoringAssetIds = ["former-id"];
            source.IsEngineOwned = true;
            TextureAsset result = TextureAssetPixelCodec.EncodeFromRgba32(source, TextureAssetColorFormat.Rgba5551, TextureAssetAlphaPrecision.Binary);
            Assert.Equal(source.Id, result.Id);
            Assert.Equal(source.RuntimeAssetId, result.RuntimeAssetId);
            Assert.Equal(source.AuthoringAssetId, result.AuthoringAssetId);
            Assert.Equal(source.FormerAuthoringAssetIds, result.FormerAuthoringAssetIds);
            Assert.NotSame(source.FormerAuthoringAssetIds, result.FormerAuthoringAssetIds);
            Assert.True(result.IsEngineOwned);
            Assert.NotSame(source.Colors, result.Colors);
            Assert.Null(result.PaletteColors);
            Assert.Equal(new byte[] { 255, 0, 0, 127 }, source.Colors);
        }

        /// <summary>Exact byte counts distinguish row padding from continuous packing and reject overflow before allocating.</summary>
        [Fact]
        public void PayloadLengthChecksUseExactDimensionsAndRejectOverflow() {
            Assert.Equal(5, TextureAssetPixelCodec.GetPixelByteLength(TextureAssetColorFormat.Indexed4, 3, 3));
            Assert.Equal(6, TextureAssetPixelCodec.GetPixelByteLength(TextureAssetColorFormat.I4, 3, 3));
            Assert.Equal(6, TextureAssetPixelCodec.GetPixelByteLength(TextureAssetColorFormat.Ia4, 3, 3));
            Assert.Equal(24, TextureAssetPixelCodec.GetPixelByteLength(TextureAssetColorFormat.Yuv16, 3, 3));
            Assert.Equal(18, TextureAssetPixelCodec.GetPixelByteLength(TextureAssetColorFormat.Ia16, 3, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => TextureAssetPixelCodec.GetPixelByteLength(TextureAssetColorFormat.I8, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => TextureAssetPixelCodec.GetPixelByteLength(TextureAssetColorFormat.Rgba32, int.MaxValue, int.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => TextureAssetPixelCodec.GetPixelByteLength(TextureAssetColorFormat.Rgba32, 65535, 65535));
            Assert.Throws<NotSupportedException>(() => TextureAssetPixelCodec.GetPixelByteLength(TextureAssetColorFormat.GxRgb5A3, 4, 4));
            Assert.Throws<NotSupportedException>(() => TextureAssetPixelCodec.GetPixelByteLength((TextureAssetColorFormat)255, 1, 1));
            Assert.Throws<ArgumentNullException>(() => TextureAssetPixelCodec.DecodeToRgba32(null));
            Assert.Throws<ArgumentException>(() => TextureAssetPixelCodec.DecodeToRgba32(Packed(1, 1, TextureAssetColorFormat.Rgba5551, TextureAssetAlphaPrecision.Binary, [0])));
            Assert.Throws<ArgumentException>(() => TextureAssetPixelCodec.DecodeToRgba32(Rgba(1, 1, [1, 2, 3])));
        }

        /// <summary>Missing, excessive, malformed and out-of-range palettes cannot be decoded as plausible colors.</summary>
        [Fact]
        public void IndexedPaletteShapeAndEveryReferencedIndexAreValidated() {
            TextureAsset asset = Packed(1, 1, TextureAssetColorFormat.Indexed8, TextureAssetAlphaPrecision.A8, [1]);
            Assert.Throws<ArgumentException>(() => TextureAssetPixelCodec.DecodeToRgba32(asset));
            asset.PaletteColors = [0, 0, 0];
            Assert.Throws<ArgumentException>(() => TextureAssetPixelCodec.DecodeToRgba32(asset));
            asset.PaletteColors = [0, 0, 0, 255];
            Assert.Throws<ArgumentException>(() => TextureAssetPixelCodec.DecodeToRgba32(asset));
            asset.PaletteColors = new byte[257 * 4];
            Assert.Throws<ArgumentException>(() => TextureAssetPixelCodec.DecodeToRgba32(asset));
            TextureAsset indexed4 = Packed(1, 1, TextureAssetColorFormat.Indexed4, TextureAssetAlphaPrecision.A8, [0x01]);
            indexed4.PaletteColors = [0, 0, 0, 255];
            Assert.Throws<ArgumentException>(() => TextureAssetPixelCodec.DecodeToRgba32(indexed4));
        }

        /// <summary>Encoding and decoding enforce alpha compatibility instead of silently dropping unsupported precision.</summary>
        [Theory]
        [InlineData(TextureAssetColorFormat.Rgba5551, TextureAssetAlphaPrecision.A8)]
        [InlineData(TextureAssetColorFormat.Ia4, TextureAssetAlphaPrecision.A4)]
        [InlineData(TextureAssetColorFormat.Ia8, TextureAssetAlphaPrecision.A8)]
        [InlineData(TextureAssetColorFormat.I4, TextureAssetAlphaPrecision.Opaque)]
        [InlineData(TextureAssetColorFormat.I4, TextureAssetAlphaPrecision.A8)]
        [InlineData(TextureAssetColorFormat.I8, TextureAssetAlphaPrecision.A4)]
        [InlineData(TextureAssetColorFormat.Yuv16, TextureAssetAlphaPrecision.Binary)]
        public void IncompatibleAlphaIsRejectedForEncodeAndDecode(TextureAssetColorFormat format, TextureAssetAlphaPrecision alpha) {
            Assert.False(TextureAssetPixelCodec.IsAlphaPrecisionSupported(format, alpha));
            Assert.Throws<ArgumentException>(() => TextureAssetPixelCodec.EncodeFromRgba32(Rgba(1, 1, [0, 0, 0, 0]), format, alpha));
            TextureAsset packed = Packed(1, 1, format, alpha, new byte[TextureAssetPixelCodec.GetPixelByteLength(format, 1, 1)]);
            Assert.Throws<ArgumentException>(() => TextureAssetPixelCodec.DecodeToRgba32(packed));
        }

        /// <summary>Creates an RGBA32 fixture whose payload can be replaced independently of the codec.</summary>
        static TextureAsset Rgba(ushort width, ushort height, byte[] colors) {
            return Packed(width, height, TextureAssetColorFormat.Rgba32, TextureAssetAlphaPrecision.A8, colors);
        }

        /// <summary>Creates a raw packed fixture, including deliberately invalid ones used by bounds tests.</summary>
        static TextureAsset Packed(ushort width, ushort height, TextureAssetColorFormat format, TextureAssetAlphaPrecision alpha, byte[] colors) {
            return new TextureAsset { Width = width, Height = height, ColorFormat = format, AlphaPrecision = alpha, Colors = colors };
        }
    }
}

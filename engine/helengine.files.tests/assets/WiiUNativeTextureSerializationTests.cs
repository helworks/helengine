using helengine;
using helengine.files.tests.assets.font;

namespace helengine.files.tests.assets {
    /// <summary>Checks actual texture and font readers preserve canonical GX2 bytes under either outer metadata byte order.</summary>
    public sealed class WiiUNativeTextureSerializationTests {
        /// <summary>Retains native scalar, packed, compressed and raw-only payloads through both texture readers.</summary>
        [Theory]
        [InlineData("WiiU.UNORM_R8_G8_B8_A8", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.LittleEndian)]
        [InlineData("WiiU.UNORM_R8_G8_B8_A8", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.BigEndian)]
        [InlineData("WiiU.UNORM_R10_G10_B10_A2", TextureAssetAlphaPrecision.A2, EngineBinaryEndianness.LittleEndian)]
        [InlineData("WiiU.UNORM_R10_G10_B10_A2", TextureAssetAlphaPrecision.A2, EngineBinaryEndianness.BigEndian)]
        [InlineData("WiiU.UNORM_A2_B10_G10_R10", TextureAssetAlphaPrecision.A2, EngineBinaryEndianness.LittleEndian)]
        [InlineData("WiiU.UNORM_A2_B10_G10_R10", TextureAssetAlphaPrecision.A2, EngineBinaryEndianness.BigEndian)]
        [InlineData("WiiU.SRGB_BC3", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.LittleEndian)]
        [InlineData("WiiU.SRGB_BC3", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.BigEndian)]
        [InlineData("WiiU.UINT_R32", TextureAssetAlphaPrecision.Opaque, EngineBinaryEndianness.LittleEndian)]
        [InlineData("WiiU.UINT_R32", TextureAssetAlphaPrecision.Opaque, EngineBinaryEndianness.BigEndian)]
        [InlineData("WiiU.UNORM_NV12", TextureAssetAlphaPrecision.Opaque, EngineBinaryEndianness.LittleEndian)]
        [InlineData("WiiU.UNORM_NV12", TextureAssetAlphaPrecision.Opaque, EngineBinaryEndianness.BigEndian)]
        public void TextureReaders_RetainExactNativeGpuBytes(string id, TextureAssetAlphaPrecision alpha, EngineBinaryEndianness endian) {
            Assert.True(WiiUNativeTextureFormatCatalog.TryGetFormat(id, out var format)); using TextureAsset source = Source(); WiiUNativeTextureLayout layout = new(format, source.Width, source.Height);
            using TextureAsset native = format.SupportsCooking ? WiiUNativeTextureCodec.Encode(source, id, alpha) : WiiUNativeTextureCodec.WrapRaw(source, id, alpha, Enumerable.Range(0, layout.TexelLength).Select(value => (byte)value).ToArray());
            using MemoryStream stream = TextureRecord(native, endian); using TextureAsset editor = Assert.IsType<TextureAsset>(EditorAssetBinarySerializer.Deserialize(stream)); AssertPayload(native, editor);
            stream.Position = 0; using TextureAsset packaged = Assert.IsType<TextureAsset>(PackagedAssetBinarySerializer.Deserialize(stream)); AssertPayload(native, packaged);
            Assert.Equal(new byte[] { 0x57, 0x47, 0x54, 0x31 }, packaged.Colors.Take(4));
            if (format.SupportsPreview) Assert.Equal(WiiUNativeTextureCodec.Decode(native), TextureAssetPixelCodec.DecodeToRgba32(packaged));
            else Assert.Throws<NotSupportedException>(() => TextureAssetPixelCodec.DecodeToRgba32(packaged));
        }
        /// <summary>Preserves native atlas coverage including two-bit alpha and sRGB transfer through actual font readers.</summary>
        [Theory]
        [InlineData("WiiU.UNORM_R8_G8_B8_A8", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.LittleEndian)]
        [InlineData("WiiU.UNORM_R8_G8_B8_A8", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.BigEndian)]
        [InlineData("WiiU.UNORM_A2_B10_G10_R10", TextureAssetAlphaPrecision.A2, EngineBinaryEndianness.LittleEndian)]
        [InlineData("WiiU.UNORM_A2_B10_G10_R10", TextureAssetAlphaPrecision.A2, EngineBinaryEndianness.BigEndian)]
        [InlineData("WiiU.SRGB_R8_G8_B8_A8", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.LittleEndian)]
        [InlineData("WiiU.SRGB_R8_G8_B8_A8", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.BigEndian)]
        public void FontReaders_RetainNativeAtlasCoverage(string id, TextureAssetAlphaPrecision alpha, EngineBinaryEndianness endian) {
            using TextureAsset source = Source(); using TextureAsset native = WiiUNativeTextureCodec.Encode(source, id, alpha); using MemoryStream stream = FontRecord(native, endian);
            using FontAsset editor = helengine.files.FontAssetBinarySerializer.Deserialize(stream); AssertPayload(native, editor.SourceTextureAsset);
            stream.Position = 0; using NativeAtlasRenderManager renderer = new(); using FontAsset runtime = helengine.FontAssetBinarySerializer.Deserialize(stream, renderer); AssertPayload(native, runtime.SourceTextureAsset);
            Assert.Equal(WiiUNativeTextureCodec.Decode(native), renderer.DecodedPixels); Assert.Equal(new byte[] { 0, 85, 170, 255 }, renderer.DecodedPixels.Where((value, index) => index % 4 == 3));
        }
        /// <summary>Checks acceptance of native A2 does not expand the generic format's alpha contract.</summary>
        [Fact]
        public void TextureReaders_RejectGenericA2() {
            using TextureAsset source = Source(); source.AlphaPrecision = TextureAssetAlphaPrecision.A2; using MemoryStream stream = TextureRecord(source, EngineBinaryEndianness.BigEndian);
            Assert.ThrowsAny<Exception>(() => EditorAssetBinarySerializer.Deserialize(stream)); stream.Position = 0; Assert.ThrowsAny<Exception>(() => PackagedAssetBinarySerializer.Deserialize(stream));
        }
        /// <summary>Creates independent white coverage pixels with each real two-bit alpha level.</summary>
        static TextureAsset Source() { return new TextureAsset { Width = 4, Height = 1, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = new byte[] { 255, 255, 255, 0, 255, 255, 255, 85, 255, 255, 255, 170, 255, 255, 255, 255 } }; }
        /// <summary>Uses the actual texture writer under an explicit metadata byte-order header.</summary>
        static MemoryStream TextureRecord(TextureAsset asset, EngineBinaryEndianness endian) {
            MemoryStream stream = new(); EngineBinaryHeaderSerializer.Write(stream, new EngineBinaryHeader(endian, 25, EditorAssetBinarySerializer.FormatId, (ushort)EditorAssetBinarySerializer.RecordKind, (ushort)EditorAssetBinaryValueKind.TextureAsset));
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, endian)) new TextureAssetPayloadSerializer().Write(writer, asset);
            stream.Position = 0; return stream;
        }
        /// <summary>Writes an independent minimal version-five native atlas record.</summary>
        static MemoryStream FontRecord(TextureAsset asset, EngineBinaryEndianness endian) {
            MemoryStream stream = new(); EngineBinaryHeaderSerializer.Write(stream, new EngineBinaryHeader(endian, 5, helengine.files.FontAssetBinarySerializer.FormatId, (ushort)helengine.files.FontAssetBinarySerializer.RecordKind, helengine.files.FontAssetBinarySerializer.ValueKind));
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, endian)) { writer.WriteString(string.Empty); writer.WriteInt64(24); writer.WriteUInt16(asset.Width); writer.WriteUInt16(asset.Height); writer.WriteByte((byte)asset.ColorFormat); writer.WriteByte((byte)asset.AlphaPrecision); writer.WriteByteArray(asset.PaletteColors); writer.WriteByteArray(asset.Colors); writer.WriteString("Wii U native font"); writer.WriteInt32(12); writer.WriteSingle(4); writer.WriteSingle(12); writer.WriteInt32(asset.Width); writer.WriteInt32(asset.Height); writer.WriteInt32(0); }
            stream.Position = 0; return stream;
        }
        /// <summary>Checks native enum, alpha, dimensions and opaque native buffers without reinterpreting their byte order.</summary>
        static void AssertPayload(TextureAsset expected, TextureAsset actual) { Assert.Equal(TextureAssetColorFormat.WiiUNative, actual.ColorFormat); Assert.Equal(expected.AlphaPrecision, actual.AlphaPrecision); Assert.Equal(expected.Width, actual.Width); Assert.Equal(expected.Height, actual.Height); Assert.Equal(expected.Colors, actual.Colors); Assert.Null(actual.PaletteColors); }
    }
}

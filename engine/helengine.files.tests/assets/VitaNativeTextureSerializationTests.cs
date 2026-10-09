using helengine;
using helengine.files.tests.assets.font;

namespace helengine.files.tests.assets {
    /// <summary>Verifies VGT1 bytes and intrinsic alpha through actual texture and font readers in both outer byte orders.</summary>
    public sealed class VitaNativeTextureSerializationTests {
        /// <summary>Preserves two-bit and eight-bit native channels through the editor writer and both texture readers.</summary>
        [Theory]
        [InlineData("Vita.U8U8U8U8_ABGR", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.LittleEndian)]
        [InlineData("Vita.U8U8U8U8_ABGR", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.BigEndian)]
        [InlineData("Vita.U2U10U10U10_ABGR", TextureAssetAlphaPrecision.A2, EngineBinaryEndianness.LittleEndian)]
        [InlineData("Vita.U2U10U10U10_ABGR", TextureAssetAlphaPrecision.A2, EngineBinaryEndianness.BigEndian)]
        [InlineData("Vita.P4_ARGB", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.LittleEndian)]
        [InlineData("Vita.P4_ARGB", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.BigEndian)]
        public void TextureReaders_RetainNativeGpuBytesAndPreview(string format, TextureAssetAlphaPrecision alpha, EngineBinaryEndianness endian) {
            using TextureAsset source = Source(format.StartsWith("Vita.P4")); using TextureAsset native = VitaNativeTextureCodec.Encode(source, format, alpha);
            using MemoryStream stream = TextureRecord(native, endian);
            using TextureAsset editor = Assert.IsType<TextureAsset>(EditorAssetBinarySerializer.Deserialize(stream));
            AssertPayload(native, editor); stream.Position = 0;
            using TextureAsset packaged = Assert.IsType<TextureAsset>(PackagedAssetBinarySerializer.Deserialize(stream));
            AssertPayload(native, packaged);
            Assert.Equal(new byte[] { 0x56, 0x47, 0x54, 0x31 }, packaged.Colors.Take(4));
            Assert.Equal(VitaNativeTextureCodec.Decode(native), TextureAssetPixelCodec.DecodeToRgba32(packaged));
        }
        /// <summary>Retains native font atlas bytes and decoded coverage through the actual font readers in either byte order.</summary>
        [Theory]
        [InlineData("Vita.U8U8U8U8_ABGR", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.LittleEndian)]
        [InlineData("Vita.U8U8U8U8_ABGR", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.BigEndian)]
        [InlineData("Vita.U2U10U10U10_ABGR", TextureAssetAlphaPrecision.A2, EngineBinaryEndianness.LittleEndian)]
        [InlineData("Vita.U2U10U10U10_ABGR", TextureAssetAlphaPrecision.A2, EngineBinaryEndianness.BigEndian)]
        [InlineData("Vita.U8_R111", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.LittleEndian)]
        [InlineData("Vita.U8_R111", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.BigEndian)]
        public void FontReaders_RetainNativeAtlasAndCoverage(string format, TextureAssetAlphaPrecision alpha, EngineBinaryEndianness endian) {
            using TextureAsset source = Source(false); using TextureAsset atlas = VitaNativeTextureCodec.Encode(source, format, alpha); using MemoryStream stream = FontRecord(atlas, endian);
            using FontAsset editor = helengine.files.FontAssetBinarySerializer.Deserialize(stream); AssertPayload(atlas, editor.SourceTextureAsset);
            stream.Position = 0; using NativeAtlasRenderManager renderer = new();
            using FontAsset runtime = helengine.FontAssetBinarySerializer.Deserialize(stream, renderer); AssertPayload(atlas, runtime.SourceTextureAsset);
            Assert.Equal(VitaNativeTextureCodec.Decode(atlas), renderer.DecodedPixels);
            Assert.Equal(new byte[] { 0, 85, 170, 255 }, renderer.DecodedPixels.Where((value, index) => index % 4 == 3));
        }
        /// <summary>Checks reader acceptance does not expand A2 to unrelated native formats or generic texture records.</summary>
        [Theory]
        [InlineData(EngineBinaryEndianness.LittleEndian)]
        [InlineData(EngineBinaryEndianness.BigEndian)]
        public void TextureReaders_RejectGenericA2Metadata(EngineBinaryEndianness endian) {
            using TextureAsset source = Source(false); source.AlphaPrecision = TextureAssetAlphaPrecision.A2; using MemoryStream stream = TextureRecord(source, endian);
            Assert.ThrowsAny<Exception>(() => EditorAssetBinarySerializer.Deserialize(stream)); stream.Position = 0;
            Assert.ThrowsAny<Exception>(() => PackagedAssetBinarySerializer.Deserialize(stream));
        }
        /// <summary>Constructs independent coverage levels or a two-entry generic indexed source.</summary>
        static TextureAsset Source(bool indexed) {
            return new TextureAsset { Id = "vita-readers", Width = 4, Height = 1, ColorFormat = indexed ? TextureAssetColorFormat.Indexed4 : TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8,
                Colors = indexed ? new byte[] { 0x10, 0x10 } : new byte[] { 255, 255, 255, 0, 255, 255, 255, 85, 255, 255, 255, 170, 255, 255, 255, 255 },
                PaletteColors = indexed ? new byte[] { 1, 2, 3, 85, 4, 5, 6, 170 } : null };
        }
        /// <summary>Uses the actual texture payload writer under an explicit metadata byte-order header.</summary>
        static MemoryStream TextureRecord(TextureAsset texture, EngineBinaryEndianness endian) {
            MemoryStream stream = new(); EngineBinaryHeaderSerializer.Write(stream, new EngineBinaryHeader(endian, 25, EditorAssetBinarySerializer.FormatId, (ushort)EditorAssetBinarySerializer.RecordKind, (ushort)EditorAssetBinaryValueKind.TextureAsset));
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, endian)) new TextureAssetPayloadSerializer().Write(writer, texture);
            stream.Position = 0; return stream;
        }
        /// <summary>Writes a minimal version-five native atlas record independently of its readers.</summary>
        static MemoryStream FontRecord(TextureAsset atlas, EngineBinaryEndianness endian) {
            MemoryStream stream = new(); EngineBinaryHeaderSerializer.Write(stream, new EngineBinaryHeader(endian, 5, helengine.files.FontAssetBinarySerializer.FormatId, (ushort)helengine.files.FontAssetBinarySerializer.RecordKind, helengine.files.FontAssetBinarySerializer.ValueKind));
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, endian)) {
                writer.WriteString(string.Empty); writer.WriteInt64(24); writer.WriteUInt16(atlas.Width); writer.WriteUInt16(atlas.Height); writer.WriteByte((byte)atlas.ColorFormat); writer.WriteByte((byte)atlas.AlphaPrecision); writer.WriteByteArray(atlas.PaletteColors); writer.WriteByteArray(atlas.Colors);
                writer.WriteString("Vita native coverage"); writer.WriteInt32(12); writer.WriteSingle(4); writer.WriteSingle(12); writer.WriteInt32(atlas.Width); writer.WriteInt32(atlas.Height); writer.WriteInt32(0);
            }
            stream.Position = 0; return stream;
        }
        /// <summary>Checks native arrays and metadata without allowing palette or channel reinterpretation.</summary>
        static void AssertPayload(TextureAsset source, TextureAsset actual) {
            Assert.Equal(TextureAssetColorFormat.VitaNative, actual.ColorFormat); Assert.Equal(source.AlphaPrecision, actual.AlphaPrecision); Assert.Equal(source.Width, actual.Width); Assert.Equal(source.Height, actual.Height);
            Assert.Equal(source.Colors, actual.Colors); Assert.Equal(source.PaletteColors, actual.PaletteColors);
        }
    }
}

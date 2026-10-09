using helengine;

namespace helengine.files.tests.assets {
    /// <summary>Checks that file and runtime readers retain GX texels, CLUT data and native three-bit atlas coverage.</summary>
    public sealed class GxNativeAssetSerializationTests {
        /// <summary>GXN1 metadata remains little endian and GX texels remain big endian inside either HELE byte order.</summary>
        /// <param name="endianness">Outer HELE metadata byte order.</param>
        /// <param name="indexed">Whether the native payload includes a TLUT.</param>
        [Theory]
        [InlineData(EngineBinaryEndianness.LittleEndian, false)]
        [InlineData(EngineBinaryEndianness.BigEndian, false)]
        [InlineData(EngineBinaryEndianness.LittleEndian, true)]
        [InlineData(EngineBinaryEndianness.BigEndian, true)]
        public void NativePayloadSurvivesBothTextureReaders(EngineBinaryEndianness endianness, bool indexed) {
            using TextureAsset source = CreateSource(indexed);
            using TextureAsset native = GxNativeTextureCodec.Encode(source,
                indexed ? "Gx.CI4.TLUT.RGB5A3" : "Gx.RGB5A3", TextureAssetAlphaPrecision.A3);
            byte[] expected = GxNativeTextureCodec.Decode(native);
            using MemoryStream stream = CreateTextureRecord(native, endianness);
            using TextureAsset editor = Assert.IsType<TextureAsset>(EditorAssetBinarySerializer.Deserialize(stream));
            AssertNativeTexture(native, editor, expected);
            stream.Position = 0;
            using TextureAsset runtime = Assert.IsType<TextureAsset>(PackagedAssetBinarySerializer.Deserialize(stream));
            AssertNativeTexture(native, runtime, expected);
            Assert.Equal(stream.Length, stream.Position);
        }

        /// <summary>Embedded native atlases retain their GPU bytes and three-bit coverage through both font readers.</summary>
        /// <param name="endianness">Outer font metadata byte order.</param>
        [Theory]
        [InlineData(EngineBinaryEndianness.LittleEndian)]
        [InlineData(EngineBinaryEndianness.BigEndian)]
        public void NativeAtlasSurvivesBothFontReaders(EngineBinaryEndianness endianness) {
            using TextureAsset source = CreateSource(false);
            using TextureAsset native = GxNativeTextureCodec.Encode(source, "Gx.RGB5A3", TextureAssetAlphaPrecision.A3);
            byte[] expected = GxNativeTextureCodec.Decode(native);
            using MemoryStream stream = CreateFontRecord(native, endianness);
            using FontAsset editor = helengine.files.FontAssetBinarySerializer.Deserialize(stream);
            AssertNativeTexture(native, editor.SourceTextureAsset, expected);
            stream.Position = 0;
            using font.NativeAtlasRenderManager renderer = new();
            using FontAsset runtime = helengine.FontAssetBinarySerializer.Deserialize(stream, renderer);
            AssertNativeTexture(native, runtime.SourceTextureAsset, expected);
            Assert.Equal(expected, renderer.DecodedPixels);
        }

        /// <summary>Three-bit alpha cannot be silently accepted in an unrelated generic texture or atlas.</summary>
        /// <param name="endianness">Outer binary metadata byte order.</param>
        [Theory]
        [InlineData(EngineBinaryEndianness.LittleEndian)]
        [InlineData(EngineBinaryEndianness.BigEndian)]
        public void GenericRecordsRejectNativeOnlyThreeBitAlpha(EngineBinaryEndianness endianness) {
            using TextureAsset source = CreateSource(false);
            source.AlphaPrecision = TextureAssetAlphaPrecision.A3;
            using MemoryStream texture = CreateTextureRecord(source, endianness);
            Assert.Throws<InvalidOperationException>(() => EditorAssetBinarySerializer.Deserialize(texture));
            texture.Position = 0;
            Assert.Throws<InvalidOperationException>(() => PackagedAssetBinarySerializer.Deserialize(texture));
            using MemoryStream atlas = CreateFontRecord(source, endianness);
            Assert.Throws<InvalidOperationException>(() => helengine.files.FontAssetBinarySerializer.Deserialize(atlas));
            atlas.Position = 0;
            using font.NativeAtlasRenderManager renderer = new();
            Assert.Throws<InvalidOperationException>(() => helengine.FontAssetBinarySerializer.Deserialize(atlas, renderer));
        }

        /// <summary>Builds a small independent color or indexed input with visible and transparent samples.</summary>
        /// <param name="indexed">Selects continuous low-nibble generic indexed storage.</param>
        /// <returns>Owned source texture.</returns>
        static TextureAsset CreateSource(bool indexed) {
            return new TextureAsset {
                Id = "gx-fixture", RuntimeAssetId = 0x0102030405060708ul, Width = 3, Height = 1,
                ColorFormat = indexed ? TextureAssetColorFormat.Indexed4 : TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8,
                Colors = indexed ? new byte[] { 0x10, 0x02 } : new byte[] { 255, 255, 255, 0, 255, 0, 0, 85, 0, 255, 0, 255 },
                PaletteColors = indexed ? new byte[] { 255, 255, 255, 0, 255, 0, 0, 85, 0, 255, 0, 255 } : Array.Empty<byte>()
            };
        }

        /// <summary>Writes a real texture payload under either supported HELE metadata byte order.</summary>
        /// <param name="texture">Texture with encoded GPU payload.</param>
        /// <param name="endianness">Outer metadata byte order.</param>
        /// <returns>Owned stream positioned at the header.</returns>
        static MemoryStream CreateTextureRecord(TextureAsset texture, EngineBinaryEndianness endianness) {
            MemoryStream stream = new();
            EngineBinaryHeaderSerializer.Write(stream, new EngineBinaryHeader(endianness, EditorAssetBinarySerializer.CurrentVersion,
                EditorAssetBinarySerializer.FormatId, (ushort)EditorAssetBinarySerializer.RecordKind, (ushort)EditorAssetBinaryValueKind.TextureAsset));
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, endianness)) {
                new TextureAssetPayloadSerializer().Write(writer, texture);
            }
            stream.Position = 0;
            return stream;
        }

        /// <summary>Writes the published version-five inline font atlas contract without a glyph table.</summary>
        /// <param name="texture">Atlas with encoded GPU payload.</param>
        /// <param name="endianness">Outer metadata byte order.</param>
        /// <returns>Owned stream positioned at the header.</returns>
        static MemoryStream CreateFontRecord(TextureAsset texture, EngineBinaryEndianness endianness) {
            MemoryStream stream = new();
            EngineBinaryHeaderSerializer.Write(stream, new EngineBinaryHeader(endianness, helengine.files.FontAssetBinarySerializer.CurrentVersion,
                helengine.files.FontAssetBinarySerializer.FormatId, (ushort)helengine.files.FontAssetBinarySerializer.RecordKind,
                helengine.files.FontAssetBinarySerializer.ValueKind));
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, endianness)) {
                writer.WriteString(texture.Id); writer.WriteInt64((long)texture.RuntimeAssetId);
                writer.WriteUInt16(texture.Width); writer.WriteUInt16(texture.Height);
                writer.WriteByte((byte)texture.ColorFormat); writer.WriteByte((byte)texture.AlphaPrecision);
                writer.WriteByteArray(texture.PaletteColors); writer.WriteByteArray(texture.Colors);
                writer.WriteString("GX native atlas"); writer.WriteInt32(12); writer.WriteSingle(4); writer.WriteSingle(12);
                writer.WriteInt32(texture.Width); writer.WriteInt32(texture.Height); writer.WriteInt32(0);
            }
            stream.Position = 0;
            return stream;
        }

        /// <summary>Compares stored native bytes and decoded coverage without treating CPU headers as GPU memory.</summary>
        /// <param name="expected">Original native texture.</param>
        /// <param name="actual">Texture read from the binary record.</param>
        /// <param name="rgba">Independently retained decoded pixels.</param>
        static void AssertNativeTexture(TextureAsset expected, TextureAsset actual, byte[] rgba) {
            Assert.Equal(TextureAssetColorFormat.GxNative, actual.ColorFormat);
            Assert.Equal(TextureAssetAlphaPrecision.A3, actual.AlphaPrecision);
            Assert.Equal(expected.Colors, actual.Colors);
            Assert.Equal(expected.PaletteColors, actual.PaletteColors);
            Assert.Equal(expected.RuntimeAssetId, actual.RuntimeAssetId);
            Assert.Equal(rgba, TextureAssetPixelCodec.DecodeToRgba32(actual));
        }
    }
}

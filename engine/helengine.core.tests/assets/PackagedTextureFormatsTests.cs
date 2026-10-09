using helengine;

namespace helengine.core.tests.assets {
    /// <summary>Verifies that runtime texture readers preserve every supported format's opaque bytes across header endiannesses.</summary>
    public sealed class PackagedTextureFormatsTests {
        /// <summary>Enumerates stable serialized format IDs in both supported header byte orders.</summary>
        public static TheoryData<byte, EngineBinaryEndianness> Formats {
            get {
                TheoryData<byte, EngineBinaryEndianness> cases = new();
                for (byte format = 0; format <= 14; format++) {
                    cases.Add(format, EngineBinaryEndianness.LittleEndian);
                    cases.Add(format, EngineBinaryEndianness.BigEndian);
                }
                return cases;
            }
        }

        /// <summary>Reads actual HELE texture records without swapping palette or pixel bytes, changing the layout, or consuming a following sentinel.</summary>
        /// <param name="format">Supported stable serialized format ID.</param>
        /// <param name="endianness">Byte order used for record metadata.</param>
        [Theory]
        [MemberData(nameof(Formats))]
        public void Deserialize_TextureFormatPreservesOpaquePayloadAndReaderPosition(byte format, EngineBinaryEndianness endianness) {
            byte[] palette = { 0x19, 0xB2, 0x73, 0xF4 };
            byte[] pixels = { 0x81, 0x02, 0xA3, 0x14, 0xC5, 0x36, 0xE7, 0x58 };
            using MemoryStream stream = CreateTextureRecord(format, endianness, palette, pixels);
            Assert.Equal((byte)25, PackagedAssetBinarySerializer.CurrentVersion);
            using TextureAsset asset = Assert.IsType<TextureAsset>(PackagedAssetBinarySerializer.Deserialize(stream));

            Assert.Equal(format, (byte)asset.ColorFormat);
            Assert.Equal(TextureAssetAlphaPrecision.A8, asset.AlphaPrecision);
            Assert.Equal((ushort)0x0102, asset.Width);
            Assert.Equal((ushort)0x0304, asset.Height);
            Assert.Equal("texture-format-test", asset.Id);
            Assert.Equal(0x0102030405060708ul, asset.RuntimeAssetId);
            Assert.Equal(palette, asset.PaletteColors);
            Assert.Equal(pixels, asset.Colors);
            Assert.Equal(stream.Length - 1, stream.Position);
            Assert.Equal(0x5A, stream.ReadByte());
        }

        /// <summary>Rejects undefined high-byte IDs before reading a malformed pixel payload.</summary>
        /// <param name="format">Unknown serialized format byte.</param>
        /// <param name="endianness">Byte order used for record metadata.</param>
        [Theory]
        [InlineData(254, EngineBinaryEndianness.LittleEndian)]
        [InlineData(255, EngineBinaryEndianness.LittleEndian)]
        [InlineData(254, EngineBinaryEndianness.BigEndian)]
        [InlineData(255, EngineBinaryEndianness.BigEndian)]
        public void Deserialize_UnknownTextureFormatRejectsByte(byte format, EngineBinaryEndianness endianness) {
            using MemoryStream stream = CreateTextureRecord(format, endianness, Array.Empty<byte>(), Array.Empty<byte>());
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => PackagedAssetBinarySerializer.Deserialize(stream));
            Assert.Contains($"Unsupported texture color format '{format}'", error.Message, StringComparison.Ordinal);
        }

        /// <summary>Writes the unchanged version-25 texture layout using the actual endian-specific writer and appends a position sentinel.</summary>
        /// <param name="format">Serialized format byte to exercise.</param>
        /// <param name="endianness">Metadata byte order.</param>
        /// <param name="palette">Opaque palette bytes, independent of the record's metadata order.</param>
        /// <param name="pixels">Opaque cooked pixel bytes, independent of the record's metadata order.</param>
        /// <returns>Record stream positioned at its header.</returns>
        static MemoryStream CreateTextureRecord(byte format, EngineBinaryEndianness endianness, byte[] palette, byte[] pixels) {
            MemoryStream stream = new();
            EngineBinaryHeaderSerializer.Write(stream, new EngineBinaryHeader(endianness, 25,
                PackagedAssetBinarySerializer.FormatId, (ushort)PackagedAssetBinarySerializer.RecordKind,
                (ushort)EditorAssetBinaryValueKind.TextureAsset));
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, endianness)) {
                writer.WriteString("texture-format-test");
                writer.WriteInt64(0x0102030405060708L);
                writer.WriteString("authored-texture");
                writer.WriteInt32(0);
                writer.WriteUInt16(0x0102);
                writer.WriteUInt16(0x0304);
                writer.WriteByte(format);
                writer.WriteByte((byte)TextureAssetAlphaPrecision.A8);
                writer.WriteByteArray(palette);
                writer.WriteByteArray(pixels);
                writer.WriteByte(0x5A);
            }
            stream.Position = 0;
            return stream;
        }
    }
}

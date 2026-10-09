using helengine;

namespace helengine.files.tests.assets {
    /// <summary>Tests editor texture serialization and its shared packaged-reader contract for every cooked pixel format.</summary>
    public sealed class TextureAssetPayloadSerializerTests {
        /// <summary>Enumerates the unchanged legacy IDs and appended format IDs in both metadata byte orders.</summary>
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

        /// <summary>Roundtrips through the editor's real writer and both actual readers while preserving source identity and unswapped byte buffers.</summary>
        /// <param name="format">Stable serialized pixel-format ID.</param>
        /// <param name="endianness">Metadata byte order used for the record.</param>
        [Theory]
        [MemberData(nameof(Formats))]
        public void Serialize_TextureFormatRoundtripsOpaqueBytesInBothReaders(byte format, EngineBinaryEndianness endianness) {
            byte[] palette = { 0x08, 0xF7, 0x26, 0xD5 };
            byte[] pixels = { 0x01, 0x80, 0x32, 0xA1, 0x63, 0xC2, 0x94, 0xE3 };
            using TextureAsset source = new() {
                Id = "format-roundtrip", RuntimeAssetId = 0x0102030405060708ul, AuthoringAssetId = "authored-format",
                Width = 0x0102, Height = 0x0304, ColorFormat = (TextureAssetColorFormat)format,
                AlphaPrecision = TextureAssetAlphaPrecision.A8, PaletteColors = palette, Colors = pixels
            };
            using MemoryStream stream = CreateTextureRecord(source, endianness);
            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            Assert.Equal((byte)25, header.Version);
            Assert.Equal(endianness, header.Endianness);
            Assert.Equal(EditorAssetBinarySerializer.FormatId, header.FormatId);
            Assert.Same(palette, source.PaletteColors);
            Assert.Same(pixels, source.Colors);

            stream.Position = 0;
            using TextureAsset editor = Assert.IsType<TextureAsset>(EditorAssetBinarySerializer.Deserialize(stream));
            AssertTexture(source, editor);
            Assert.Equal(stream.Length, stream.Position);
            stream.Position = 0;
            using TextureAsset runtime = Assert.IsType<TextureAsset>(PackagedAssetBinarySerializer.Deserialize(stream));
            AssertTexture(source, runtime);
            Assert.Equal(stream.Length, stream.Position);
        }

        /// <summary>Preserves the native XTX1 little-endian header inside both HELE metadata byte orders and both asset readers.</summary>
        /// <param name="endianness">Byte order used for the outer asset record.</param>
        [Theory]
        [InlineData(EngineBinaryEndianness.LittleEndian)]
        [InlineData(EngineBinaryEndianness.BigEndian)]
        public void Deserialize_NativeXboxTextureRetainsGpuBytesAndPreview(EngineBinaryEndianness endianness) {
            using TextureAsset source = new() {
                Id = "xbox-native-roundtrip", RuntimeAssetId = 19, AuthoringAssetId = "authored-xbox-texture",
                Width = 3, Height = 1, ColorFormat = TextureAssetColorFormat.Indexed8,
                AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = new byte[] { 0, 1, 0 },
                PaletteColors = new byte[] { 255, 0, 0, 255, 0, 255, 0, 128 }
            };
            using TextureAsset native = XboxNativeTextureCodec.Encode(source, "Xbox.Swizzled.I8_A8R8G8B8", TextureAssetAlphaPrecision.A8);
            using MemoryStream stream = CreateTextureRecord(native, endianness);
            byte[] expectedPreview = TextureAssetPixelCodec.DecodeToRgba32(source);
            using TextureAsset editor = Assert.IsType<TextureAsset>(EditorAssetBinarySerializer.Deserialize(stream));
            AssertTexture(native, editor);
            Assert.Equal(new byte[] { 0x58, 0x54, 0x58, 0x31 }, editor.Colors.Take(4));
            Assert.Equal(expectedPreview, TextureAssetPixelCodec.DecodeToRgba32(editor));
            stream.Position = 0;
            using TextureAsset packaged = Assert.IsType<TextureAsset>(PackagedAssetBinarySerializer.Deserialize(stream));
            AssertTexture(native, packaged);
            Assert.Equal(expectedPreview, TextureAssetPixelCodec.DecodeToRgba32(packaged));
        }

        /// <summary>Preserves native Xenos bytes through both HELE metadata byte orders and both asset readers.</summary>
        [Theory]
        [InlineData(EngineBinaryEndianness.LittleEndian)]
        [InlineData(EngineBinaryEndianness.BigEndian)]
        public void Deserialize_NativeXbox360TextureRetainsGpuBytesAndPreview(EngineBinaryEndianness endianness) {
            using TextureAsset source = new() {
                Id = "x360-native-roundtrip", RuntimeAssetId = 23, Width = 2, Height = 1,
                ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8,
                Colors = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9a, 0xbc, 0xde, 0xf0 }
            };
            using TextureAsset native = Xbox360NativeTextureCodec.Encode(source, "Xbox360.Linear.8_8_8_8", TextureAssetAlphaPrecision.A8);
            using MemoryStream stream = CreateTextureRecord(native, endianness);
            using TextureAsset editor = Assert.IsType<TextureAsset>(EditorAssetBinarySerializer.Deserialize(stream));
            AssertTexture(native, editor);
            Assert.Equal(new byte[] { 0x58, 0x33, 0x54, 0x31 }, editor.Colors.Take(4));
            Assert.Equal(source.Colors, TextureAssetPixelCodec.DecodeToRgba32(editor));
            stream.Position = 0;
            using TextureAsset runtime = Assert.IsType<TextureAsset>(PackagedAssetBinarySerializer.Deserialize(stream));
            AssertTexture(native, runtime);
            Assert.Equal(source.Colors, TextureAssetPixelCodec.DecodeToRgba32(runtime));
        }

        /// <summary>Preserves all two-bit levels in both native RGB10A2 codes and both HELE metadata byte orders.</summary>
        [Theory]
        [InlineData("2_10_10_10", EngineBinaryEndianness.LittleEndian)]
        [InlineData("2_10_10_10", EngineBinaryEndianness.BigEndian)]
        [InlineData("2_10_10_10_AS_16_16_16_16", EngineBinaryEndianness.LittleEndian)]
        [InlineData("2_10_10_10_AS_16_16_16_16", EngineBinaryEndianness.BigEndian)]
        public void Deserialize_NativeTwoBitAlphaRemainsOpaqueGpuBytes(string name, EngineBinaryEndianness endianness) {
            using TextureAsset source = new() { Width = 4, Height = 1, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8,
                Colors = new byte[] { 0, 0, 0, 0, 0, 0, 0, 85, 0, 0, 0, 170, 0, 0, 0, 255 } };
            using TextureAsset native = Xbox360NativeTextureCodec.Encode(source, "Xbox360.Linear." + name, TextureAssetAlphaPrecision.A2);
            using MemoryStream stream = CreateTextureRecord(native, endianness);
            using TextureAsset editor = Assert.IsType<TextureAsset>(EditorAssetBinarySerializer.Deserialize(stream));
            Assert.Equal(TextureAssetAlphaPrecision.A2, editor.AlphaPrecision);
            Assert.Equal(native.Colors, editor.Colors);
            Assert.Equal(source.Colors, TextureAssetPixelCodec.DecodeToRgba32(editor));
            stream.Position = 0;
            using TextureAsset runtime = Assert.IsType<TextureAsset>(PackagedAssetBinarySerializer.Deserialize(stream));
            Assert.Equal(TextureAssetAlphaPrecision.A2, runtime.AlphaPrecision);
            Assert.Equal(native.Colors, runtime.Colors);
            Assert.Equal(source.Colors, TextureAssetPixelCodec.DecodeToRgba32(runtime));
        }
        /// <summary>Rejects the appended policy outside native Xbox 360 rather than expanding legacy metadata support.</summary>
        [Theory]
        [InlineData(EngineBinaryEndianness.LittleEndian)]
        [InlineData(EngineBinaryEndianness.BigEndian)]
        public void Deserialize_GenericTextureRejectsTwoBitAlpha(EngineBinaryEndianness endianness) {
            using TextureAsset source = new() { Width = 1, Height = 1, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A2, Colors = new byte[4] };
            using MemoryStream stream = CreateTextureRecord(source, endianness);
            Assert.Throws<InvalidOperationException>(() => EditorAssetBinarySerializer.Deserialize(stream));
            stream.Position = 0;
            Assert.Throws<InvalidOperationException>(() => PackagedAssetBinarySerializer.Deserialize(stream));
        }

        /// <summary>Proves serialized unknown values are rejected by the editor reader rather than accepted through an unchecked enum cast.</summary>
        /// <param name="format">Undefined serialized pixel-format ID.</param>
        /// <param name="endianness">Metadata byte order used for the record.</param>
        [Theory]
        [InlineData(254, EngineBinaryEndianness.LittleEndian)]
        [InlineData(255, EngineBinaryEndianness.LittleEndian)]
        [InlineData(254, EngineBinaryEndianness.BigEndian)]
        [InlineData(255, EngineBinaryEndianness.BigEndian)]
        public void Deserialize_UnknownTextureFormatRejectsByte(byte format, EngineBinaryEndianness endianness) {
            using TextureAsset source = new() {
                Id = "unknown-format", RuntimeAssetId = 1, Width = 1, Height = 1,
                ColorFormat = (TextureAssetColorFormat)format, AlphaPrecision = TextureAssetAlphaPrecision.Opaque,
                Colors = new byte[] { 1, 2, 3, 4 }, PaletteColors = Array.Empty<byte>()
            };
            using MemoryStream stream = CreateTextureRecord(source, endianness);
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => EditorAssetBinarySerializer.Deserialize(stream));
            Assert.Contains($"Unsupported texture color format '{format}'", error.Message, StringComparison.Ordinal);
        }

        /// <summary>Serializes the ordinary little-endian editor record or writes the same payload behind a big-endian HELE header.</summary>
        /// <param name="source">Asset whose owned byte arrays must be preserved.</param>
        /// <param name="endianness">Metadata byte order used for the record.</param>
        /// <returns>Stream positioned at its HELE header.</returns>
        static MemoryStream CreateTextureRecord(TextureAsset source, EngineBinaryEndianness endianness) {
            MemoryStream stream = new();
            if (endianness == EngineBinaryEndianness.LittleEndian) {
                EditorAssetBinarySerializer.Serialize(stream, source);
            } else {
                EngineBinaryHeaderSerializer.Write(stream, new EngineBinaryHeader(endianness, 25,
                    EditorAssetBinarySerializer.FormatId, (ushort)EditorAssetBinarySerializer.RecordKind,
                    (ushort)EditorAssetBinaryValueKind.TextureAsset));
                using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, endianness);
                new TextureAssetPayloadSerializer().Write(writer, source);
            }
            stream.Position = 0;
            return stream;
        }

        /// <summary>Checks identity, dimensions, enums, palette and cooked bytes without decoding or reinterpreting any pixel data.</summary>
        /// <param name="source">Original serialized asset.</param>
        /// <param name="decoded">Asset produced by one of the two binary readers.</param>
        static void AssertTexture(TextureAsset source, TextureAsset decoded) {
            Assert.Equal(source.Id, decoded.Id);
            Assert.Equal(source.RuntimeAssetId, decoded.RuntimeAssetId);
            Assert.Equal(source.AuthoringAssetId, decoded.AuthoringAssetId);
            Assert.Equal(source.Width, decoded.Width);
            Assert.Equal(source.Height, decoded.Height);
            Assert.Equal(source.ColorFormat, decoded.ColorFormat);
            Assert.Equal(source.AlphaPrecision, decoded.AlphaPrecision);
            Assert.Equal(source.PaletteColors, decoded.PaletteColors);
            Assert.Equal(source.Colors, decoded.Colors);
        }
    }
}

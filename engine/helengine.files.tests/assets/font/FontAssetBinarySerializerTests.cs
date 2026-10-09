using helengine;
using helengine.files;

namespace helengine.files.tests.assets.font {
    /// <summary>
    /// Verifies that the packaged-file font serializer validates its complete binary header.
    /// </summary>
    public sealed class FontAssetBinarySerializerTests {
        /// <summary>Preserves Switch packed/native alpha and both owned buffers through editor and runtime font readers in either metadata byte order.</summary>
        [Theory]
        [InlineData("Switch.Linear.RGBA8_Unorm", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.LittleEndian)]
        [InlineData("Switch.Linear.RGBA8_Unorm", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.BigEndian)]
        [InlineData("Switch.Linear.RGBA4_Unorm", TextureAssetAlphaPrecision.A4, EngineBinaryEndianness.LittleEndian)]
        [InlineData("Switch.Linear.RGBA4_Unorm", TextureAssetAlphaPrecision.A4, EngineBinaryEndianness.BigEndian)]
        [InlineData("Switch.Linear.RGB10A2_Unorm", TextureAssetAlphaPrecision.A2, EngineBinaryEndianness.LittleEndian)]
        [InlineData("Switch.Linear.RGB10A2_Unorm", TextureAssetAlphaPrecision.A2, EngineBinaryEndianness.BigEndian)]
        public void Deserialize_NativeSwitchAtlasPreservesPackedAlpha(string id, TextureAssetAlphaPrecision alpha, EngineBinaryEndianness endianness) {
            SwitchTextureFormatCatalog.TryGetFormat(id, out SwitchTextureFormat format);
            SwitchTextureLayout layout = new SwitchTextureLayout(format, 2, 1);
            byte coverage = alpha == TextureAssetAlphaPrecision.A2 ? (byte)85 : alpha == TextureAssetAlphaPrecision.A4 ? (byte)136 : (byte)129;
            byte[] preview = new byte[] { 255, 255, 255, 0, 255, 255, 255, coverage };
            byte[] texels = new byte[layout.TexelBytes];
            if (alpha == TextureAssetAlphaPrecision.A8) Array.Copy(preview, texels, 8);
            else if (alpha == TextureAssetAlphaPrecision.A4) { texels[0] = 255; texels[1] = 15; texels[2] = 255; texels[3] = 143; }
            else { for (int index = 0; index < 8; index++) texels[index] = 255; texels[3] = 63; texels[7] = 127; }
            using TextureAsset atlas = SwitchTextureCodec.CreateNative(2, 1, format.Code, false, 0, alpha, SwitchTextureCodec.IdentitySwizzle, texels, preview);
            using MemoryStream stream = CreateTwoBitFontRecord(atlas, endianness);
            using FontAsset editor = FontAssetBinarySerializer.Deserialize(stream);
            Assert.Equal(TextureAssetColorFormat.SwitchNative, editor.SourceTextureAsset.ColorFormat);
            Assert.Equal(alpha, editor.SourceTextureAsset.AlphaPrecision); Assert.Equal(atlas.Colors, editor.SourceTextureAsset.Colors);
            Assert.Equal(preview, editor.SourceTextureAsset.PaletteColors);
            stream.Position = 0;
            using NativeAtlasRenderManager renderer = new NativeAtlasRenderManager();
            using FontAsset runtime = helengine.FontAssetBinarySerializer.Deserialize(stream, renderer);
            Assert.Equal(atlas.Colors, runtime.SourceTextureAsset.Colors); Assert.Equal(preview, renderer.DecodedPixels);
        }
        /// <summary>
        /// Ensures a packaged font header with a different value kind is rejected before payload reads begin.
        /// </summary>
        [Fact]
        public void Deserialize_WhenHeaderValueKindIsNotFontAsset_ThrowsFormatError() {
            using MemoryStream stream = CreateHeaderOnlyStream(0);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => FontAssetBinarySerializer.Deserialize(stream));

            Assert.Contains("value kind", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Preserves a swizzled PSP CLUT atlas through both font readers and decodes its CPU coverage.</summary>
        [Fact]
        public void Deserialize_NativePspAtlasPreservesCoverageAndGpuPayload() {
            using TextureAsset source = new TextureAsset {
                Id = "psp-font-atlas", RuntimeAssetId = 25, Width = 2, Height = 1, ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = new byte[] { 255, 255, 255, 0, 255, 255, 255, 128 }
            };
            using TextureAsset indexed = new TextureAsset {
                Width = 2, Height = 1, ColorFormat = TextureAssetColorFormat.Indexed8,
                AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = new byte[] { 0, 1 }, PaletteColors = source.Colors
            };
            TextureAsset atlas = PspTextureCodec.Encode(indexed, "Psp.Swizzled.T8.ClutRgba8888", TextureAssetAlphaPrecision.A8);
            using FontAsset font = new FontAsset(new FontInfo("PSP native font", 12, 4), null, new Dictionary<char, FontChar>(), 12, 2, 1) { SourceTextureAsset = atlas };
            using MemoryStream stream = new MemoryStream();
            FontAssetBinarySerializer.Serialize(stream, font);
            stream.Position = 0;
            using FontAsset decoded = FontAssetBinarySerializer.Deserialize(stream);
            Assert.Equal(TextureAssetColorFormat.PspNative, decoded.SourceTextureAsset.ColorFormat);
            Assert.Equal(atlas.Colors, decoded.SourceTextureAsset.Colors);
            Assert.Equal(atlas.PaletteColors, decoded.SourceTextureAsset.PaletteColors);
            stream.Position = 0;
            using NativeAtlasRenderManager renderer = new NativeAtlasRenderManager();
            using FontAsset runtime = helengine.FontAssetBinarySerializer.Deserialize(stream, renderer);
            Assert.Equal(atlas.Colors, runtime.SourceTextureAsset.Colors);
            Assert.Equal(atlas.PaletteColors, runtime.SourceTextureAsset.PaletteColors);
            Assert.Equal(source.Colors, renderer.DecodedPixels);
        }

        /// <summary>Preserves PICA alpha coverage and encoded GPU storage through both font readers in either metadata byte order.</summary>
        [Theory]
        [InlineData("Nintendo3Ds.A8", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.LittleEndian)]
        [InlineData("Nintendo3Ds.A8", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.BigEndian)]
        [InlineData("Nintendo3Ds.La8", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.LittleEndian)]
        [InlineData("Nintendo3Ds.La8", TextureAssetAlphaPrecision.A8, EngineBinaryEndianness.BigEndian)]
        [InlineData("Nintendo3Ds.Etc1A4", TextureAssetAlphaPrecision.A4, EngineBinaryEndianness.LittleEndian)]
        [InlineData("Nintendo3Ds.Etc1A4", TextureAssetAlphaPrecision.A4, EngineBinaryEndianness.BigEndian)]
        public void Deserialize_NativeNintendo3DsAtlasPreservesCoverage(string format, TextureAssetAlphaPrecision alpha, EngineBinaryEndianness endianness) {
            using TextureAsset source = new TextureAsset { Width = 2, Height = 1, ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = new byte[] { 255, 255, 255, 0, 255, 255, 255, 136 } };
            using TextureAsset atlas = Nintendo3DsTextureCodec.Encode(source, format, alpha);
            using MemoryStream stream = CreateTwoBitFontRecord(atlas, endianness);
            using FontAsset editor = FontAssetBinarySerializer.Deserialize(stream);
            Assert.Equal(TextureAssetColorFormat.Nintendo3DsNative, editor.SourceTextureAsset.ColorFormat);
            Assert.Equal(atlas.Colors, editor.SourceTextureAsset.Colors);
            Assert.Equal(alpha, editor.SourceTextureAsset.AlphaPrecision);
            stream.Position = 0;
            using NativeAtlasRenderManager renderer = new NativeAtlasRenderManager();
            using FontAsset runtime = helengine.FontAssetBinarySerializer.Deserialize(stream, renderer);
            Assert.Equal(atlas.Colors, runtime.SourceTextureAsset.Colors);
            Assert.Equal(Nintendo3DsTextureCodec.Decode(atlas), renderer.DecodedPixels);
            Assert.Equal(0, renderer.DecodedPixels[3]); Assert.Equal(136, renderer.DecodedPixels[7]);
        }

        /// <summary>Preserves native DS A3/A5 and binary coverage through editor/runtime font readers in either metadata byte order.</summary>
        [Theory]
        [InlineData("NintendoDs.A3I5", TextureAssetAlphaPrecision.A3, EngineBinaryEndianness.LittleEndian)]
        [InlineData("NintendoDs.A3I5", TextureAssetAlphaPrecision.A3, EngineBinaryEndianness.BigEndian)]
        [InlineData("NintendoDs.A5I3", TextureAssetAlphaPrecision.A5, EngineBinaryEndianness.LittleEndian)]
        [InlineData("NintendoDs.A5I3", TextureAssetAlphaPrecision.A5, EngineBinaryEndianness.BigEndian)]
        [InlineData("NintendoDs.Bgr5551", TextureAssetAlphaPrecision.Binary, EngineBinaryEndianness.LittleEndian)]
        [InlineData("NintendoDs.Bgr5551", TextureAssetAlphaPrecision.Binary, EngineBinaryEndianness.BigEndian)]
        public void Deserialize_NativeNintendoDsAtlasPreservesCoverage(string format, TextureAssetAlphaPrecision alpha, EngineBinaryEndianness endianness) {
            using TextureAsset source = new TextureAsset { Width = 2, Height = 1, ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = new byte[] { 255, 255, 255, 0, 255, 255, 255, 255 } };
            using TextureAsset atlas = NintendoDsTextureCodec.Encode(source, format, alpha);
            using MemoryStream stream = CreateTwoBitFontRecord(atlas, endianness);
            using FontAsset editor = FontAssetBinarySerializer.Deserialize(stream);
            Assert.Equal(TextureAssetColorFormat.NintendoDsNative, editor.SourceTextureAsset.ColorFormat);
            Assert.Equal(atlas.Colors, editor.SourceTextureAsset.Colors); Assert.Equal(atlas.PaletteColors, editor.SourceTextureAsset.PaletteColors);
            Assert.Equal(alpha, editor.SourceTextureAsset.AlphaPrecision);
            stream.Position = 0;
            using NativeAtlasRenderManager renderer = new NativeAtlasRenderManager();
            using FontAsset runtime = helengine.FontAssetBinarySerializer.Deserialize(stream, renderer);
            Assert.Equal(atlas.Colors, runtime.SourceTextureAsset.Colors);
            Assert.Equal(source.Colors, renderer.DecodedPixels);
        }

        /// <summary>Roundtrips a native Xbox alpha-only font atlas through file and runtime font readers.</summary>
        [Fact]
        public void Deserialize_NativeXboxAtlasPreservesCoverageAndGpuPayload() {
            using TextureAsset source = new() {
                Id = "font-atlas", RuntimeAssetId = 23, Width = 2, Height = 1, ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = new byte[] { 255, 255, 255, 0, 255, 255, 255, 128 }
            };
            TextureAsset atlas = XboxNativeTextureCodec.Encode(source, "Xbox.Linear.A8", TextureAssetAlphaPrecision.A8);
            using FontAsset font = new(new FontInfo("Xbox native font", 12, 4), null, new Dictionary<char, FontChar>(), 12, 2, 1) {
                SourceTextureAsset = atlas
            };
            using MemoryStream stream = new();
            FontAssetBinarySerializer.Serialize(stream, font);
            stream.Position = 0;
            using FontAsset decoded = FontAssetBinarySerializer.Deserialize(stream);
            Assert.Equal(TextureAssetColorFormat.XboxNative, decoded.SourceTextureAsset.ColorFormat);
            Assert.Equal(atlas.Colors, decoded.SourceTextureAsset.Colors);
            Assert.Equal(source.Colors, XboxNativeTextureCodec.Decode(decoded.SourceTextureAsset));
            stream.Position = 0;
            using NativeAtlasRenderManager renderer = new();
            using FontAsset runtime = helengine.FontAssetBinarySerializer.Deserialize(stream, renderer);
            Assert.Equal(atlas.Colors, runtime.SourceTextureAsset.Colors);
            Assert.Equal(source.Colors, renderer.DecodedPixels);
        }

        /// <summary>Roundtrips compact BC4 and single-channel Xbox 360 alpha atlases through file and runtime font readers.</summary>
        [Theory]
        [InlineData("DXT5A")]
        [InlineData("8_A")]
        public void Deserialize_NativeXbox360AtlasPreservesCoverageAndGpuPayload(string formatName) {
            using TextureAsset source = new() {
                Id = "x360-font-atlas", RuntimeAssetId = 24, Width = 2, Height = 1, ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = new byte[] { 255, 255, 255, 0, 255, 255, 255, 128 }
            };
            TextureAsset atlas = Xbox360NativeTextureCodec.Encode(source, "Xbox360.Linear." + formatName, TextureAssetAlphaPrecision.A8);
            using FontAsset font = new(new FontInfo("Xbox 360 native font", 12, 4), null, new Dictionary<char, FontChar>(), 12, 2, 1) { SourceTextureAsset = atlas };
            using MemoryStream stream = new();
            FontAssetBinarySerializer.Serialize(stream, font);
            stream.Position = 0;
            using FontAsset decoded = FontAssetBinarySerializer.Deserialize(stream);
            Assert.Equal(TextureAssetColorFormat.Xbox360Native, decoded.SourceTextureAsset.ColorFormat);
            Assert.Equal(atlas.Colors, decoded.SourceTextureAsset.Colors);
            Assert.Equal(source.Colors, TextureAssetPixelCodec.DecodeToRgba32(decoded.SourceTextureAsset));
            stream.Position = 0;
            using NativeAtlasRenderManager renderer = new();
            using FontAsset runtime = helengine.FontAssetBinarySerializer.Deserialize(stream, renderer);
            Assert.Equal(atlas.Colors, runtime.SourceTextureAsset.Colors);
            Assert.Equal(source.Colors, renderer.DecodedPixels);
        }

        /// <summary>Retains native RGB10A2 coverage through file and runtime font readers in both metadata byte orders.</summary>
        [Theory]
        [InlineData(EngineBinaryEndianness.LittleEndian)]
        [InlineData(EngineBinaryEndianness.BigEndian)]
        public void Deserialize_NativeXbox360AtlasRetainsTwoBitAlpha(EngineBinaryEndianness endianness) {
            using TextureAsset source = new() { Width = 4, Height = 1, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8,
                Colors = new byte[] { 255, 255, 255, 0, 255, 255, 255, 85, 255, 255, 255, 170, 255, 255, 255, 255 } };
            using TextureAsset atlas = Xbox360NativeTextureCodec.Encode(source, "Xbox360.Linear.2_10_10_10", TextureAssetAlphaPrecision.A2);
            using MemoryStream stream = CreateTwoBitFontRecord(atlas, endianness);
            using FontAsset editor = FontAssetBinarySerializer.Deserialize(stream);
            Assert.Equal(TextureAssetAlphaPrecision.A2, editor.SourceTextureAsset.AlphaPrecision);
            Assert.Equal(atlas.Colors, editor.SourceTextureAsset.Colors);
            Assert.Equal(source.Colors, TextureAssetPixelCodec.DecodeToRgba32(editor.SourceTextureAsset));
            stream.Position = 0;
            using NativeAtlasRenderManager renderer = new();
            using FontAsset runtime = helengine.FontAssetBinarySerializer.Deserialize(stream, renderer);
            Assert.Equal(TextureAssetAlphaPrecision.A2, runtime.SourceTextureAsset.AlphaPrecision);
            Assert.Equal(source.Colors, renderer.DecodedPixels);
        }
        /// <summary>Rejects native-only alpha precision in a generic atlas in both font readers.</summary>
        [Theory]
        [InlineData(EngineBinaryEndianness.LittleEndian)]
        [InlineData(EngineBinaryEndianness.BigEndian)]
        public void Deserialize_GenericAtlasRejectsTwoBitAlpha(EngineBinaryEndianness endianness) {
            using TextureAsset atlas = new() { Width = 1, Height = 1, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A2, Colors = new byte[4] };
            using MemoryStream stream = CreateTwoBitFontRecord(atlas, endianness);
            Assert.Throws<InvalidOperationException>(() => FontAssetBinarySerializer.Deserialize(stream));
            stream.Position = 0;
            using NativeAtlasRenderManager renderer = new();
            Assert.Throws<InvalidOperationException>(() => helengine.FontAssetBinarySerializer.Deserialize(stream, renderer));
        }
        /// <summary>Builds an independent version-five font record with an inline atlas and no glyph table.</summary>
        static MemoryStream CreateTwoBitFontRecord(TextureAsset atlas, EngineBinaryEndianness endianness) {
            MemoryStream stream = new();
            EngineBinaryHeaderSerializer.Write(stream, new EngineBinaryHeader(endianness, 5, FontAssetBinarySerializer.FormatId, (ushort)FontAssetBinarySerializer.RecordKind, FontAssetBinarySerializer.ValueKind));
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, endianness)) {
                writer.WriteString(string.Empty); writer.WriteInt64(24); writer.WriteUInt16(atlas.Width); writer.WriteUInt16(atlas.Height);
                writer.WriteByte((byte)atlas.ColorFormat); writer.WriteByte((byte)atlas.AlphaPrecision); writer.WriteByteArray(atlas.PaletteColors); writer.WriteByteArray(atlas.Colors);
                writer.WriteString("Two-bit alpha fixture"); writer.WriteInt32(12); writer.WriteSingle(4); writer.WriteSingle(12);
                writer.WriteInt32(atlas.Width); writer.WriteInt32(atlas.Height); writer.WriteInt32(0);
            }
            stream.Position = 0; return stream;
        }

        /// <summary>
        /// Creates a stream containing a valid packaged font header except for its value kind.
        /// </summary>
        /// <param name="valueKind">Value kind encoded in the header.</param>
        /// <returns>Stream positioned at the beginning of the header.</returns>
        static MemoryStream CreateHeaderOnlyStream(ushort valueKind) {
            MemoryStream stream = new MemoryStream();
            EngineBinaryHeader header = new EngineBinaryHeader(
                EngineBinaryEndianness.LittleEndian,
                FontAssetBinarySerializer.CurrentVersion,
                FontAssetBinarySerializer.FormatId,
                (ushort)FontAssetBinarySerializer.RecordKind,
                valueKind);
            EngineBinaryHeaderSerializer.Write(stream, header);
            stream.Position = 0;
            return stream;
        }
    }
}

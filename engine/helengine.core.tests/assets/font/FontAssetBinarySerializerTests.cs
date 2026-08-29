using helengine;

namespace helengine.core.tests.assets.font {
    /// <summary>
    /// Verifies that packaged font readers enforce the current binary format.
    /// </summary>
    public sealed class FontAssetBinarySerializerTests {
        sealed class TestRenderManager2D : RenderManager2D {
            public override RuntimeTexture BuildTextureFromRaw(TextureAsset data) {
                return null;
            }

            public override void DrawSprite(ISpriteDrawable2D sprite) {
            }

            public override void DrawText(ITextDrawable2D text) {
            }

            public override void DrawRoundedRect(IRoundedRectDrawable2D shape) {
            }
        }
        /// <summary>
        /// Ensures a packaged font with an older or newer header version is rejected before payload reads begin.
        /// </summary>
        /// <param name="version">Unsupported packaged font header version.</param>
        [Theory]
        [InlineData(4)]
        [InlineData(6)]
        public void Deserialize_WhenHeaderVersionIsNotCurrent_ThrowsRegenerationGuidance(byte version) {
            using MemoryStream stream = CreateHeaderOnlyStream(version);

            using TestRenderManager2D renderer = new TestRenderManager2D();
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => FontAssetBinarySerializer.Deserialize(stream, renderer));

            Assert.Contains(version.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains(FontAssetBinarySerializer.CurrentVersion.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains("Regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures a packaged font header with a different value kind is rejected before any renderer state is required.
        /// </summary>
        [Fact]
        public void Deserialize_WhenHeaderValueKindIsNotFontAsset_ThrowsFormatError() {
            using MemoryStream stream = CreateHeaderOnlyStream(
                FontAssetBinarySerializer.CurrentVersion,
                (ushort)FontAssetBinarySerializer.RecordKind,
                0);

            using TestRenderManager2D renderer = new TestRenderManager2D();
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => FontAssetBinarySerializer.Deserialize(stream, renderer));

            Assert.Contains("value kind", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates a stream containing a valid packaged font header and no payload bytes.
        /// </summary>
        /// <param name="version">Version encoded in the header.</param>
        /// <returns>Stream positioned at the beginning of the header.</returns>
        static MemoryStream CreateHeaderOnlyStream(
            byte version,
            ushort recordKind = (ushort)FontAssetBinarySerializer.RecordKind,
            ushort valueKind = FontAssetBinarySerializer.ValueKind) {
            MemoryStream stream = new MemoryStream();
            EngineBinaryHeader header = new EngineBinaryHeader(
                EngineBinaryEndianness.LittleEndian,
                version,
                FontAssetBinarySerializer.FormatId,
                recordKind,
                valueKind);
            EngineBinaryHeaderSerializer.Write(stream, header);
            stream.Position = 0;
            return stream;
        }
    }
}

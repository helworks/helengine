using helengine;

namespace helengine.core.tests.assets.font {
    /// <summary>
    /// Verifies that packaged font readers enforce the current binary format.
    /// </summary>
    public sealed class FontAssetBinarySerializerTests {
        /// <summary>
        /// Ensures a packaged font with an older or newer header version is rejected before payload reads begin.
        /// </summary>
        /// <param name="version">Unsupported packaged font header version.</param>
        [Theory]
        [InlineData(4)]
        [InlineData(6)]
        public void Deserialize_WhenHeaderVersionIsNotCurrent_ThrowsRegenerationGuidance(byte version) {
            using MemoryStream stream = CreateHeaderOnlyStream(version);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => FontAssetBinarySerializer.Deserialize(stream));

            Assert.Contains(version.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains(FontAssetBinarySerializer.CurrentVersion.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains("Regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates a stream containing a valid packaged font header and no payload bytes.
        /// </summary>
        /// <param name="version">Version encoded in the header.</param>
        /// <returns>Stream positioned at the beginning of the header.</returns>
        static MemoryStream CreateHeaderOnlyStream(byte version) {
            MemoryStream stream = new MemoryStream();
            EngineBinaryHeader header = new EngineBinaryHeader(
                EngineBinaryEndianness.LittleEndian,
                version,
                FontAssetBinarySerializer.FormatId,
                (ushort)FontAssetBinarySerializer.RecordKind,
                1);
            EngineBinaryHeaderSerializer.Write(stream, header);
            stream.Position = 0;
            return stream;
        }
    }
}

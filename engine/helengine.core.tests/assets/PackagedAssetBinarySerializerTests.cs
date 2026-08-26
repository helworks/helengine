using helengine;

namespace helengine.core.tests.assets {
    /// <summary>
    /// Verifies that packaged runtime asset readers enforce the current binary format.
    /// </summary>
    public sealed class PackagedAssetBinarySerializerTests {
        /// <summary>
        /// Ensures a packaged asset with an older or newer header version is rejected before payload reads begin.
        /// </summary>
        /// <param name="version">Unsupported packaged asset header version.</param>
        [Theory]
        [InlineData(23)]
        [InlineData(25)]
        public void Deserialize_WhenHeaderVersionIsNotCurrent_ThrowsRegenerationGuidance(byte version) {
            using MemoryStream stream = CreateHeaderOnlyStream(version);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => PackagedAssetBinarySerializer.Deserialize(stream));

            Assert.Contains(version.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains(PackagedAssetBinarySerializer.CurrentVersion.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains("Regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates a stream containing a valid packaged scene header and no payload bytes.
        /// </summary>
        /// <param name="version">Version encoded in the header.</param>
        /// <returns>Stream positioned at the beginning of the header.</returns>
        static MemoryStream CreateHeaderOnlyStream(byte version) {
            MemoryStream stream = new MemoryStream();
            EngineBinaryHeader header = new EngineBinaryHeader(
                EngineBinaryEndianness.LittleEndian,
                version,
                PackagedAssetBinarySerializer.FormatId,
                (ushort)PackagedAssetBinarySerializer.RecordKind,
                (ushort)EditorAssetBinaryValueKind.SceneAsset);
            EngineBinaryHeaderSerializer.Write(stream, header);
            stream.Position = 0;
            return stream;
        }
    }
}

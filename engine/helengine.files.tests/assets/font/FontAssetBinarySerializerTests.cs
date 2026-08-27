using helengine;
using helengine.files;

namespace helengine.files.tests.assets.font {
    /// <summary>
    /// Verifies that the packaged-file font serializer validates its complete binary header.
    /// </summary>
    public sealed class FontAssetBinarySerializerTests {
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

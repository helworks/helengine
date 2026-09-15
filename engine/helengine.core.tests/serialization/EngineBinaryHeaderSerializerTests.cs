using Xunit;

namespace helengine.core.tests.serialization {
    /// <summary>
    /// Verifies the non-throwing HELE header probe used when classifying arbitrary project files.
    /// </summary>
    public sealed class EngineBinaryHeaderSerializerTests {
        /// <summary>
        /// Ensures a written header round-trips through the probe.
        /// </summary>
        [Fact]
        public void TryRead_WhenPayloadHasValidHeader_ReturnsHeader() {
            using MemoryStream stream = new MemoryStream();
            EngineBinaryHeaderSerializer.Write(stream, new EngineBinaryHeader(EngineBinaryEndianness.LittleEndian, 3, 7, 11, 13));
            stream.Position = 0;

            bool read = EngineBinaryHeaderSerializer.TryRead(stream, out EngineBinaryHeader header);

            Assert.True(read);
            Assert.Equal(EngineBinaryEndianness.LittleEndian, header.Endianness);
            Assert.Equal(3, header.Version);
            Assert.Equal(7, header.FormatId);
            Assert.Equal(11, header.RecordKind);
            Assert.Equal(13, header.ValueKind);
        }

        /// <summary>
        /// Ensures payloads without the HELE magic are rejected without any exception being raised.
        /// </summary>
        [Fact]
        public void TryRead_WhenPayloadLacksMagic_ReturnsFalseWithoutThrowing() {
            using MemoryStream stream = new MemoryStream(new byte[] { (byte)'{', (byte)'"', (byte)'a', (byte)'"', 1, 2, 3, 4, 5, 6 });
            int firstChanceCount = 0;
            EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> handler = (sender, args) => firstChanceCount++;

            AppDomain.CurrentDomain.FirstChanceException += handler;
            bool read;
            EngineBinaryHeader header;
            try {
                read = EngineBinaryHeaderSerializer.TryRead(stream, out header);
            } finally {
                AppDomain.CurrentDomain.FirstChanceException -= handler;
            }

            Assert.False(read);
            Assert.Null(header);
            Assert.Equal(0, firstChanceCount);
        }

        /// <summary>
        /// Ensures a truncated header is rejected without throwing.
        /// </summary>
        [Fact]
        public void TryRead_WhenPayloadIsTruncated_ReturnsFalse() {
            using MemoryStream stream = new MemoryStream(new byte[] { (byte)'H', (byte)'E', (byte)'L', (byte)'E', (byte)EngineBinaryEndianness.LittleEndian, 1, 7 });

            Assert.False(EngineBinaryHeaderSerializer.TryRead(stream, out EngineBinaryHeader header));
            Assert.Null(header);
        }

        /// <summary>
        /// Ensures an unsupported endianness byte is rejected without throwing.
        /// </summary>
        [Fact]
        public void TryRead_WhenEndiannessIsUnsupported_ReturnsFalse() {
            using MemoryStream stream = new MemoryStream(new byte[] { (byte)'H', (byte)'E', (byte)'L', (byte)'E', 99, 1, 7, 0, 11, 0, 13, 0 });

            Assert.False(EngineBinaryHeaderSerializer.TryRead(stream, out EngineBinaryHeader header));
            Assert.Null(header);
        }
    }
}

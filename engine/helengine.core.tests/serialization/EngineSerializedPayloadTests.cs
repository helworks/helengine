using System.Reflection;
using helengine;

namespace helengine.core.tests.serialization {
    /// <summary>
    /// Verifies the managed contract of the engine-owned serialized payload reader factory.
    /// </summary>
    public sealed class EngineSerializedPayloadTests {
        /// <summary>
        /// Ensures a successful reader creation keeps the transferred payload stream usable.
        /// </summary>
        [Fact]
        public void CreatePayloadReader_WhenPayloadIsValid_ReturnsUsableReader() {
            EngineSerializedPayload payload = EngineSerializedPayload.Create(
                "test.payload",
                0x1234,
                7,
                EngineBinaryEndianness.LittleEndian,
                writer => writer.WriteInt32(123456));

            using EngineBinaryReader reader = payload.CreatePayloadReader("test.payload", 0x1234, 7);

            Assert.Equal(EngineBinaryEndianness.LittleEndian, reader.Endianness);
            Assert.Equal(123456, reader.ReadInt32());
        }

        /// <summary>
        /// Ensures a payload without the HELE magic reports the existing malformed-header exception.
        /// </summary>
        [Fact]
        public void CreatePayloadReader_WhenHeaderIsMalformed_ThrowsInvalidOperationException() {
            EngineSerializedPayload payload = CreatePayloadFromBytes([(byte)'N', (byte)'O', (byte)'P', (byte)'E']);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => payload.CreatePayloadReader("test.payload", 0x1234, 7));

            Assert.Equal("The binary payload does not start with the HELE header.", exception.Message);
        }

        /// <summary>
        /// Ensures a payload truncated before the complete HELE header reports the existing end-of-stream exception.
        /// </summary>
        [Fact]
        public void CreatePayloadReader_WhenHeaderIsTruncated_ThrowsEndOfStreamException() {
            EngineSerializedPayload payload = CreatePayloadFromBytes([(byte)'H', (byte)'E', (byte)'L', (byte)'E']);

            EndOfStreamException exception = Assert.Throws<EndOfStreamException>(
                () => payload.CreatePayloadReader("test.payload", 0x1234, 7));

            Assert.Equal("The binary payload ended before the HELE header was complete.", exception.Message);
        }

        /// <summary>
        /// Ensures header metadata validation keeps reporting the existing expected-format failure.
        /// </summary>
        [Fact]
        public void CreatePayloadReader_WhenHeaderFormatDoesNotMatch_ThrowsInvalidOperationException() {
            EngineSerializedPayload payload = EngineSerializedPayload.Create(
                "test.payload",
                0x1234,
                7,
                EngineBinaryEndianness.BigEndian,
                writer => writer.WriteInt32(123456));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => payload.CreatePayloadReader("test.payload", 0x4321, 7));

            Assert.Equal("Serialized payload binary format '4660' does not match expected format '17185'.", exception.Message);
        }

        /// <summary>
        /// Constructs a payload with controlled bytes so header failure paths can be exercised directly.
        /// </summary>
        /// <param name="serializedBytes">Serialized payload bytes to store.</param>
        /// <returns>Payload initialized with the supplied bytes.</returns>
        static EngineSerializedPayload CreatePayloadFromBytes(byte[] serializedBytes) {
            ConstructorInfo constructor = typeof(EngineSerializedPayload).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                [typeof(string), typeof(byte[])],
                null);
            Assert.NotNull(constructor);
            return (EngineSerializedPayload)constructor.Invoke(["test.payload", serializedBytes]);
        }
    }
}

using helengine;

namespace helengine.core.tests.serialization {
    /// <summary>
    /// Verifies the shared header-write, header-read, and strict version-validation ceremony used by the engine's versioned HELE binary payload serializers.
    /// </summary>
    public sealed class VersionedBinaryPayloadTests {
        [Fact]
        public void WriteHeader_ThenReadHeader_RoundTripsThePayload() {
            using MemoryStream stream = new MemoryStream();
            using (EngineBinaryWriter writer = VersionedBinaryPayload.WriteHeader(stream, EngineBinaryEndianness.LittleEndian, 3, 7, 11, 42)) {
                writer.WriteInt32(1234);
            }

            stream.Position = 0;
            EngineBinaryHeader header;
            using EngineBinaryReader reader = VersionedBinaryPayload.ReadHeader(stream, 7, 11, 42, 3, "test payload", "test payload", "Regenerate the test payload.", out header);

            Assert.Equal(3, header.Version);
            Assert.Equal((ushort)7, header.FormatId);
            Assert.Equal((ushort)11, header.RecordKind);
            Assert.Equal((ushort)42, header.ValueKind);
            Assert.Equal(1234, reader.ReadInt32());
        }

        [Fact]
        public void ReadHeader_WhenVersionDoesNotMatch_ThrowsWithRegenerateInstruction() {
            using MemoryStream stream = new MemoryStream();
            using (VersionedBinaryPayload.WriteHeader(stream, EngineBinaryEndianness.LittleEndian, 1, 7, 11, 42)) {
            }

            stream.Position = 0;
            EngineBinaryHeader header;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => VersionedBinaryPayload.ReadHeader(stream, 7, 11, 42, 2, "test payload", "test payload", "Regenerate the test payload.", out header));

            Assert.Contains("current version is '2'", exception.Message, StringComparison.Ordinal);
            Assert.Contains("Regenerate the test payload.", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ReadHeader_WhenVersionDoesNotMatchAndRegenerateInstructionIsEmpty_ThrowsTerseMessage() {
            using MemoryStream stream = new MemoryStream();
            using (VersionedBinaryPayload.WriteHeader(stream, EngineBinaryEndianness.LittleEndian, 1, 7, 11, 42)) {
            }

            stream.Position = 0;
            EngineBinaryHeader header;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => VersionedBinaryPayload.ReadHeader(stream, 7, 11, 42, 2, "test payload", "test payload", string.Empty, out header));

            Assert.Equal("Unsupported test payload binary version '1'.", exception.Message);
        }

        [Fact]
        public void ReadHeader_WhenValueKindDoesNotMatch_ThrowsBeforeVersionIsChecked() {
            using MemoryStream stream = new MemoryStream();
            using (VersionedBinaryPayload.WriteHeader(stream, EngineBinaryEndianness.LittleEndian, 99, 7, 11, 42)) {
            }

            stream.Position = 0;
            EngineBinaryHeader header;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => VersionedBinaryPayload.ReadHeader(stream, 7, 11, 43, 3, "test payload", "test payload", "Regenerate the test payload.", out header));

            Assert.Equal("Unexpected test payload value kind '42'.", exception.Message);
        }

        [Fact]
        public void ReadHeaderWithDispatchedValueKind_DoesNotValidateValueKind() {
            using MemoryStream stream = new MemoryStream();
            using (EngineBinaryWriter writer = VersionedBinaryPayload.WriteHeader(stream, EngineBinaryEndianness.LittleEndian, 5, 7, 11, 99)) {
                writer.WriteInt32(4321);
            }

            stream.Position = 0;
            EngineBinaryHeader header;
            using EngineBinaryReader reader = VersionedBinaryPayload.ReadHeaderWithDispatchedValueKind(stream, 7, 11, 5, "dispatched payload", "dispatched payload", string.Empty, out header);

            Assert.Equal((ushort)99, header.ValueKind);
            Assert.Equal(4321, reader.ReadInt32());
        }
    }
}

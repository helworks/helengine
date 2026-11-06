using helengine;

namespace helengine.core.tests.serialization {
    /// <summary>
    /// Verifies the shared header-write, header-read, already-decoded-header validation, and strict version-validation ceremony used by the engine's versioned HELE binary payload serializers.
    /// </summary>
    public sealed class VersionedBinaryPayloadTests {
        /// <summary>
        /// Ensures each helper that returns a stream-backed reader or writer leaves its stream parameter eligible for the returned wrapper to retain.
        /// </summary>
        [Theory]
        [InlineData("WriteHeader")]
        [InlineData("ReadHeader")]
        [InlineData("ReadHeaderWithDispatchedValueKind")]
        [InlineData("ValidateHeader")]
        [InlineData("ValidateHeaderWithDispatchedValueKind")]
        public void ReturnedStreamWrapperMethods_DoNotDeclareStreamNoEscape(string methodName) {
            System.Reflection.MethodInfo[] overloads = typeof(VersionedBinaryPayload).GetMethods().Where(method => method.Name == methodName).ToArray();

            Assert.NotEmpty(overloads);
            foreach (System.Reflection.MethodInfo overload in overloads) {
                System.Reflection.ParameterInfo stream = overload.GetParameters()[0];

                Assert.Empty(stream.GetCustomAttributes(typeof(NativeNoEscapeAttribute), false));
            }
        }

        [Fact]
        public void WriteHeader_ThenReadHeader_RoundTripsThePayload() {
            using MemoryStream stream = new MemoryStream();
            using (EngineBinaryWriter writer = VersionedBinaryPayload.WriteHeader(stream, EngineBinaryEndianness.LittleEndian, 3, 7, 11, 42)) {
                writer.WriteInt32(1234);
            }

            stream.Position = 0;
            using EngineBinaryReader reader = VersionedBinaryPayload.ReadHeader(
                stream,
                7,
                11,
                42,
                3,
                "test payload",
                "test payload",
                "test payload",
                VersionedBinaryVersionMismatchStyle.CurrentVersionSuffix,
                "Regenerate the test payload.");

            Assert.Equal(1234, reader.ReadInt32());
        }

        [Fact]
        public void ReadHeader_WhenVersionDoesNotMatch_ThrowsWithRegenerateInstruction() {
            using MemoryStream stream = new MemoryStream();
            using (VersionedBinaryPayload.WriteHeader(stream, EngineBinaryEndianness.LittleEndian, 1, 7, 11, 42)) {
            }

            stream.Position = 0;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => VersionedBinaryPayload.ReadHeader(
                    stream,
                    7,
                    11,
                    42,
                    2,
                    "test payload",
                    "test payload",
                    "test payload",
                    VersionedBinaryVersionMismatchStyle.CurrentVersionSuffix,
                    "Regenerate the test payload."));

            Assert.Contains("current version is '2'", exception.Message, StringComparison.Ordinal);
            Assert.Contains("Regenerate the test payload.", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ReadHeader_WhenVersionDoesNotMatchAndStyleIsReceivedVersionOnly_ThrowsTerseMessage() {
            using MemoryStream stream = new MemoryStream();
            using (VersionedBinaryPayload.WriteHeader(stream, EngineBinaryEndianness.LittleEndian, 1, 7, 11, 42)) {
            }

            stream.Position = 0;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => VersionedBinaryPayload.ReadHeader(
                    stream,
                    7,
                    11,
                    42,
                    2,
                    "test payload",
                    "test payload",
                    "test payload",
                    VersionedBinaryVersionMismatchStyle.ReceivedVersionOnly,
                    string.Empty));

            Assert.Equal("Unsupported test payload binary version '1'.", exception.Message);
        }

        [Fact]
        public void ReadHeader_WhenVersionDoesNotMatchAndStyleIsRequiredVersionSubjectFirst_ThrowsSubjectLedMessage() {
            using MemoryStream stream = new MemoryStream();
            using (VersionedBinaryPayload.WriteHeader(stream, EngineBinaryEndianness.LittleEndian, 1, 7, 11, 42)) {
            }

            stream.Position = 0;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => VersionedBinaryPayload.ReadHeader(
                    stream,
                    7,
                    11,
                    42,
                    2,
                    "test payload",
                    "test payload",
                    "Test payload",
                    VersionedBinaryVersionMismatchStyle.RequiredVersionSubjectFirst,
                    "Regenerate the test payload."));

            Assert.Equal("Test payload version '1' is unsupported; version '2' is required. Regenerate the test payload.", exception.Message);
        }

        /// <summary>
        /// Ensures the accepted-versions overload admits every listed version and hands back the decoded header.
        /// </summary>
        [Theory]
        [InlineData((byte)5)]
        [InlineData((byte)6)]
        public void ReadHeader_WithAcceptedVersions_AdmitsEachListedVersionAndReturnsHeader(byte version) {
            using MemoryStream stream = new MemoryStream();
            using (EngineBinaryWriter writer = VersionedBinaryPayload.WriteHeader(stream, EngineBinaryEndianness.LittleEndian, version, 7, 11, 42)) {
                writer.WriteInt32(77);
            }

            stream.Position = 0;
            using EngineBinaryReader reader = VersionedBinaryPayload.ReadHeader(
                stream,
                7,
                11,
                42,
                new byte[] { 5, 6 },
                "test payload",
                "test payload",
                "test payload",
                VersionedBinaryVersionMismatchStyle.CurrentVersionSuffix,
                "Regenerate the test payload.",
                out EngineBinaryHeader header);

            Assert.Equal(version, header.Version);
            Assert.Equal(77, reader.ReadInt32());
        }

        /// <summary>
        /// Ensures the accepted-versions overload rejects an unlisted version and names the highest accepted version as current.
        /// </summary>
        [Fact]
        public void ReadHeader_WithAcceptedVersions_WhenVersionIsNotListed_ThrowsNamingHighestVersion() {
            using MemoryStream stream = new MemoryStream();
            using (VersionedBinaryPayload.WriteHeader(stream, EngineBinaryEndianness.LittleEndian, 4, 7, 11, 42)) {
            }

            stream.Position = 0;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => VersionedBinaryPayload.ReadHeader(
                    stream,
                    7,
                    11,
                    42,
                    new byte[] { 5, 6 },
                    "test payload",
                    "test payload",
                    "test payload",
                    VersionedBinaryVersionMismatchStyle.CurrentVersionSuffix,
                    "Regenerate the test payload.",
                    out EngineBinaryHeader _));

            Assert.Contains("received '4'", exception.Message, StringComparison.Ordinal);
            Assert.Contains("current version is '6'", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ReadHeader_WhenValueKindDoesNotMatch_ThrowsBeforeVersionIsChecked() {
            using MemoryStream stream = new MemoryStream();
            using (VersionedBinaryPayload.WriteHeader(stream, EngineBinaryEndianness.LittleEndian, 99, 7, 11, 42)) {
            }

            stream.Position = 0;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => VersionedBinaryPayload.ReadHeader(
                    stream,
                    7,
                    11,
                    43,
                    3,
                    "test payload",
                    "test payload",
                    "test payload",
                    VersionedBinaryVersionMismatchStyle.CurrentVersionSuffix,
                    "Regenerate the test payload."));

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
            using EngineBinaryReader reader = VersionedBinaryPayload.ReadHeaderWithDispatchedValueKind(
                stream,
                7,
                11,
                5,
                "dispatched payload",
                "dispatched payload",
                "dispatched payload",
                VersionedBinaryVersionMismatchStyle.ReceivedVersionOnly,
                string.Empty,
                out header);

            Assert.Equal((ushort)99, header.ValueKind);
            Assert.Equal(4321, reader.ReadInt32());
        }

        [Fact]
        public void ValidateHeader_WhenHeaderWasAlreadyRead_ValidatesAndReturnsPayloadReader() {
            using MemoryStream stream = new MemoryStream();
            using (EngineBinaryWriter writer = VersionedBinaryPayload.WriteHeader(stream, EngineBinaryEndianness.LittleEndian, 3, 7, 11, 42)) {
                writer.WriteInt32(555);
            }

            stream.Position = 0;
            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            using EngineBinaryReader reader = VersionedBinaryPayload.ValidateHeader(
                stream,
                header,
                7,
                11,
                42,
                3,
                "test payload",
                "test payload",
                "Test payload",
                VersionedBinaryVersionMismatchStyle.RequiredVersionSubjectFirst,
                "Regenerate the test payload.");

            Assert.Equal(555, reader.ReadInt32());
        }

        [Fact]
        public void ValidateHeader_WhenAlreadyReadHeaderHasWrongVersion_ThrowsWithoutConsumingThePayload() {
            using MemoryStream stream = new MemoryStream();
            using (EngineBinaryWriter writer = VersionedBinaryPayload.WriteHeader(stream, EngineBinaryEndianness.LittleEndian, 1, 7, 11, 42)) {
                writer.WriteInt32(555);
            }

            stream.Position = 0;
            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => VersionedBinaryPayload.ValidateHeader(
                    stream,
                    header,
                    7,
                    11,
                    42,
                    2,
                    "test payload",
                    "test payload",
                    "Test payload",
                    VersionedBinaryVersionMismatchStyle.RequiredVersionSubjectFirst,
                    "Regenerate the test payload."));

            Assert.Equal("Test payload version '1' is unsupported; version '2' is required. Regenerate the test payload.", exception.Message);
        }

        [Fact]
        public void ValidateHeaderWithDispatchedValueKind_WhenHeaderWasAlreadyRead_IgnoresValueKind() {
            using MemoryStream stream = new MemoryStream();
            using (EngineBinaryWriter writer = VersionedBinaryPayload.WriteHeader(stream, EngineBinaryEndianness.LittleEndian, 5, 7, 11, 99)) {
                writer.WriteInt32(777);
            }

            stream.Position = 0;
            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            using EngineBinaryReader reader = VersionedBinaryPayload.ValidateHeaderWithDispatchedValueKind(
                stream,
                header,
                7,
                11,
                5,
                "dispatched payload",
                "dispatched payload",
                "Dispatched payload",
                VersionedBinaryVersionMismatchStyle.RequiredVersionSubjectFirst,
                "Regenerate the dispatched payload.");

            Assert.Equal((ushort)99, header.ValueKind);
            Assert.Equal(777, reader.ReadInt32());
        }
    }
}

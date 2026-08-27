using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies the tolerant tagged field container used by editor scene component payloads.
    /// </summary>
    public class EditorTaggedSceneComponentFieldReaderTests {
        /// <summary>
        /// Ensures known fields can still be read when the payload also contains unknown fields.
        /// </summary>
        [Fact]
        public void Read_WhenPayloadContainsUnknownField_IgnoresTheUnknownField() {
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("Known", fieldWriter => fieldWriter.WriteByte(7));
            writer.WriteField("FutureField", fieldWriter => fieldWriter.WriteString("ignored"));

            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(writer.BuildPayload());

            Assert.True(reader.TryGetFieldReader("Known", out EngineBinaryReader knownFieldReader));
            Assert.Equal((byte)7, knownFieldReader.ReadByte());
        }

        /// <summary>
        /// Ensures missing fields can be detected without throwing so callers can preserve component defaults.
        /// </summary>
        [Fact]
        public void Read_WhenFieldIsMissing_ReturnsFalse() {
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("Known", fieldWriter => fieldWriter.WriteByte(3));

            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(writer.BuildPayload());

            Assert.False(reader.TryGetFieldReader("Missing", out EngineBinaryReader missingFieldReader));
            Assert.Null(missingFieldReader);
        }

        /// <summary>
        /// Ensures an empty tagged payload reports current-format guidance instead of exposing raw end-of-stream failure.
        /// </summary>
        [Fact]
        public void Read_WhenPayloadIsEmpty_ThrowsCurrentFormatError() {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new EditorTaggedSceneComponentFieldReader(Array.Empty<byte>()));

            Assert.Contains("current version", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("field count", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("rebuild", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures a tagged payload truncated before its field-count header reports current-format guidance.
        /// </summary>
        [Fact]
        public void Read_WhenPayloadIsTruncatedBeforeFieldCount_ThrowsCurrentFormatError() {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new EditorTaggedSceneComponentFieldReader(new[] { EditorTaggedSceneComponentPayloadFormat.CurrentVersion }));

            Assert.Contains("field count", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("rebuild", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures an unsupported tagged payload version identifies the received and current versions with regeneration guidance.
        /// </summary>
        [Fact]
        public void Read_WhenPayloadVersionIsWrong_ThrowsCurrentFormatError() {
            using MemoryStream stream = new MemoryStream();
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian)) {
                writer.WriteByte(2);
                writer.WriteInt32(0);
            }

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new EditorTaggedSceneComponentFieldReader(stream.ToArray()));

            Assert.Contains("received", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("2", exception.Message, StringComparison.Ordinal);
            Assert.Contains("current", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(EditorTaggedSceneComponentPayloadFormat.CurrentVersion.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains("regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("rebuild", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures an unsupported tagged field count identifies the received and current count contract with regeneration guidance.
        /// </summary>
        [Fact]
        public void Read_WhenFieldCountIsNegative_ThrowsCurrentFormatError() {
            using MemoryStream stream = new MemoryStream();
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian)) {
                writer.WriteByte(EditorTaggedSceneComponentPayloadFormat.CurrentVersion);
                writer.WriteInt32(-1);
            }

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new EditorTaggedSceneComponentFieldReader(stream.ToArray()));

            Assert.Contains("received", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("-1", exception.Message, StringComparison.Ordinal);
            Assert.Contains("current", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("field count", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("rebuild", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures a tagged payload truncated inside a field header reports current-format guidance.
        /// </summary>
        [Fact]
        public void Read_WhenFieldPayloadIsTruncated_ThrowsCurrentFormatError() {
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("Known", fieldWriter => fieldWriter.WriteByte(7));
            byte[] payload = writer.BuildPayload();
            Array.Resize(ref payload, payload.Length - 1);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new EditorTaggedSceneComponentFieldReader(payload));

            Assert.Contains("truncated", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("rebuild", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}

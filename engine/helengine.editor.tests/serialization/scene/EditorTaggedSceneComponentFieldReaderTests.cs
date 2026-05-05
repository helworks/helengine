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
    }
}

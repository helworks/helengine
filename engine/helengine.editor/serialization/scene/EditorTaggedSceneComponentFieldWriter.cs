namespace helengine.editor {
    /// <summary>
    /// Builds one tolerant editor scene component payload from named binary fields.
    /// </summary>
    public sealed class EditorTaggedSceneComponentFieldWriter {
        /// <summary>
        /// Field names written into the payload in the order they were added.
        /// </summary>
        readonly List<string> FieldNames;

        /// <summary>
        /// Raw field payload bytes written into the payload in the order they were added.
        /// </summary>
        readonly List<byte[]> FieldPayloads;

        /// <summary>
        /// Initializes an empty tagged field writer.
        /// </summary>
        public EditorTaggedSceneComponentFieldWriter() {
            FieldNames = new List<string>();
            FieldPayloads = new List<byte[]>();
        }

        /// <summary>
        /// Appends one named field to the payload being built.
        /// </summary>
        /// <param name="fieldName">Stable field name.</param>
        /// <param name="writeFieldPayload">Delegate that writes the field payload bytes.</param>
        public void WriteField(string fieldName, Action<EngineBinaryWriter> writeFieldPayload) {
            if (string.IsNullOrWhiteSpace(fieldName)) {
                throw new ArgumentException("Field name must be provided.", nameof(fieldName));
            } else if (writeFieldPayload == null) {
                throw new ArgumentNullException(nameof(writeFieldPayload));
            } else if (FieldNames.Contains(fieldName, StringComparer.Ordinal)) {
                throw new InvalidOperationException($"Editor scene component payloads cannot contain duplicate field '{fieldName}'.");
            }

            using MemoryStream fieldStream = new MemoryStream();
            using (EngineBinaryWriter fieldWriter = EngineBinaryWriter.Create(fieldStream, EngineBinaryEndianness.LittleEndian)) {
                writeFieldPayload(fieldWriter);
            }

            FieldNames.Add(fieldName);
            FieldPayloads.Add(fieldStream.ToArray());
        }

        /// <summary>
        /// Finalizes the accumulated fields into one editor-scene component payload.
        /// </summary>
        /// <returns>Serialized payload bytes.</returns>
        public byte[] BuildPayload() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(EditorTaggedSceneComponentPayloadFormat.CurrentVersion);
            writer.WriteInt32(FieldNames.Count);

            for (int index = 0; index < FieldNames.Count; index++) {
                writer.WriteString(FieldNames[index]);
                writer.WriteByteArray(FieldPayloads[index]);
            }

            return stream.ToArray();
        }
    }
}

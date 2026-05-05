namespace helengine.editor {
    /// <summary>
    /// Reads named fields from one tolerant editor scene component payload.
    /// </summary>
    public sealed class EditorTaggedSceneComponentFieldReader {
        /// <summary>
        /// Raw field payload bytes keyed by stable field name.
        /// </summary>
        readonly Dictionary<string, byte[]> FieldPayloadsByName;

        /// <summary>
        /// Initializes a reader over one serialized editor component payload.
        /// </summary>
        /// <param name="payload">Serialized payload bytes to parse.</param>
        public EditorTaggedSceneComponentFieldReader(byte[] payload) {
            if (payload == null) {
                throw new ArgumentNullException(nameof(payload));
            }

            FieldPayloadsByName = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            using MemoryStream stream = new MemoryStream(payload, false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != EditorTaggedSceneComponentPayloadFormat.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported editor tagged scene component payload version '{version}'.");
            }

            int fieldCount = reader.ReadInt32();
            if (fieldCount < 0) {
                throw new InvalidOperationException("Editor tagged scene component payload field counts cannot be negative.");
            }

            for (int index = 0; index < fieldCount; index++) {
                string fieldName = reader.ReadString();
                if (string.IsNullOrWhiteSpace(fieldName)) {
                    throw new InvalidOperationException("Editor tagged scene component payload fields must define a name.");
                } else if (FieldPayloadsByName.ContainsKey(fieldName)) {
                    throw new InvalidOperationException($"Editor scene component payloads cannot contain duplicate field '{fieldName}'.");
                }

                FieldPayloadsByName.Add(fieldName, reader.ReadByteArray() ?? Array.Empty<byte>());
            }
        }

        /// <summary>
        /// Attempts to open one named field payload for reading.
        /// </summary>
        /// <param name="fieldName">Stable field name to resolve.</param>
        /// <param name="fieldReader">Reader over the field payload when found.</param>
        /// <returns>True when the field exists; otherwise false.</returns>
        public bool TryGetFieldReader(string fieldName, out EngineBinaryReader fieldReader) {
            if (string.IsNullOrWhiteSpace(fieldName)) {
                throw new ArgumentException("Field name must be provided.", nameof(fieldName));
            }

            if (!FieldPayloadsByName.TryGetValue(fieldName, out byte[] payload)) {
                fieldReader = null;
                return false;
            }

            fieldReader = EngineBinaryReader.Create(new MemoryStream(payload, false), EngineBinaryEndianness.LittleEndian, false);
            return true;
        }
    }
}

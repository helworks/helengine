namespace helengine.editor {
    /// <summary>
    /// Reads named fields from one current editor scene component payload.
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
            byte? receivedVersion = null;
            int? receivedFieldCount = null;
            try {
                using MemoryStream stream = new MemoryStream(payload, false);
                using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
                receivedVersion = reader.ReadByte();
                if (receivedVersion != EditorTaggedSceneComponentPayloadFormat.CurrentVersion) {
                    throw new InvalidOperationException(
                        $"Unsupported editor tagged scene component payload received version '{receivedVersion}'; current version '{EditorTaggedSceneComponentPayloadFormat.CurrentVersion}' is required. Regenerate/rebuild the asset in the current format.");
                }

                receivedFieldCount = reader.ReadInt32();
                if (receivedFieldCount < 0) {
                    throw new InvalidOperationException(
                        $"Unsupported editor tagged scene component payload received field count '{receivedFieldCount}'; current field count must be non-negative. Regenerate/rebuild the asset in the current format.");
                }

                for (int index = 0; index < receivedFieldCount; index++) {
                    string fieldName = reader.ReadString();
                    if (string.IsNullOrWhiteSpace(fieldName)) {
                        throw new InvalidOperationException("Editor scene component payload fields must define a name in the current format. Regenerate/rebuild the asset.");
                    } else if (FieldPayloadsByName.ContainsKey(fieldName)) {
                        throw new InvalidOperationException($"Editor scene component payloads cannot contain duplicate field '{fieldName}' in the current format. Regenerate/rebuild the asset.");
                    }

                    FieldPayloadsByName.Add(fieldName, reader.ReadByteArray() ?? Array.Empty<byte>());
                }

                if (stream.Position != stream.Length) {
                    throw new InvalidOperationException(
                        $"Editor tagged scene component payload contains trailing data after current field count '{receivedFieldCount}'. Regenerate/rebuild the asset in the current format.");
                }
            } catch (EndOfStreamException exception) {
                string versionText = receivedVersion.HasValue
                    ? $"received version '{receivedVersion.Value}', current version '{EditorTaggedSceneComponentPayloadFormat.CurrentVersion}'"
                    : $"current version '{EditorTaggedSceneComponentPayloadFormat.CurrentVersion}' (received version unavailable)";
                string countText = receivedFieldCount.HasValue
                    ? $"received field count '{receivedFieldCount.Value}'"
                    : "received field count unavailable";
                throw new InvalidOperationException(
                    $"Editor tagged scene component payload is truncated ({versionText}; {countText}). Regenerate/rebuild the asset in the current format.",
                    exception);
            }
        }

        /// <summary>
        /// Gets the field names carried by the current tagged payload for exact schema validation.
        /// </summary>
        internal IReadOnlyCollection<string> FieldNames => FieldPayloadsByName.Keys;

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

        /// <summary>
        /// Attempts to read the raw byte length of one current tagged field payload.
        /// </summary>
        /// <param name="fieldName">Current persisted field name.</param>
        /// <param name="payloadLength">Raw payload length when the field exists.</param>
        /// <returns>True when the field exists; otherwise false.</returns>
        internal bool TryGetFieldPayloadLength(string fieldName, out int payloadLength) {
            if (string.IsNullOrWhiteSpace(fieldName)) {
                payloadLength = 0;
                return false;
            }

            if (FieldPayloadsByName.TryGetValue(fieldName, out byte[] payload)) {
                payloadLength = payload.Length;
                return true;
            }

            payloadLength = 0;
            return false;
        }
    }
}

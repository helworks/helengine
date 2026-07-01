namespace helengine {
    /// <summary>
    /// Stores one engine-owned binary payload produced through the standard HELE header and endian-aware writer path.
    /// </summary>
    public sealed class EngineSerializedPayload {
        /// <summary>
        /// Stable record kind used for nested engine-owned binary payload containers.
        /// </summary>
        const ushort PayloadRecordKind = 0x4550;

        /// <summary>
        /// Stable value kind used for nested engine-owned binary payload containers.
        /// </summary>
        const ushort PayloadValueKind = 0x4C44;

        /// <summary>
        /// Backing field for the logical payload format identifier.
        /// </summary>
        readonly string FormatIdValue;

        /// <summary>
        /// Backing field for the serialized HELE payload bytes.
        /// </summary>
        readonly byte[] SerializedBytesValue;

        /// <summary>
        /// Initializes one serialized payload from validated persisted bytes.
        /// </summary>
        /// <param name="formatId">Logical payload format identifier.</param>
        /// <param name="serializedBytes">Validated HELE payload bytes.</param>
        EngineSerializedPayload(string formatId, byte[] serializedBytes) {
            if (string.IsNullOrWhiteSpace(formatId)) {
                throw new ArgumentException("Payload format id must be provided.", nameof(formatId));
            } else if (serializedBytes == null) {
                throw new ArgumentNullException(nameof(serializedBytes));
            } else if (serializedBytes.Length == 0) {
                throw new ArgumentOutOfRangeException(nameof(serializedBytes), "Serialized payload bytes must not be empty.");
            }

            FormatIdValue = formatId;
            SerializedBytesValue = [.. serializedBytes];
        }

        /// <summary>
        /// Gets the logical payload format identifier used by runtime feature guards.
        /// </summary>
        public string FormatId {
            get { return FormatIdValue; }
        }

        /// <summary>
        /// Creates one payload by letting Helengine own the final serialized byte buffer and HELE header emission.
        /// </summary>
        /// <param name="formatId">Logical payload format identifier used by the consuming runtime.</param>
        /// <param name="binaryFormatId">Stable binary serializer format identifier written into the HELE header.</param>
        /// <param name="version">Stable binary serializer version written into the HELE header.</param>
        /// <param name="endianness">Byte order used for the payload body.</param>
        /// <param name="writePayload">Writer callback that serializes the payload fields.</param>
        /// <returns>Constructed serialized payload.</returns>
        public static EngineSerializedPayload Create(
            string formatId,
            ushort binaryFormatId,
            byte version,
            EngineBinaryEndianness endianness,
            Action<EngineBinaryWriter> writePayload) {
            if (string.IsNullOrWhiteSpace(formatId)) {
                throw new ArgumentException("Payload format id must be provided.", nameof(formatId));
            } else if (writePayload == null) {
                throw new ArgumentNullException(nameof(writePayload));
            }

            using MemoryStream stream = new MemoryStream();
            EngineBinaryHeaderSerializer.Write(
                stream,
                new EngineBinaryHeader(
                    endianness,
                    version,
                    binaryFormatId,
                    PayloadRecordKind,
                    PayloadValueKind));
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, endianness, true)) {
                writePayload(writer);
            }

            return new EngineSerializedPayload(formatId, stream.ToArray());
        }

        /// <summary>
        /// Opens one payload reader after validating the logical format id together with the HELE header metadata.
        /// </summary>
        /// <param name="expectedFormatId">Expected logical payload format identifier.</param>
        /// <param name="expectedBinaryFormatId">Expected binary serializer format identifier.</param>
        /// <param name="expectedVersion">Expected binary serializer version.</param>
        /// <returns>Endian-aware reader positioned at the payload body.</returns>
        public EngineBinaryReader CreatePayloadReader(string expectedFormatId, ushort expectedBinaryFormatId, byte expectedVersion) {
            if (string.IsNullOrWhiteSpace(expectedFormatId)) {
                throw new ArgumentException("Expected payload format id must be provided.", nameof(expectedFormatId));
            } else if (!string.Equals(FormatIdValue, expectedFormatId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Serialized payload format '{FormatIdValue}' does not match expected format '{expectedFormatId}'.");
            }

            MemoryStream stream = new MemoryStream(SerializedBytesValue, false);
            EngineBinaryHeader header;
            try {
                header = EngineBinaryHeaderSerializer.Read(stream);
            } catch {
                stream.Dispose();
                throw;
            }

            try {
                ValidateHeader(header, expectedBinaryFormatId, expectedVersion);
                return EngineBinaryReader.Create(stream, header.Endianness, false);
            } catch {
                stream.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Restores one validated serialized payload from persisted bytes emitted earlier by Helengine.
        /// </summary>
        /// <param name="formatId">Logical payload format identifier.</param>
        /// <param name="serializedBytes">Persisted HELE payload bytes.</param>
        /// <returns>Restored serialized payload.</returns>
        internal static EngineSerializedPayload Restore(string formatId, byte[] serializedBytes) {
            if (string.IsNullOrWhiteSpace(formatId)) {
                throw new ArgumentException("Payload format id must be provided.", nameof(formatId));
            } else if (serializedBytes == null) {
                throw new ArgumentNullException(nameof(serializedBytes));
            } else if (serializedBytes.Length == 0) {
                throw new ArgumentOutOfRangeException(nameof(serializedBytes), "Serialized payload bytes must not be empty.");
            }

            using MemoryStream stream = new MemoryStream(serializedBytes, false);
            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            ValidateHeaderKinds(header);
            return new EngineSerializedPayload(formatId, serializedBytes);
        }

        /// <summary>
        /// Returns one defensive copy of the serialized bytes for engine-owned persistence flows.
        /// </summary>
        /// <returns>Copy of the serialized HELE payload bytes.</returns>
        internal byte[] GetSerializedBytesForPersistence() {
            return [.. SerializedBytesValue];
        }

        /// <summary>
        /// Validates the HELE header metadata expected for one reopened payload reader.
        /// </summary>
        /// <param name="header">Header to validate.</param>
        /// <param name="expectedBinaryFormatId">Expected binary serializer format identifier.</param>
        /// <param name="expectedVersion">Expected binary serializer version.</param>
        static void ValidateHeader(EngineBinaryHeader header, ushort expectedBinaryFormatId, byte expectedVersion) {
            if (header == null) {
                throw new ArgumentNullException(nameof(header));
            }

            ValidateHeaderKinds(header);
            if (header.FormatId != expectedBinaryFormatId) {
                throw new InvalidOperationException($"Serialized payload binary format '{header.FormatId}' does not match expected format '{expectedBinaryFormatId}'.");
            } else if (header.Version != expectedVersion) {
                throw new InvalidOperationException($"Serialized payload binary version '{header.Version}' does not match expected version '{expectedVersion}'.");
            }
        }

        /// <summary>
        /// Validates the shared nested-payload record and value kinds used by the engine payload container.
        /// </summary>
        /// <param name="header">Header to validate.</param>
        static void ValidateHeaderKinds(EngineBinaryHeader header) {
            if (header == null) {
                throw new ArgumentNullException(nameof(header));
            }
            if (header.RecordKind != PayloadRecordKind) {
                throw new InvalidOperationException($"Serialized payload record kind '{header.RecordKind}' is not supported.");
            } else if (header.ValueKind != PayloadValueKind) {
                throw new InvalidOperationException($"Serialized payload value kind '{header.ValueKind}' is not supported.");
            }
        }
    }
}

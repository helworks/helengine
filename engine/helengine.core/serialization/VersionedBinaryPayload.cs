namespace helengine {
    /// <summary>
    /// Owns the header-construction, strict version-validation, and endianness-selection ceremony shared by the engine's versioned HELE binary payload serializers, so each serializer only supplies its format identifiers, expected values, and diagnostic wording.
    /// </summary>
    public static class VersionedBinaryPayload {
        /// <summary>
        /// Writes the standardized HELE header for one versioned binary payload and returns a writer configured for the header's endianness, ready to receive the payload body.
        /// </summary>
        /// <param name="stream">Destination stream for the header and payload.</param>
        /// <param name="endianness">Payload endianness to record in the header and to configure the returned writer with.</param>
        /// <param name="version">Serializer version for the payload layout being written.</param>
        /// <param name="formatId">Serializer format identifier shared by every payload in the same binary file family.</param>
        /// <param name="recordKind">Logical record kind stored in the payload.</param>
        /// <param name="valueKind">Concrete value type stored in the payload.</param>
        /// <returns>Writer configured for the header's endianness, positioned immediately after the written header.</returns>
        public static EngineBinaryWriter WriteHeader(
            Stream stream,
            EngineBinaryEndianness endianness,
            byte version,
            ushort formatId,
            ushort recordKind,
            ushort valueKind) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            EngineBinaryHeader header = new EngineBinaryHeader(endianness, version, formatId, recordKind, valueKind);
            EngineBinaryHeaderSerializer.Write(stream, header);
            return EngineBinaryWriter.Create(stream, endianness);
        }

        /// <summary>
        /// Reads and strictly validates the standardized HELE header for one versioned binary payload whose format id, record kind, value kind, and version are each pinned to one expected value, then returns a reader configured for the header's endianness.
        /// </summary>
        /// <param name="stream">Source stream containing the header and payload.</param>
        /// <param name="expectedFormatId">Serializer format identifier the payload must match.</param>
        /// <param name="expectedRecordKind">Logical record kind the payload must match.</param>
        /// <param name="expectedValueKind">Concrete value type the payload must match.</param>
        /// <param name="expectedVersion">Serializer version the payload must match.</param>
        /// <param name="headerMismatchSubject">Human-readable payload description used in the format-id, record-kind, and value-kind mismatch messages.</param>
        /// <param name="versionMismatchSubject">Human-readable payload description used in the version mismatch message.</param>
        /// <param name="regenerateInstruction">Trailing guidance appended to the version mismatch message describing how to recover, or an empty string to omit the current-version suffix entirely.</param>
        /// <param name="header">Receives the decoded and validated header.</param>
        /// <returns>Reader configured for the header's endianness, positioned immediately after the header.</returns>
        public static EngineBinaryReader ReadHeader(
            Stream stream,
            ushort expectedFormatId,
            ushort expectedRecordKind,
            ushort expectedValueKind,
            byte expectedVersion,
            string headerMismatchSubject,
            string versionMismatchSubject,
            string regenerateInstruction,
            out EngineBinaryHeader header) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (string.IsNullOrWhiteSpace(headerMismatchSubject)) {
                throw new ArgumentException("Header mismatch subject is required.", nameof(headerMismatchSubject));
            } else if (string.IsNullOrWhiteSpace(versionMismatchSubject)) {
                throw new ArgumentException("Version mismatch subject is required.", nameof(versionMismatchSubject));
            } else if (regenerateInstruction == null) {
                throw new ArgumentNullException(nameof(regenerateInstruction));
            }

            header = EngineBinaryHeaderSerializer.Read(stream);
            if (header.FormatId != expectedFormatId) {
                throw new InvalidOperationException($"Unsupported {headerMismatchSubject} format id '{header.FormatId}'.");
            } else if (header.RecordKind != expectedRecordKind) {
                throw new InvalidOperationException($"Unexpected {headerMismatchSubject} record kind '{header.RecordKind}'.");
            } else if (header.ValueKind != expectedValueKind) {
                throw new InvalidOperationException($"Unexpected {headerMismatchSubject} value kind '{header.ValueKind}'.");
            } else if (header.Version != expectedVersion) {
                ThrowVersionMismatch(header.Version, expectedVersion, versionMismatchSubject, regenerateInstruction);
            }

            return EngineBinaryReader.Create(stream, header.Endianness);
        }

        /// <summary>
        /// Reads and strictly validates the standardized HELE header for one versioned binary payload whose value kind selects among several supported layouts and is therefore validated by the caller, then returns a reader configured for the header's endianness.
        /// </summary>
        /// <param name="stream">Source stream containing the header and payload.</param>
        /// <param name="expectedFormatId">Serializer format identifier the payload must match.</param>
        /// <param name="expectedRecordKind">Logical record kind the payload must match.</param>
        /// <param name="expectedVersion">Serializer version the payload must match.</param>
        /// <param name="headerMismatchSubject">Human-readable payload description used in the format-id and record-kind mismatch messages.</param>
        /// <param name="versionMismatchSubject">Human-readable payload description used in the version mismatch message.</param>
        /// <param name="regenerateInstruction">Trailing guidance appended to the version mismatch message describing how to recover, or an empty string to omit the current-version suffix entirely.</param>
        /// <param name="header">Receives the decoded and validated header.</param>
        /// <returns>Reader configured for the header's endianness, positioned immediately after the header.</returns>
        public static EngineBinaryReader ReadHeaderWithDispatchedValueKind(
            Stream stream,
            ushort expectedFormatId,
            ushort expectedRecordKind,
            byte expectedVersion,
            string headerMismatchSubject,
            string versionMismatchSubject,
            string regenerateInstruction,
            out EngineBinaryHeader header) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (string.IsNullOrWhiteSpace(headerMismatchSubject)) {
                throw new ArgumentException("Header mismatch subject is required.", nameof(headerMismatchSubject));
            } else if (string.IsNullOrWhiteSpace(versionMismatchSubject)) {
                throw new ArgumentException("Version mismatch subject is required.", nameof(versionMismatchSubject));
            } else if (regenerateInstruction == null) {
                throw new ArgumentNullException(nameof(regenerateInstruction));
            }

            header = EngineBinaryHeaderSerializer.Read(stream);
            if (header.FormatId != expectedFormatId) {
                throw new InvalidOperationException($"Unsupported {headerMismatchSubject} format id '{header.FormatId}'.");
            } else if (header.RecordKind != expectedRecordKind) {
                throw new InvalidOperationException($"Unexpected {headerMismatchSubject} record kind '{header.RecordKind}'.");
            } else if (header.Version != expectedVersion) {
                ThrowVersionMismatch(header.Version, expectedVersion, versionMismatchSubject, regenerateInstruction);
            }

            return EngineBinaryReader.Create(stream, header.Endianness);
        }

        /// <summary>
        /// Throws the standardized version-mismatch exception for one versioned binary payload.
        /// </summary>
        /// <param name="actualVersion">Serializer version decoded from the payload header.</param>
        /// <param name="expectedVersion">Serializer version the payload was required to match.</param>
        /// <param name="versionMismatchSubject">Human-readable payload description used in the mismatch message.</param>
        /// <param name="regenerateInstruction">Trailing guidance appended to the mismatch message describing how to recover, or an empty string to omit the current-version suffix entirely.</param>
        static void ThrowVersionMismatch(byte actualVersion, byte expectedVersion, string versionMismatchSubject, string regenerateInstruction) {
            if (regenerateInstruction.Length == 0) {
                throw new InvalidOperationException($"Unsupported {versionMismatchSubject} binary version '{actualVersion}'.");
            }

            throw new InvalidOperationException(
                $"Unsupported {versionMismatchSubject} binary version received '{actualVersion}'; current version is '{expectedVersion}'. {regenerateInstruction}");
        }
    }
}

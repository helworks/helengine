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
        /// <param name="formatIdMismatchSubject">Human-readable payload description used in the format-id mismatch message.</param>
        /// <param name="recordMismatchSubject">Human-readable payload description used in the record-kind and value-kind mismatch messages.</param>
        /// <param name="versionMismatchSubject">Human-readable payload description used in the version mismatch message.</param>
        /// <param name="versionMismatchStyle">Wording to use when the payload version does not match.</param>
        /// <param name="regenerateInstruction">Trailing guidance appended to the version mismatch message describing how to recover; ignored by <see cref="VersionedBinaryVersionMismatchStyle.ReceivedVersionOnly"/>.</param>
        /// <returns>Reader configured for the header's endianness, positioned immediately after the header.</returns>
        public static EngineBinaryReader ReadHeader(
            Stream stream,
            ushort expectedFormatId,
            ushort expectedRecordKind,
            ushort expectedValueKind,
            byte expectedVersion,
            string formatIdMismatchSubject,
            string recordMismatchSubject,
            string versionMismatchSubject,
            VersionedBinaryVersionMismatchStyle versionMismatchStyle,
            string regenerateInstruction) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            return ValidateHeader(
                stream,
                header,
                expectedFormatId,
                expectedRecordKind,
                expectedValueKind,
                expectedVersion,
                formatIdMismatchSubject,
                recordMismatchSubject,
                versionMismatchSubject,
                versionMismatchStyle,
                regenerateInstruction);
        }

        /// <summary>
        /// Reads and strictly validates the standardized HELE header for one versioned binary payload whose value kind selects among several supported layouts and is therefore validated by the caller, then returns a reader configured for the header's endianness.
        /// </summary>
        /// <param name="stream">Source stream containing the header and payload.</param>
        /// <param name="expectedFormatId">Serializer format identifier the payload must match.</param>
        /// <param name="expectedRecordKind">Logical record kind the payload must match.</param>
        /// <param name="expectedVersion">Serializer version the payload must match.</param>
        /// <param name="formatIdMismatchSubject">Human-readable payload description used in the format-id mismatch message.</param>
        /// <param name="recordMismatchSubject">Human-readable payload description used in the record-kind mismatch message.</param>
        /// <param name="versionMismatchSubject">Human-readable payload description used in the version mismatch message.</param>
        /// <param name="versionMismatchStyle">Wording to use when the payload version does not match.</param>
        /// <param name="regenerateInstruction">Trailing guidance appended to the version mismatch message describing how to recover; ignored by <see cref="VersionedBinaryVersionMismatchStyle.ReceivedVersionOnly"/>.</param>
        /// <param name="header">Receives the decoded and validated header so the caller can dispatch on its value kind.</param>
        /// <returns>Reader configured for the header's endianness, positioned immediately after the header.</returns>
        public static EngineBinaryReader ReadHeaderWithDispatchedValueKind(
            Stream stream,
            ushort expectedFormatId,
            ushort expectedRecordKind,
            byte expectedVersion,
            string formatIdMismatchSubject,
            string recordMismatchSubject,
            string versionMismatchSubject,
            VersionedBinaryVersionMismatchStyle versionMismatchStyle,
            string regenerateInstruction,
            out EngineBinaryHeader header) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            header = EngineBinaryHeaderSerializer.Read(stream);
            return ValidateHeaderWithDispatchedValueKind(
                stream,
                header,
                expectedFormatId,
                expectedRecordKind,
                expectedVersion,
                formatIdMismatchSubject,
                recordMismatchSubject,
                versionMismatchSubject,
                versionMismatchStyle,
                regenerateInstruction);
        }

        /// <summary>
        /// Strictly validates an already-decoded HELE header whose format id, record kind, value kind, and version are each pinned to one expected value, then returns a reader configured for the header's endianness, serving serializers that expose a deserialize overload taking a header the caller already read.
        /// </summary>
        /// <param name="stream">Source stream positioned immediately after the decoded header.</param>
        /// <param name="header">Header the caller already decoded from the stream.</param>
        /// <param name="expectedFormatId">Serializer format identifier the payload must match.</param>
        /// <param name="expectedRecordKind">Logical record kind the payload must match.</param>
        /// <param name="expectedValueKind">Concrete value type the payload must match.</param>
        /// <param name="expectedVersion">Serializer version the payload must match.</param>
        /// <param name="formatIdMismatchSubject">Human-readable payload description used in the format-id mismatch message.</param>
        /// <param name="recordMismatchSubject">Human-readable payload description used in the record-kind and value-kind mismatch messages.</param>
        /// <param name="versionMismatchSubject">Human-readable payload description used in the version mismatch message.</param>
        /// <param name="versionMismatchStyle">Wording to use when the payload version does not match.</param>
        /// <param name="regenerateInstruction">Trailing guidance appended to the version mismatch message describing how to recover; ignored by <see cref="VersionedBinaryVersionMismatchStyle.ReceivedVersionOnly"/>.</param>
        /// <returns>Reader configured for the header's endianness, positioned at the payload body.</returns>
        public static EngineBinaryReader ValidateHeader(
            Stream stream,
            [NativeNoEscape] EngineBinaryHeader header,
            ushort expectedFormatId,
            ushort expectedRecordKind,
            ushort expectedValueKind,
            byte expectedVersion,
            string formatIdMismatchSubject,
            string recordMismatchSubject,
            string versionMismatchSubject,
            VersionedBinaryVersionMismatchStyle versionMismatchStyle,
            string regenerateInstruction) {
            ValidateArguments(stream, header, formatIdMismatchSubject, recordMismatchSubject, versionMismatchSubject, regenerateInstruction);
            if (header.FormatId != expectedFormatId) {
                throw new InvalidOperationException($"Unsupported {formatIdMismatchSubject} format id '{header.FormatId}'.");
            } else if (header.RecordKind != expectedRecordKind) {
                throw new InvalidOperationException($"Unexpected {recordMismatchSubject} record kind '{header.RecordKind}'.");
            } else if (header.ValueKind != expectedValueKind) {
                throw new InvalidOperationException($"Unexpected {recordMismatchSubject} value kind '{header.ValueKind}'.");
            } else if (header.Version != expectedVersion) {
                ThrowVersionMismatch(header.Version, expectedVersion, versionMismatchSubject, versionMismatchStyle, regenerateInstruction);
            }

            return EngineBinaryReader.Create(stream, header.Endianness);
        }

        /// <summary>
        /// Strictly validates an already-decoded HELE header whose value kind selects among several supported layouts and is therefore validated by the caller, then returns a reader configured for the header's endianness.
        /// </summary>
        /// <param name="stream">Source stream positioned immediately after the decoded header.</param>
        /// <param name="header">Header the caller already decoded from the stream.</param>
        /// <param name="expectedFormatId">Serializer format identifier the payload must match.</param>
        /// <param name="expectedRecordKind">Logical record kind the payload must match.</param>
        /// <param name="expectedVersion">Serializer version the payload must match.</param>
        /// <param name="formatIdMismatchSubject">Human-readable payload description used in the format-id mismatch message.</param>
        /// <param name="recordMismatchSubject">Human-readable payload description used in the record-kind mismatch message.</param>
        /// <param name="versionMismatchSubject">Human-readable payload description used in the version mismatch message.</param>
        /// <param name="versionMismatchStyle">Wording to use when the payload version does not match.</param>
        /// <param name="regenerateInstruction">Trailing guidance appended to the version mismatch message describing how to recover; ignored by <see cref="VersionedBinaryVersionMismatchStyle.ReceivedVersionOnly"/>.</param>
        /// <returns>Reader configured for the header's endianness, positioned at the payload body.</returns>
        public static EngineBinaryReader ValidateHeaderWithDispatchedValueKind(
            Stream stream,
            [NativeNoEscape] EngineBinaryHeader header,
            ushort expectedFormatId,
            ushort expectedRecordKind,
            byte expectedVersion,
            string formatIdMismatchSubject,
            string recordMismatchSubject,
            string versionMismatchSubject,
            VersionedBinaryVersionMismatchStyle versionMismatchStyle,
            string regenerateInstruction) {
            ValidateArguments(stream, header, formatIdMismatchSubject, recordMismatchSubject, versionMismatchSubject, regenerateInstruction);
            if (header.FormatId != expectedFormatId) {
                throw new InvalidOperationException($"Unsupported {formatIdMismatchSubject} format id '{header.FormatId}'.");
            } else if (header.RecordKind != expectedRecordKind) {
                throw new InvalidOperationException($"Unexpected {recordMismatchSubject} record kind '{header.RecordKind}'.");
            } else if (header.Version != expectedVersion) {
                ThrowVersionMismatch(header.Version, expectedVersion, versionMismatchSubject, versionMismatchStyle, regenerateInstruction);
            }

            return EngineBinaryReader.Create(stream, header.Endianness);
        }

        /// <summary>
        /// Rejects a missing stream, a missing header, or missing diagnostic wording before any header field is inspected, so every validation entry point fails the same way on caller mistakes.
        /// </summary>
        /// <param name="stream">Source stream the payload body will be read from.</param>
        /// <param name="header">Header to validate.</param>
        /// <param name="formatIdMismatchSubject">Human-readable payload description used in the format-id mismatch message.</param>
        /// <param name="recordMismatchSubject">Human-readable payload description used in the record-kind and value-kind mismatch messages.</param>
        /// <param name="versionMismatchSubject">Human-readable payload description used in the version mismatch message.</param>
        /// <param name="regenerateInstruction">Trailing guidance appended to the version mismatch message.</param>
        static void ValidateArguments(
            [NativeNoEscape] Stream stream,
            [NativeNoEscape] EngineBinaryHeader header,
            string formatIdMismatchSubject,
            string recordMismatchSubject,
            string versionMismatchSubject,
            string regenerateInstruction) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (header == null) {
                throw new ArgumentNullException(nameof(header));
            } else if (string.IsNullOrWhiteSpace(formatIdMismatchSubject)) {
                throw new ArgumentException("Format id mismatch subject is required.", nameof(formatIdMismatchSubject));
            } else if (string.IsNullOrWhiteSpace(recordMismatchSubject)) {
                throw new ArgumentException("Record mismatch subject is required.", nameof(recordMismatchSubject));
            } else if (string.IsNullOrWhiteSpace(versionMismatchSubject)) {
                throw new ArgumentException("Version mismatch subject is required.", nameof(versionMismatchSubject));
            } else if (regenerateInstruction == null) {
                throw new ArgumentNullException(nameof(regenerateInstruction));
            }
        }

        /// <summary>
        /// Throws the standardized version-mismatch exception for one versioned binary payload using the wording style that serializer published before.
        /// </summary>
        /// <param name="actualVersion">Serializer version decoded from the payload header.</param>
        /// <param name="expectedVersion">Serializer version the payload was required to match.</param>
        /// <param name="versionMismatchSubject">Human-readable payload description used in the mismatch message.</param>
        /// <param name="versionMismatchStyle">Wording to use for the mismatch message.</param>
        /// <param name="regenerateInstruction">Trailing guidance appended to the mismatch message describing how to recover.</param>
        static void ThrowVersionMismatch(
            byte actualVersion,
            byte expectedVersion,
            string versionMismatchSubject,
            VersionedBinaryVersionMismatchStyle versionMismatchStyle,
            string regenerateInstruction) {
            if (versionMismatchStyle == VersionedBinaryVersionMismatchStyle.ReceivedVersionOnly) {
                throw new InvalidOperationException($"Unsupported {versionMismatchSubject} binary version '{actualVersion}'.");
            } else if (versionMismatchStyle == VersionedBinaryVersionMismatchStyle.RequiredVersionSubjectFirst) {
                throw new InvalidOperationException(
                    $"{versionMismatchSubject} version '{actualVersion}' is unsupported; version '{expectedVersion}' is required. {regenerateInstruction}");
            }

            throw new InvalidOperationException(
                $"Unsupported {versionMismatchSubject} binary version received '{actualVersion}'; current version is '{expectedVersion}'. {regenerateInstruction}");
        }
    }
}

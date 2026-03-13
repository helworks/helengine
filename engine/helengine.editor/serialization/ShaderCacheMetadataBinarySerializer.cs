namespace helengine.editor {
    /// <summary>
    /// Serializes and deserializes shader cache metadata using the HELE binary header and editor payload layout.
    /// </summary>
    public static class ShaderCacheMetadataBinarySerializer {
        /// <summary>
        /// Record kind used for shader cache metadata payloads.
        /// </summary>
        public const EditorBinaryRecordKind RecordKind = EditorBinaryRecordKind.ShaderCacheMetadata;

        /// <summary>
        /// Value kind used for shader cache metadata payloads.
        /// </summary>
        public const ShaderCacheMetadataBinaryValueKind ValueKind = ShaderCacheMetadataBinaryValueKind.ShaderCacheMetadata;

        /// <summary>
        /// Serializer version for the current shader cache metadata payload layout.
        /// </summary>
        public const byte CurrentVersion = 1;

        /// <summary>
        /// Payload endianness used by the current shader cache metadata format.
        /// </summary>
        static readonly EngineBinaryEndianness PayloadEndianness = EngineBinaryEndianness.LittleEndian;

        /// <summary>
        /// Serializes shader cache metadata to the supplied stream.
        /// </summary>
        /// <param name="stream">Destination stream for the payload.</param>
        /// <param name="metadata">Metadata instance to serialize.</param>
        public static void Serialize(Stream stream, ShaderCacheMetadata metadata) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (metadata == null) {
                throw new ArgumentNullException(nameof(metadata));
            }

            EngineBinaryHeader header = new EngineBinaryHeader(
                PayloadEndianness,
                CurrentVersion,
                EditorAssetBinarySerializer.FormatId,
                (ushort)RecordKind,
                (ushort)ValueKind);

            EngineBinaryHeaderSerializer.Write(stream, header);
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, PayloadEndianness);
            writer.WriteString(metadata.SourceHash);
            writer.WriteInt64(metadata.SourceWriteTimeUtcTicks);
            writer.WriteInt64(metadata.SourceLengthBytes);
        }

        /// <summary>
        /// Deserializes shader cache metadata from the supplied stream.
        /// </summary>
        /// <param name="stream">Source stream containing the payload.</param>
        /// <returns>Deserialized metadata instance.</returns>
        public static ShaderCacheMetadata Deserialize(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            ValidateHeader(header);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, header.Endianness);
            return new ShaderCacheMetadata {
                SourceHash = reader.ReadString(),
                SourceWriteTimeUtcTicks = reader.ReadInt64(),
                SourceLengthBytes = reader.ReadInt64()
            };
        }

        /// <summary>
        /// Attempts to deserialize shader cache metadata without throwing for legacy non-HELE payloads.
        /// </summary>
        /// <param name="stream">Source stream containing the payload.</param>
        /// <param name="metadata">Deserialized metadata instance when the payload matches the current format.</param>
        /// <returns>True when metadata was deserialized successfully.</returns>
        public static bool TryDeserialize(Stream stream, out ShaderCacheMetadata metadata) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            metadata = null;
            EngineBinaryHeader header;
            if (!EngineBinaryHeaderSerializer.TryRead(stream, out header)) {
                return false;
            }

            if (!IsExpectedHeader(header)) {
                return false;
            }

            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, header.Endianness);
            metadata = new ShaderCacheMetadata {
                SourceHash = reader.ReadString(),
                SourceWriteTimeUtcTicks = reader.ReadInt64(),
                SourceLengthBytes = reader.ReadInt64()
            };
            return true;
        }

        /// <summary>
        /// Validates that the provided header matches the shader cache metadata format.
        /// </summary>
        /// <param name="header">Header metadata to validate.</param>
        static void ValidateHeader(EngineBinaryHeader header) {
            if (header == null) {
                throw new ArgumentNullException(nameof(header));
            } else if (header.FormatId != EditorAssetBinarySerializer.FormatId) {
                throw new InvalidOperationException($"Unsupported shader cache metadata format id '{header.FormatId}'.");
            } else if (header.RecordKind != (ushort)RecordKind) {
                throw new InvalidOperationException($"Unexpected shader cache metadata record kind '{header.RecordKind}'.");
            } else if (header.ValueKind != (ushort)ValueKind) {
                throw new InvalidOperationException($"Unexpected shader cache metadata value kind '{header.ValueKind}'.");
            } else if (header.Version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported shader cache metadata binary version '{header.Version}'.");
            }
        }

        /// <summary>
        /// Determines whether the header matches the current shader cache metadata format.
        /// </summary>
        /// <param name="header">Header metadata to evaluate.</param>
        /// <returns>True when the header matches the expected payload layout.</returns>
        static bool IsExpectedHeader(EngineBinaryHeader header) {
            if (header == null) {
                throw new ArgumentNullException(nameof(header));
            }

            return header.FormatId == EditorAssetBinarySerializer.FormatId &&
                header.RecordKind == (ushort)RecordKind &&
                header.ValueKind == (ushort)ValueKind &&
                header.Version == CurrentVersion;
        }
    }
}

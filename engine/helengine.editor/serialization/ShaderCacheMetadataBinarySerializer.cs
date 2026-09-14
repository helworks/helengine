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

            using EngineBinaryWriter writer = VersionedBinaryPayload.WriteHeader(
                stream,
                PayloadEndianness,
                CurrentVersion,
                EditorAssetBinarySerializer.FormatId,
                (ushort)RecordKind,
                (ushort)ValueKind);
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

            using EngineBinaryReader reader = VersionedBinaryPayload.ReadHeader(
                stream,
                EditorAssetBinarySerializer.FormatId,
                (ushort)RecordKind,
                (ushort)ValueKind,
                CurrentVersion,
                "shader cache metadata",
                "shader cache metadata",
                "shader cache metadata",
                VersionedBinaryVersionMismatchStyle.ReceivedVersionOnly,
                string.Empty);

            return new ShaderCacheMetadata {
                SourceHash = reader.ReadString(),
                SourceWriteTimeUtcTicks = reader.ReadInt64(),
                SourceLengthBytes = reader.ReadInt64()
            };
        }
    }
}

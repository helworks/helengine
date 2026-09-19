using helengine;

namespace helengine.files {
    /// <summary>
    /// Serializes and deserializes editor asset payloads using the engine's minimal HELE binary format.
    /// </summary>
    public static class EditorAssetBinarySerializer {
        /// <summary>
        /// Shared format identifier for editor-authored binary files.
        /// </summary>
        public const ushort FormatId = 1;

        /// <summary>
        /// Record kind used for serialized asset payloads.
        /// </summary>
        public const EditorBinaryRecordKind RecordKind = EditorBinaryRecordKind.Asset;

        /// <summary>
        /// Serializer version for the current editor asset payload layout.
        /// </summary>
        public const byte CurrentVersion = 25;

        /// <summary>
        /// Payload description used when a stored header carries a foreign format id.
        /// </summary>
        const string FormatIdMismatchSubject = "asset binary";

        /// <summary>
        /// Payload description used when a stored header carries an unexpected record kind.
        /// </summary>
        const string RecordMismatchSubject = "asset";

        /// <summary>
        /// Payload description used when a stored header carries an unsupported serializer version.
        /// </summary>
        const string VersionMismatchSubject = "Editor asset";

        /// <summary>
        /// Recovery guidance appended to the version mismatch message for stale authored assets.
        /// </summary>
        const string RegenerateInstruction = "Regenerate the authored asset.";

        /// <summary>
        /// Payload endianness used by the current editor asset format.
        /// </summary>
        static readonly EngineBinaryEndianness PayloadEndianness = EngineBinaryEndianness.LittleEndian;

        /// <summary>
        /// Serializes an asset to the supplied stream using the editor asset format.
        /// </summary>
        /// <param name="stream">Destination stream for the asset payload.</param>
        /// <param name="asset">Asset instance to serialize.</param>
        public static void Serialize(Stream stream, Asset asset) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            IEditorAssetPayloadSerializer payloadSerializer = ResolvePayloadSerializer(asset);
            payloadSerializer.Validate(asset);
            EngineBinaryHeader header = new EngineBinaryHeader(
                PayloadEndianness,
                CurrentVersion,
                FormatId,
                (ushort)RecordKind,
                (ushort)payloadSerializer.ValueKind);

            EngineBinaryHeaderSerializer.Write(stream, header);
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, PayloadEndianness);
            payloadSerializer.Write(writer, asset);
        }

        /// <summary>
        /// Deserializes an asset from the supplied stream using the editor asset format.
        /// </summary>
        /// <param name="stream">Source stream containing the asset payload.</param>
        /// <returns>Deserialized asset instance.</returns>
        public static Asset Deserialize(Stream stream) {
            EngineBinaryHeader header;
            using EngineBinaryReader reader = VersionedBinaryPayload.ReadHeaderWithDispatchedValueKind(
                stream,
                FormatId,
                (ushort)RecordKind,
                CurrentVersion,
                FormatIdMismatchSubject,
                RecordMismatchSubject,
                VersionMismatchSubject,
                VersionedBinaryVersionMismatchStyle.RequiredVersionSubjectFirst,
                RegenerateInstruction,
                out header);

            return ReadAssetPayload(reader, (EditorAssetBinaryValueKind)header.ValueKind);
        }

        /// <summary>
        /// Deserializes an asset from a stream after the standardized header has already been read.
        /// </summary>
        /// <param name="stream">Source stream positioned at the payload.</param>
        /// <param name="header">Previously decoded HELE header.</param>
        /// <returns>Deserialized asset instance.</returns>
        public static Asset Deserialize(Stream stream, EngineBinaryHeader header) {
            using EngineBinaryReader reader = VersionedBinaryPayload.ValidateHeaderWithDispatchedValueKind(
                stream,
                header,
                FormatId,
                (ushort)RecordKind,
                CurrentVersion,
                FormatIdMismatchSubject,
                RecordMismatchSubject,
                VersionMismatchSubject,
                VersionedBinaryVersionMismatchStyle.RequiredVersionSubjectFirst,
                RegenerateInstruction);

            return ReadAssetPayload(reader, (EditorAssetBinaryValueKind)header.ValueKind);
        }

        /// <summary>
        /// Resolves the registered payload serializer that owns a runtime asset instance.
        /// </summary>
        /// <param name="asset">Asset instance to classify.</param>
        /// <returns>Payload serializer that writes the asset.</returns>
        static IEditorAssetPayloadSerializer ResolvePayloadSerializer(Asset asset) {
            IEditorAssetPayloadSerializer payloadSerializer = EditorAssetPayloadSerializerRegistry.FindByAsset(asset);
            if (payloadSerializer == null) {
                throw new InvalidOperationException($"Asset type '{asset.GetType().Name}' is not supported by the editor binary serializer.");
            }

            return payloadSerializer;
        }

        /// <summary>
        /// Reads an asset payload using the supplied value kind.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <param name="valueKind">Format-specific value kind identifier.</param>
        /// <returns>Deserialized asset instance.</returns>
        static Asset ReadAssetPayload(EngineBinaryReader reader, EditorAssetBinaryValueKind valueKind) {
            IEditorAssetPayloadSerializer payloadSerializer = EditorAssetPayloadSerializerRegistry.FindByValueKind(valueKind);
            if (payloadSerializer == null) {
                throw new InvalidOperationException($"Unsupported asset value kind '{(ushort)valueKind}'.");
            }

            return payloadSerializer.Read(reader);
        }
    }
}

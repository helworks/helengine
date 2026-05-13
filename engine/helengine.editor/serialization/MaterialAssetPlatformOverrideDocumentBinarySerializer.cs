namespace helengine.editor {
    /// <summary>
    /// Serializes and deserializes per-platform material override documents stored in `*.<platform>.hasset` files.
    /// </summary>
    public static class MaterialAssetPlatformOverrideDocumentBinarySerializer {
        /// <summary>
        /// Record kind used for material override payloads.
        /// </summary>
        public const EditorBinaryRecordKind RecordKind = EditorBinaryRecordKind.AssetImportSettings;

        /// <summary>
        /// Serializer version for the material override payload layout.
        /// </summary>
        public const byte CurrentVersion = 1;

        /// <summary>
        /// Payload endianness used by the current material override format.
        /// </summary>
        static readonly EngineBinaryEndianness PayloadEndianness = EngineBinaryEndianness.LittleEndian;

        /// <summary>
        /// Serializes one platform material override document to the supplied stream.
        /// </summary>
        /// <param name="stream">Destination stream for the payload.</param>
        /// <param name="document">Platform material override document to serialize.</param>
        public static void Serialize(Stream stream, MaterialAssetPlatformOverrideDocument document) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (document == null) {
                throw new ArgumentNullException(nameof(document));
            } else if (string.IsNullOrWhiteSpace(document.PlatformId)) {
                throw new InvalidOperationException("Material platform override must specify a platform id.");
            } else if (document.Processor == null) {
                throw new InvalidOperationException("Material platform override must include processor settings.");
            } else if (document.Processor.FieldValues == null) {
                throw new InvalidOperationException("Material platform override must include field values.");
            }

            EngineBinaryHeader header = new EngineBinaryHeader(
                PayloadEndianness,
                CurrentVersion,
                EditorAssetBinarySerializer.FormatId,
                (ushort)RecordKind,
                (ushort)AssetImportSettingsBinaryValueKind.MaterialAssetPlatformOverrideDocument);
            EngineBinaryHeaderSerializer.Write(stream, header);

            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, PayloadEndianness);
            writer.WriteString(document.PlatformId);
            writer.WriteByte(document.Processor.HasSchemaIdOverride ? (byte)1 : (byte)0);
            writer.WriteString(document.Processor.SchemaId ?? string.Empty);
            writer.WriteInt32(document.Processor.FieldValues.Count);
            foreach (KeyValuePair<string, string> entry in document.Processor.FieldValues) {
                if (string.IsNullOrWhiteSpace(entry.Key)) {
                    throw new InvalidOperationException("Material platform override cannot contain a blank field id.");
                } else if (entry.Value == null) {
                    throw new InvalidOperationException($"Material platform override cannot contain a null field value for '{entry.Key}'.");
                }

                writer.WriteString(entry.Key);
                writer.WriteString(entry.Value);
            }
        }

        /// <summary>
        /// Deserializes one platform material override document from the supplied stream.
        /// </summary>
        /// <param name="stream">Source stream containing the payload.</param>
        /// <returns>Deserialized platform material override document.</returns>
        public static MaterialAssetPlatformOverrideDocument Deserialize(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            ValidateHeader(header);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, header.Endianness);

            MaterialAssetPlatformOverrideDocument document = new MaterialAssetPlatformOverrideDocument();
            document.PlatformId = reader.ReadString();
            if (string.IsNullOrWhiteSpace(document.PlatformId)) {
                throw new InvalidOperationException("Material platform override cannot contain a blank platform id.");
            }

            document.Processor.HasSchemaIdOverride = ReadBooleanByte(reader);
            document.Processor.SchemaId = reader.ReadString();

            int fieldValueCount = reader.ReadInt32();
            if (fieldValueCount < 0) {
                throw new InvalidOperationException("Material platform override field count cannot be negative.");
            }

            for (int index = 0; index < fieldValueCount; index++) {
                string fieldId = reader.ReadString();
                if (string.IsNullOrWhiteSpace(fieldId)) {
                    throw new InvalidOperationException("Material platform override cannot contain a blank field id.");
                }

                document.Processor.FieldValues.Add(fieldId, reader.ReadString());
            }

            return document;
        }

        /// <summary>
        /// Validates that the provided header matches the material override format.
        /// </summary>
        /// <param name="header">Header metadata to validate.</param>
        static void ValidateHeader(EngineBinaryHeader header) {
            if (header == null) {
                throw new ArgumentNullException(nameof(header));
            } else if (header.FormatId != EditorAssetBinarySerializer.FormatId) {
                throw new InvalidOperationException($"Unsupported material platform override format id '{header.FormatId}'.");
            } else if (header.RecordKind != (ushort)RecordKind) {
                throw new InvalidOperationException($"Unexpected material platform override record kind '{header.RecordKind}'.");
            } else if (header.ValueKind != (ushort)AssetImportSettingsBinaryValueKind.MaterialAssetPlatformOverrideDocument) {
                throw new InvalidOperationException($"Unexpected material platform override value kind '{header.ValueKind}'.");
            } else if (header.Version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported material platform override binary version '{header.Version}'.");
            }
        }

        /// <summary>
        /// Reads a boolean encoded as a single byte where zero means false and one means true.
        /// </summary>
        /// <param name="reader">Reader positioned at the encoded boolean value.</param>
        /// <returns>Decoded boolean value.</returns>
        static bool ReadBooleanByte(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            byte value = reader.ReadByte();
            if (value == 0) {
                return false;
            } else if (value == 1) {
                return true;
            }

            throw new InvalidOperationException($"Unsupported material platform override boolean value '{value}'.");
        }
    }
}

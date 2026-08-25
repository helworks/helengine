namespace helengine.editor {
    /// <summary>
    /// Serializes and deserializes shared material settings documents stored in base `*.hasset` files.
    /// </summary>
    public static class MaterialAssetCommonSettingsDocumentBinarySerializer {
        /// <summary>
        /// Record kind used for material settings payloads.
        /// </summary>
        public const EditorBinaryRecordKind RecordKind = EditorBinaryRecordKind.AssetImportSettings;

        /// <summary>
        /// Serializer version for the shared material settings payload layout.
        /// </summary>
        public const byte CurrentVersion = 3;

        /// <summary>
        /// Payload endianness used by the current shared material settings format.
        /// </summary>
        static readonly EngineBinaryEndianness PayloadEndianness = EngineBinaryEndianness.LittleEndian;

        /// <summary>
        /// Serializes one shared material settings document to the supplied stream.
        /// </summary>
        /// <param name="stream">Destination stream for the payload.</param>
        /// <param name="document">Shared material settings document to serialize.</param>
        public static void Serialize(Stream stream, MaterialAssetCommonSettingsDocument document) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (document == null) {
                throw new ArgumentNullException(nameof(document));
            } else if (document.Importer == null) {
                throw new InvalidOperationException("Material common settings must include importer settings.");
            } else if (document.Processor == null) {
                throw new InvalidOperationException("Material common settings must include processor settings.");
            } else if (document.Processor.FieldValues == null || document.Processor.AssetReferenceValues == null) {
                throw new InvalidOperationException("Material common settings must include field values.");
            }

            EngineBinaryHeader header = new EngineBinaryHeader(
                PayloadEndianness,
                CurrentVersion,
                EditorAssetBinarySerializer.FormatId,
                (ushort)RecordKind,
                (ushort)AssetImportSettingsBinaryValueKind.MaterialAssetCommonSettingsDocument);
            EngineBinaryHeaderSerializer.Write(stream, header);

            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, PayloadEndianness);
            writer.WriteString(document.AuthoringAssetId ?? string.Empty);
            writer.WriteInt32(document.FormerAuthoringAssetIds?.Count ?? 0);
            if (document.FormerAuthoringAssetIds != null) {
                for (int index = 0; index < document.FormerAuthoringAssetIds.Count; index++) {
                    writer.WriteString(document.FormerAuthoringAssetIds[index]);
                }
            }
            writer.WriteString(document.Importer.ImporterId ?? string.Empty);
            writer.WriteString(document.Importer.SourceChecksum ?? string.Empty);
            writer.WriteString(document.Importer.AssetId ?? string.Empty);
            writer.WriteString(document.Processor.SchemaId ?? string.Empty);
            writer.WriteInt32(document.Processor.FieldValues.Count);
            foreach (KeyValuePair<string, string> entry in document.Processor.FieldValues) {
                if (string.IsNullOrWhiteSpace(entry.Key)) {
                    throw new InvalidOperationException("Material common settings cannot contain a blank field id.");
                } else if (entry.Value == null) {
                    throw new InvalidOperationException($"Material common settings cannot contain a null field value for '{entry.Key}'.");
                }

                writer.WriteString(entry.Key);
                writer.WriteString(entry.Value);
            }
            WriteReferences(writer, document.Processor.AssetReferenceValues);
        }

        /// <summary>
        /// Deserializes one shared material settings document from the supplied stream.
        /// </summary>
        /// <param name="stream">Source stream containing the payload.</param>
        /// <returns>Deserialized shared material settings document.</returns>
        public static MaterialAssetCommonSettingsDocument Deserialize(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            ValidateHeader(header);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, header.Endianness);

            MaterialAssetCommonSettingsDocument document = new MaterialAssetCommonSettingsDocument();
            document.AuthoringAssetId = reader.ReadString();
            int formerAssetIdCount = reader.ReadInt32();
            if (formerAssetIdCount < 0) {
                throw new InvalidOperationException("Material common settings former asset id count cannot be negative.");
            }
            for (int index = 0; index < formerAssetIdCount; index++) {
                document.FormerAuthoringAssetIds.Add(reader.ReadString());
            }
            document.Importer.ImporterId = reader.ReadString();
            document.Importer.SourceChecksum = reader.ReadString();
            document.Importer.AssetId = reader.ReadString();
            document.Processor.SchemaId = reader.ReadString();

            int fieldValueCount = reader.ReadInt32();
            if (fieldValueCount < 0) {
                throw new InvalidOperationException("Material common settings field count cannot be negative.");
            }

            for (int index = 0; index < fieldValueCount; index++) {
                string fieldId = reader.ReadString();
                if (string.IsNullOrWhiteSpace(fieldId)) {
                    throw new InvalidOperationException("Material common settings cannot contain a blank field id.");
                }

                document.Processor.FieldValues.Add(fieldId, reader.ReadString());
            }
            ReadReferences(reader, document.Processor.AssetReferenceValues);

            return document;
        }

        /// <summary>
        /// Validates that the provided header matches the shared material settings format.
        /// </summary>
        /// <param name="header">Header metadata to validate.</param>
        static void ValidateHeader(EngineBinaryHeader header) {
            if (header == null) {
                throw new ArgumentNullException(nameof(header));
            } else if (header.FormatId != EditorAssetBinarySerializer.FormatId) {
                throw new InvalidOperationException($"Unsupported material common settings format id '{header.FormatId}'.");
            } else if (header.RecordKind != (ushort)RecordKind) {
                throw new InvalidOperationException($"Unexpected material common settings record kind '{header.RecordKind}'.");
            } else if (header.ValueKind != (ushort)AssetImportSettingsBinaryValueKind.MaterialAssetCommonSettingsDocument) {
                throw new InvalidOperationException($"Unexpected material common settings value kind '{header.ValueKind}'.");
            } else if (header.Version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported material common settings binary version '{header.Version}'.");
            }
        }

        /// <summary>Writes typed material references.</summary>
        static void WriteReferences(EngineBinaryWriter writer, Dictionary<string, SceneAssetReference> references) {
            writer.WriteInt32(references.Count);
            foreach (KeyValuePair<string, SceneAssetReference> entry in references) {
                writer.WriteString(entry.Key);
                writer.WriteInt32((int)entry.Value.SourceKind);
                writer.WriteString(entry.Value.RelativePath);
                writer.WriteString(entry.Value.ProviderId);
                writer.WriteString(entry.Value.AssetId);
                writer.WriteString(entry.Value.ContentHash);
            }
        }

        /// <summary>Reads typed material references.</summary>
        static void ReadReferences(EngineBinaryReader reader, Dictionary<string, SceneAssetReference> references) {
            int count = reader.ReadInt32();
            if (count < 0) {
                throw new InvalidOperationException("Material common settings reference count cannot be negative.");
            }
            for (int index = 0; index < count; index++) {
                string fieldId = reader.ReadString();
                references.Add(fieldId, global::helengine.SceneAssetReferenceFactory.Rehydrate(
                    (SceneAssetReferenceSourceKind)reader.ReadInt32(),
                    reader.ReadString(),
                    reader.ReadString(),
                    reader.ReadString(),
                    reader.ReadString()));
            }
        }
    }
}

namespace helengine.editor {
    /// <summary>
    /// Serializes material asset import settings using the editor binary header format.
    /// </summary>
    public static class MaterialAssetImportSettingsBinarySerializer {
        /// <summary>
        /// Record kind used for material asset import settings payloads.
        /// </summary>
        public const EditorBinaryRecordKind RecordKind = EditorBinaryRecordKind.AssetImportSettings;

        /// <summary>
        /// Serializer version for the current material asset import settings payload layout.
        /// </summary>
        public const byte CurrentVersion = 1;

        /// <summary>
        /// Payload endianness used by the current material asset import settings format.
        /// </summary>
        static readonly EngineBinaryEndianness PayloadEndianness = EngineBinaryEndianness.LittleEndian;

        /// <summary>
        /// Serializes material asset import settings to the supplied stream.
        /// </summary>
        /// <param name="stream">Destination stream for the payload.</param>
        /// <param name="settings">Settings instance to serialize.</param>
        public static void Serialize(Stream stream, MaterialAssetImportSettings settings) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (settings.Importer == null) {
                throw new InvalidOperationException("Material asset import settings must include importer settings.");
            } else if (settings.Processor == null || settings.Processor.Platforms == null) {
                throw new InvalidOperationException("Material asset import settings must include processor platform settings.");
            }

            EngineBinaryHeader header = new EngineBinaryHeader(
                PayloadEndianness,
                CurrentVersion,
                EditorAssetBinarySerializer.FormatId,
                (ushort)RecordKind,
                (ushort)AssetImportSettingsBinaryValueKind.MaterialAssetImportSettings);
            EngineBinaryHeaderSerializer.Write(stream, header);

            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, PayloadEndianness);
            writer.WriteString(settings.Importer.ImporterId);
            writer.WriteString(settings.Importer.SourceChecksum);
            writer.WriteString(settings.Importer.AssetId);
            writer.WriteInt32(settings.Processor.Platforms.Count);
            foreach (KeyValuePair<string, MaterialAssetProcessorSettings> entry in settings.Processor.Platforms) {
                if (string.IsNullOrWhiteSpace(entry.Key)) {
                    throw new InvalidOperationException("Material asset import settings cannot contain a blank processor platform id.");
                } else if (entry.Value == null) {
                    throw new InvalidOperationException($"Material asset import settings must include processor settings for platform '{entry.Key}'.");
                } else if (entry.Value.FieldValues == null) {
                    throw new InvalidOperationException($"Material asset import settings must include material field values for platform '{entry.Key}'.");
                }

                writer.WriteString(entry.Key);
                writer.WriteString(entry.Value.SchemaId ?? string.Empty);
                writer.WriteInt32(entry.Value.FieldValues.Count);
                foreach (KeyValuePair<string, string> fieldEntry in entry.Value.FieldValues) {
                    if (string.IsNullOrWhiteSpace(fieldEntry.Key)) {
                        throw new InvalidOperationException($"Material asset import settings cannot contain a blank material field id for platform '{entry.Key}'.");
                    } else if (fieldEntry.Value == null) {
                        throw new InvalidOperationException($"Material asset import settings cannot contain a null material field value for platform '{entry.Key}'.");
                    }

                    writer.WriteString(fieldEntry.Key);
                    writer.WriteString(fieldEntry.Value);
                }
            }
        }

        /// <summary>
        /// Deserializes material asset import settings from the supplied stream.
        /// </summary>
        /// <param name="stream">Source stream containing the payload.</param>
        /// <returns>Deserialized settings instance.</returns>
        public static MaterialAssetImportSettings Deserialize(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            ValidateHeader(header);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, header.Endianness);
            MaterialAssetImportSettings settings = new MaterialAssetImportSettings();
            settings.Importer.ImporterId = reader.ReadString();
            settings.Importer.SourceChecksum = reader.ReadString();
            settings.Importer.AssetId = reader.ReadString();

            int platformCount = reader.ReadInt32();
            if (platformCount < 0) {
                throw new InvalidOperationException("Material asset import settings platform count cannot be negative.");
            }

            for (int platformIndex = 0; platformIndex < platformCount; platformIndex++) {
                string platformId = reader.ReadString();
                if (string.IsNullOrWhiteSpace(platformId)) {
                    throw new InvalidOperationException("Material asset import settings cannot contain a blank processor platform id.");
                }

                MaterialAssetProcessorSettings platformSettings = new MaterialAssetProcessorSettings {
                    SchemaId = reader.ReadString()
                };
                int fieldValueCount = reader.ReadInt32();
                if (fieldValueCount < 0) {
                    throw new InvalidOperationException("Material asset import settings material field count cannot be negative.");
                }

                for (int fieldIndex = 0; fieldIndex < fieldValueCount; fieldIndex++) {
                    string fieldId = reader.ReadString();
                    if (string.IsNullOrWhiteSpace(fieldId)) {
                        throw new InvalidOperationException("Material asset import settings cannot contain a blank material field id.");
                    }

                    platformSettings.FieldValues.Add(fieldId, reader.ReadString());
                }

                settings.Processor.Platforms.Add(platformId, platformSettings);
            }

            return settings;
        }

        /// <summary>
        /// Validates that the provided header matches the material asset import settings format.
        /// </summary>
        /// <param name="header">Header metadata to validate.</param>
        static void ValidateHeader(EngineBinaryHeader header) {
            if (header == null) {
                throw new ArgumentNullException(nameof(header));
            } else if (header.FormatId != EditorAssetBinarySerializer.FormatId) {
                throw new InvalidOperationException($"Unsupported material asset import settings format id '{header.FormatId}'.");
            } else if (header.RecordKind != (ushort)RecordKind) {
                throw new InvalidOperationException($"Unexpected material asset import settings record kind '{header.RecordKind}'.");
            } else if (header.ValueKind != (ushort)AssetImportSettingsBinaryValueKind.MaterialAssetImportSettings) {
                throw new InvalidOperationException($"Unexpected material asset import settings value kind '{header.ValueKind}'.");
            } else if (header.Version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported material asset import settings binary version '{header.Version}'.");
            }
        }
    }
}

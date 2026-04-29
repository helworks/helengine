namespace helengine.editor {
    /// <summary>
    /// Serializes and deserializes asset import settings using the HELE binary header and editor payload layout.
    /// </summary>
    public static class AssetImportSettingsBinarySerializer {
        /// <summary>
        /// Record kind used for asset import settings payloads.
        /// </summary>
        public const EditorBinaryRecordKind RecordKind = EditorBinaryRecordKind.AssetImportSettings;

        /// <summary>
        /// Value kind used for asset import settings payloads.
        /// </summary>
        public const AssetImportSettingsBinaryValueKind ValueKind = AssetImportSettingsBinaryValueKind.AssetImportSettings;

        /// <summary>
        /// Serializer version for the current asset import settings payload layout.
        /// </summary>
        public const byte CurrentVersion = 2;

        /// <summary>
        /// Payload endianness used by the current asset import settings format.
        /// </summary>
        static readonly EngineBinaryEndianness PayloadEndianness = EngineBinaryEndianness.LittleEndian;

        /// <summary>
        /// Serializes asset import settings to the supplied stream.
        /// </summary>
        /// <param name="stream">Destination stream for the payload.</param>
        /// <param name="settings">Settings instance to serialize.</param>
        public static void Serialize(Stream stream, AssetImportSettings settings) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            EngineBinaryHeader header = new EngineBinaryHeader(
                PayloadEndianness,
                CurrentVersion,
                EditorAssetBinarySerializer.FormatId,
                (ushort)RecordKind,
                (ushort)ValueKind);

            EngineBinaryHeaderSerializer.Write(stream, header);
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, PayloadEndianness);
            if (settings.Importer == null) {
                throw new InvalidOperationException("Asset import settings must include importer settings.");
            } else if (settings.Processor == null) {
                throw new InvalidOperationException("Asset import settings must include processor settings.");
            } else if (settings.Processor.Platforms == null) {
                throw new InvalidOperationException("Asset import settings must include a processor platform map.");
            }

            writer.WriteString(settings.Importer.ImporterId);
            writer.WriteString(settings.Importer.SourceChecksum);
            writer.WriteString(settings.Importer.AssetId);
            writer.WriteInt32(settings.Processor.Platforms.Count);
            foreach (KeyValuePair<string, AssetPlatformProcessorSettings> entry in settings.Processor.Platforms) {
                if (string.IsNullOrWhiteSpace(entry.Key)) {
                    throw new InvalidOperationException("Asset import settings cannot contain a blank processor platform id.");
                } else if (entry.Value == null) {
                    throw new InvalidOperationException($"Asset import settings must include processor settings for platform '{entry.Key}'.");
                } else if (entry.Value.Model == null) {
                    throw new InvalidOperationException($"Asset import settings must include model processor settings for platform '{entry.Key}'.");
                }

                writer.WriteString(entry.Key);
                writer.WriteByte(entry.Value.Model.FlipWinding ? (byte)1 : (byte)0);
            }
        }

        /// <summary>
        /// Deserializes asset import settings from the supplied stream.
        /// </summary>
        /// <param name="stream">Source stream containing the payload.</param>
        /// <returns>Deserialized settings instance.</returns>
        public static AssetImportSettings Deserialize(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            ValidateHeader(header);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, header.Endianness);
            AssetImportSettings settings = new AssetImportSettings();
            settings.Importer.ImporterId = reader.ReadString();
            settings.Importer.SourceChecksum = reader.ReadString();
            settings.Importer.AssetId = reader.ReadString();

            int platformCount = reader.ReadInt32();
            if (platformCount < 0) {
                throw new InvalidOperationException("Asset import settings platform count cannot be negative.");
            }

            for (int i = 0; i < platformCount; i++) {
                string platformId = reader.ReadString();
                if (string.IsNullOrWhiteSpace(platformId)) {
                    throw new InvalidOperationException("Asset import settings cannot contain a blank processor platform id.");
                }

                AssetPlatformProcessorSettings platformSettings = new AssetPlatformProcessorSettings();
                platformSettings.Model.FlipWinding = ReadBooleanByte(reader);
                settings.Processor.Platforms.Add(platformId, platformSettings);
            }

            return settings;
        }

        /// <summary>
        /// Validates that the provided header matches the asset import settings format.
        /// </summary>
        /// <param name="header">Header metadata to validate.</param>
        static void ValidateHeader(EngineBinaryHeader header) {
            if (header == null) {
                throw new ArgumentNullException(nameof(header));
            } else if (header.FormatId != EditorAssetBinarySerializer.FormatId) {
                throw new InvalidOperationException($"Unsupported asset import settings format id '{header.FormatId}'.");
            } else if (header.RecordKind != (ushort)RecordKind) {
                throw new InvalidOperationException($"Unexpected asset import settings record kind '{header.RecordKind}'.");
            } else if (header.ValueKind != (ushort)ValueKind) {
                throw new InvalidOperationException($"Unexpected asset import settings value kind '{header.ValueKind}'.");
            } else if (header.Version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported asset import settings binary version '{header.Version}'.");
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

            throw new InvalidOperationException($"Unsupported asset import settings boolean value '{value}'.");
        }
    }
}

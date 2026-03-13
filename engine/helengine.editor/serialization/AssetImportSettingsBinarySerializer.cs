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
        public const byte CurrentVersion = 1;

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
            writer.WriteString(settings.ImporterId);
            writer.WriteString(settings.SourceChecksum);
            writer.WriteString(settings.AssetId);
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
            return new AssetImportSettings {
                ImporterId = reader.ReadString(),
                SourceChecksum = reader.ReadString(),
                AssetId = reader.ReadString()
            };
        }

        /// <summary>
        /// Attempts to deserialize asset import settings without throwing for legacy non-HELE payloads.
        /// </summary>
        /// <param name="stream">Source stream containing the payload.</param>
        /// <param name="settings">Deserialized settings instance when the payload matches the current format.</param>
        /// <returns>True when settings were deserialized successfully.</returns>
        public static bool TryDeserialize(Stream stream, out AssetImportSettings settings) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            settings = null;
            EngineBinaryHeader header;
            if (!EngineBinaryHeaderSerializer.TryRead(stream, out header)) {
                return false;
            }

            if (!IsExpectedHeader(header)) {
                return false;
            }

            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, header.Endianness);
            settings = new AssetImportSettings {
                ImporterId = reader.ReadString(),
                SourceChecksum = reader.ReadString(),
                AssetId = reader.ReadString()
            };
            return true;
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
        /// Determines whether the header matches the current asset import settings format.
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

namespace helengine.editor {
    /// <summary>
    /// Serializes texture asset import settings using the editor binary header format.
    /// </summary>
    public static class TextureAssetImportSettingsBinarySerializer {
        /// <summary>
        /// Record kind used for texture asset import settings payloads.
        /// </summary>
        public const EditorBinaryRecordKind RecordKind = EditorBinaryRecordKind.AssetImportSettings;

        /// <summary>
        /// Serializer version for the current texture asset import settings payload layout.
        /// </summary>
        public const byte CurrentVersion = 5;

        /// <summary>
        /// Payload endianness used by the current texture asset import settings format.
        /// </summary>
        static readonly EngineBinaryEndianness PayloadEndianness = EngineBinaryEndianness.LittleEndian;

        /// <summary>
        /// Serializes texture asset import settings to the supplied stream.
        /// </summary>
        /// <param name="stream">Destination stream for the payload.</param>
        /// <param name="settings">Settings instance to serialize.</param>
        public static void Serialize(Stream stream, TextureAssetImportSettings settings) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (settings.Importer == null) {
                throw new InvalidOperationException("Texture asset import settings must include importer settings.");
            } else if (settings.Processor == null || settings.Processor.Platforms == null) {
                throw new InvalidOperationException("Texture asset import settings must include processor platform settings.");
            }

            EngineBinaryHeader header = new EngineBinaryHeader(
                PayloadEndianness,
                CurrentVersion,
                EditorAssetBinarySerializer.FormatId,
                (ushort)RecordKind,
                (ushort)AssetImportSettingsBinaryValueKind.TextureAssetImportSettings);
            EngineBinaryHeaderSerializer.Write(stream, header);

            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, PayloadEndianness);
            writer.WriteString(settings.Importer.ImporterId);
            writer.WriteString(settings.Importer.SourceChecksum);
            writer.WriteString(settings.Importer.AssetId);
            writer.WriteInt32(settings.Processor.Platforms.Count);
            foreach (KeyValuePair<string, TextureAssetProcessorSettings> entry in settings.Processor.Platforms) {
                if (string.IsNullOrWhiteSpace(entry.Key)) {
                    throw new InvalidOperationException("Texture asset import settings cannot contain a blank processor platform id.");
                } else if (entry.Value == null) {
                    throw new InvalidOperationException($"Texture asset import settings must include processor settings for platform '{entry.Key}'.");
                } else if (entry.Value.MaxResolution < 0) {
                    throw new InvalidOperationException($"Texture asset import settings cannot contain a negative texture max resolution for platform '{entry.Key}'.");
                } else if (string.IsNullOrWhiteSpace(entry.Value.ColorFormatId)) {
                    throw new InvalidOperationException($"Texture asset import settings cannot contain a blank texture color format id for platform '{entry.Key}'.");
                } else if (!IsSupportedAlphaPrecision(entry.Value.AlphaPrecision)) {
                    throw new InvalidOperationException($"Texture asset import settings cannot contain unsupported texture alpha precision '{entry.Value.AlphaPrecision}' for platform '{entry.Key}'.");
                }

                writer.WriteString(entry.Key);
                writer.WriteInt32(entry.Value.MaxResolution);
                writer.WriteString(entry.Value.ColorFormatId);
                writer.WriteByte((byte)entry.Value.AlphaPrecision);
                writer.WriteString(entry.Value.IndexingMethodId ?? string.Empty);
            }
        }

        /// <summary>
        /// Deserializes texture asset import settings from the supplied stream.
        /// </summary>
        /// <param name="stream">Source stream containing the payload.</param>
        /// <returns>Deserialized settings instance.</returns>
        public static TextureAssetImportSettings Deserialize(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            ValidateHeader(header);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, header.Endianness);
            if (header.ValueKind != (ushort)AssetImportSettingsBinaryValueKind.TextureAssetImportSettings) {
                throw new InvalidOperationException($"Unexpected texture asset import settings value kind '{header.ValueKind}'.");
            } else if (header.Version < 1 || header.Version > CurrentVersion) {
                throw new InvalidOperationException($"Unsupported texture asset import settings binary version '{header.Version}'.");
            }

            TextureAssetImportSettings settings = new TextureAssetImportSettings();
            settings.Importer.ImporterId = reader.ReadString();
            settings.Importer.SourceChecksum = reader.ReadString();
            settings.Importer.AssetId = reader.ReadString();

            int platformCount = reader.ReadInt32();
            if (platformCount < 0) {
                throw new InvalidOperationException("Texture asset import settings platform count cannot be negative.");
            }

            for (int index = 0; index < platformCount; index++) {
                string platformId = reader.ReadString();
                if (string.IsNullOrWhiteSpace(platformId)) {
                    throw new InvalidOperationException("Texture asset import settings cannot contain a blank processor platform id.");
                }

                TextureAssetProcessorSettings platformSettings = new TextureAssetProcessorSettings {
                    MaxResolution = reader.ReadInt32()
                };
                if (platformSettings.MaxResolution < 0) {
                    throw new InvalidOperationException($"Texture asset import settings cannot contain a negative texture max resolution for platform '{platformId}'.");
                }
                platformSettings.ColorFormatId = header.Version >= CurrentVersion
                    ? reader.ReadString()
                    : ReadLegacyTextureAssetColorFormat(reader).ToString();
                platformSettings.AlphaPrecision = header.Version >= 3
                    ? ReadTextureAssetAlphaPrecision(reader)
                    : TextureAssetAlphaPrecision.A8;
                platformSettings.IndexingMethodId = header.Version >= CurrentVersion
                    ? reader.ReadString()
                    : string.Empty;

                settings.Processor.Platforms.Add(platformId, platformSettings);
            }

            return settings;
        }

        /// <summary>
        /// Validates that the provided header matches the texture asset import settings format.
        /// </summary>
        /// <param name="header">Header metadata to validate.</param>
        static void ValidateHeader(EngineBinaryHeader header) {
            if (header == null) {
                throw new ArgumentNullException(nameof(header));
            } else if (header.FormatId != EditorAssetBinarySerializer.FormatId) {
                throw new InvalidOperationException($"Unsupported texture asset import settings format id '{header.FormatId}'.");
            } else if (header.RecordKind != (ushort)RecordKind) {
                throw new InvalidOperationException($"Unexpected texture asset import settings record kind '{header.RecordKind}'.");
            }
        }

        /// <summary>
        /// Reads one serialized texture color-format value.
        /// </summary>
        /// <param name="reader">Reader positioned at the texture format byte.</param>
        /// <returns>Decoded texture color format.</returns>
        static TextureAssetColorFormat ReadLegacyTextureAssetColorFormat(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            byte serializedValue = reader.ReadByte();
            if (serializedValue == (byte)TextureAssetColorFormat.Rgba32) {
                return TextureAssetColorFormat.Rgba32;
            } else if (serializedValue == (byte)TextureAssetColorFormat.Rgba4444) {
                return TextureAssetColorFormat.Rgba4444;
            } else if (serializedValue == (byte)TextureAssetColorFormat.Indexed4) {
                return TextureAssetColorFormat.Indexed4;
            } else if (serializedValue == (byte)TextureAssetColorFormat.Indexed8) {
                return TextureAssetColorFormat.Indexed8;
            }

            throw new InvalidOperationException($"Unsupported texture color format '{serializedValue}'.");
        }

        /// <summary>
        /// Reads one serialized texture alpha-precision value.
        /// </summary>
        /// <param name="reader">Reader positioned at the texture alpha-precision byte.</param>
        /// <returns>Decoded texture alpha precision.</returns>
        static TextureAssetAlphaPrecision ReadTextureAssetAlphaPrecision(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            byte serializedValue = reader.ReadByte();
            if (serializedValue == (byte)TextureAssetAlphaPrecision.Opaque) {
                return TextureAssetAlphaPrecision.Opaque;
            } else if (serializedValue == (byte)TextureAssetAlphaPrecision.Binary) {
                return TextureAssetAlphaPrecision.Binary;
            } else if (serializedValue == (byte)TextureAssetAlphaPrecision.A4) {
                return TextureAssetAlphaPrecision.A4;
            } else if (serializedValue == (byte)TextureAssetAlphaPrecision.A8) {
                return TextureAssetAlphaPrecision.A8;
            }

            throw new InvalidOperationException($"Unsupported texture alpha precision '{serializedValue}'.");
        }

        /// <summary>
        /// Determines whether one texture alpha precision can be serialized by this settings document.
        /// </summary>
        /// <param name="alphaPrecision">Texture alpha precision to validate.</param>
        /// <returns>True when the alpha precision is supported.</returns>
        static bool IsSupportedAlphaPrecision(TextureAssetAlphaPrecision alphaPrecision) {
            return alphaPrecision == TextureAssetAlphaPrecision.Opaque
                || alphaPrecision == TextureAssetAlphaPrecision.Binary
                || alphaPrecision == TextureAssetAlphaPrecision.A4
                || alphaPrecision == TextureAssetAlphaPrecision.A8;
        }
    }
}

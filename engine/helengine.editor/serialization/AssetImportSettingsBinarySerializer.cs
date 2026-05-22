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
        public const byte CurrentVersion = 7;

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
                } else if (entry.Value.Texture == null) {
                    throw new InvalidOperationException($"Asset import settings must include texture processor settings for platform '{entry.Key}'.");
                } else if (entry.Value.Texture.MaxResolution < 0) {
                    throw new InvalidOperationException($"Asset import settings cannot contain a negative texture max resolution for platform '{entry.Key}'.");
                } else if (string.IsNullOrWhiteSpace(entry.Value.Texture.ColorFormatId)) {
                    throw new InvalidOperationException($"Asset import settings cannot contain a blank texture color format id for platform '{entry.Key}'.");
                } else if (!IsSupportedAlphaPrecision(entry.Value.Texture.AlphaPrecision)) {
                    throw new InvalidOperationException($"Asset import settings cannot contain unsupported texture alpha precision '{entry.Value.Texture.AlphaPrecision}' for platform '{entry.Key}'.");
                } else if (entry.Value.Model == null) {
                    throw new InvalidOperationException($"Asset import settings must include model processor settings for platform '{entry.Key}'.");
                } else if (entry.Value.Material == null) {
                    throw new InvalidOperationException($"Asset import settings must include material processor settings for platform '{entry.Key}'.");
                } else if (entry.Value.Material.FieldValues == null) {
                    throw new InvalidOperationException($"Asset import settings must include material field values for platform '{entry.Key}'.");
                }

                writer.WriteString(entry.Key);
                writer.WriteByte(entry.Value.Model.FlipWinding ? (byte)1 : (byte)0);
                writer.WriteInt32(entry.Value.Texture.MaxResolution);
                writer.WriteString(entry.Value.Texture.ColorFormatId);
                writer.WriteByte((byte)entry.Value.Texture.AlphaPrecision);
                writer.WriteString(entry.Value.Material.SchemaId ?? string.Empty);
                writer.WriteInt32(entry.Value.Material.FieldValues.Count);
                foreach (KeyValuePair<string, string> fieldEntry in entry.Value.Material.FieldValues) {
                    if (string.IsNullOrWhiteSpace(fieldEntry.Key)) {
                        throw new InvalidOperationException($"Asset import settings cannot contain a blank material field id for platform '{entry.Key}'.");
                    } else if (fieldEntry.Value == null) {
                        throw new InvalidOperationException($"Asset import settings cannot contain a null material field value for platform '{entry.Key}'.");
                    }

                    writer.WriteString(fieldEntry.Key);
                    writer.WriteString(fieldEntry.Value);
                }
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
                platformSettings.Texture.MaxResolution = reader.ReadInt32();
                if (platformSettings.Texture.MaxResolution < 0) {
                    throw new InvalidOperationException($"Asset import settings cannot contain a negative texture max resolution for platform '{platformId}'.");
                }
                platformSettings.Texture.ColorFormatId = header.Version >= CurrentVersion
                    ? reader.ReadString()
                    : (header.Version >= 5
                        ? ReadLegacyTextureAssetColorFormat(reader).ToString()
                        : TextureAssetColorFormat.Rgba32.ToString());
                platformSettings.Texture.AlphaPrecision = header.Version >= 6
                    ? ReadTextureAssetAlphaPrecision(reader)
                    : TextureAssetAlphaPrecision.A8;
                platformSettings.Material.SchemaId = reader.ReadString();

                int fieldValueCount = reader.ReadInt32();
                if (fieldValueCount < 0) {
                    throw new InvalidOperationException("Asset import settings material field count cannot be negative.");
                }

                for (int fieldIndex = 0; fieldIndex < fieldValueCount; fieldIndex++) {
                    string fieldId = reader.ReadString();
                    if (string.IsNullOrWhiteSpace(fieldId)) {
                        throw new InvalidOperationException("Asset import settings cannot contain a blank material field id.");
                    }

                    platformSettings.Material.FieldValues.Add(fieldId, reader.ReadString());
                }
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
            } else if (header.Version < 4 || header.Version > CurrentVersion) {
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

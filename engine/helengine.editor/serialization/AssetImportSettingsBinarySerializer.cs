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
        public const byte CurrentVersion = 11;

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
                } else if (entry.Value.Sections == null) {
                    throw new InvalidOperationException($"Asset import settings must include registered processor settings sections for platform '{entry.Key}'.");
                }

                writer.WriteString(entry.Key);
                SerializePlatformSettings(writer, entry.Value, $"platform '{entry.Key}'");
                writer.WriteInt32(entry.Value.Environments?.Count ?? 0);
                if (entry.Value.Environments != null) {
                    foreach (KeyValuePair<string, AssetPlatformProcessorSettings> environmentEntry in entry.Value.Environments) {
                        if (string.IsNullOrWhiteSpace(environmentEntry.Key) || environmentEntry.Value == null) {
                            throw new InvalidOperationException($"Asset import settings cannot contain an invalid environment override for platform '{entry.Key}'.");
                        }
                        writer.WriteString(environmentEntry.Key);
                        SerializePlatformSettings(writer, environmentEntry.Value, $"environment '{environmentEntry.Key}' on platform '{entry.Key}'");
                    }
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
                DeserializePlatformSettings(reader, platformSettings, header.Version, $"platform '{platformId}'");
                if (header.Version >= 11) {
                    int environmentCount = reader.ReadInt32();
                    if (environmentCount < 0) {
                        throw new InvalidOperationException("Asset import settings environment count cannot be negative.");
                    }
                    for (int environmentIndex = 0; environmentIndex < environmentCount; environmentIndex++) {
                        string environmentId = reader.ReadString();
                        if (string.IsNullOrWhiteSpace(environmentId) || platformSettings.Environments.ContainsKey(environmentId)) {
                            throw new InvalidOperationException($"Asset import settings cannot contain duplicate or blank environment id for platform '{platformId}'.");
                        }
                        AssetPlatformProcessorSettings environmentSettings = new AssetPlatformProcessorSettings();
                        DeserializePlatformSettings(reader, environmentSettings, header.Version, $"environment '{environmentId}' on platform '{platformId}'");
                        platformSettings.Environments.Add(environmentId, environmentSettings);
                    }
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
            } else if (header.Version < 9 || header.Version > CurrentVersion) {
                throw new InvalidOperationException($"Unsupported asset import settings binary version '{header.Version}'.");
            }
        }

        static void SerializePlatformSettings(EngineBinaryWriter writer, AssetPlatformProcessorSettings settings, string ownerLabel) {
            if (settings == null || settings.Sections == null) {
                throw new InvalidOperationException($"Asset import settings must include registered processor settings sections for {ownerLabel}.");
            }

            writer.WriteInt32(settings.Sections.Count);
            foreach (KeyValuePair<string, AssetPlatformSettingsSection> sectionEntry in settings.Sections) {
                if (string.IsNullOrWhiteSpace(sectionEntry.Key) || sectionEntry.Value == null) {
                    throw new InvalidOperationException($"Asset import settings contains an invalid processor section for {ownerLabel}.");
                }
                writer.WriteString(sectionEntry.Key);
                AssetPlatformSettingsSectionRegistry.Shared.SerializeSection(writer, sectionEntry.Key, sectionEntry.Value.Settings);
            }
        }

        static void DeserializePlatformSettings(EngineBinaryReader reader, AssetPlatformProcessorSettings settings, byte version, string ownerLabel) {
            int sectionCount = reader.ReadInt32();
            if (sectionCount < 0) {
                throw new InvalidOperationException($"Asset import settings section count cannot be negative for {ownerLabel}.");
            }
            for (int sectionIndex = 0; sectionIndex < sectionCount; sectionIndex++) {
                string sectionId = reader.ReadString();
                if (string.IsNullOrWhiteSpace(sectionId) || settings.Sections.ContainsKey(sectionId)) {
                    throw new InvalidOperationException($"Asset import settings contains a duplicate or blank processor section for {ownerLabel}.");
                }
                object sectionSettings = AssetPlatformSettingsSectionRegistry.Shared.DeserializeSection(reader, sectionId, version);
                settings.Sections.Add(sectionId, new AssetPlatformSettingsSection(sectionId, sectionSettings));
            }
        }
    }
}

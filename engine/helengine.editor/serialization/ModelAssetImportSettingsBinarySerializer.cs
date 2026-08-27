namespace helengine.editor {
    /// <summary>
    /// Serializes model asset import settings using the editor binary header format.
    /// </summary>
    public static class ModelAssetImportSettingsBinarySerializer {
        /// <summary>
        /// Record kind used for model asset import settings payloads.
        /// </summary>
        public const EditorBinaryRecordKind RecordKind = EditorBinaryRecordKind.AssetImportSettings;

        /// <summary>
        /// Serializer version for the current model asset import settings payload layout.
        /// </summary>
        public const byte CurrentVersion = 3;

        /// <summary>
        /// Payload endianness used by the current model asset import settings format.
        /// </summary>
        static readonly EngineBinaryEndianness PayloadEndianness = EngineBinaryEndianness.LittleEndian;

        /// <summary>
        /// Serializes model asset import settings to the supplied stream.
        /// </summary>
        /// <param name="stream">Destination stream for the payload.</param>
        /// <param name="settings">Settings instance to serialize.</param>
        public static void Serialize(Stream stream, ModelAssetImportSettings settings) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (settings.Importer == null) {
                throw new InvalidOperationException("Model asset import settings must include importer settings.");
            } else if (settings.Processor == null || settings.Processor.Platforms == null) {
                throw new InvalidOperationException("Model asset import settings must include processor platform settings.");
            }

            EngineBinaryHeader header = new EngineBinaryHeader(
                PayloadEndianness,
                CurrentVersion,
                EditorAssetBinarySerializer.FormatId,
                (ushort)RecordKind,
                (ushort)AssetImportSettingsBinaryValueKind.ModelAssetImportSettings);
            EngineBinaryHeaderSerializer.Write(stream, header);

            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, PayloadEndianness);
            writer.WriteString(settings.Importer.ImporterId);
            writer.WriteString(settings.Importer.SourceChecksum);
            writer.WriteString(settings.Importer.AssetId);
            writer.WriteInt32(settings.Processor.Platforms.Count);
            foreach (KeyValuePair<string, ModelAssetProcessorSettings> entry in settings.Processor.Platforms.OrderBy(entry => entry.Key, StringComparer.Ordinal)) {
                if (string.IsNullOrWhiteSpace(entry.Key)) {
                    throw new InvalidOperationException("Model asset import settings cannot contain a blank processor platform id.");
                } else if (entry.Value == null) {
                    throw new InvalidOperationException($"Model asset import settings must include processor settings for platform '{entry.Key}'.");
                }

                writer.WriteString(entry.Key);
                WriteModelSettings(writer, entry.Value);
            }
            writer.WriteInt32(settings.Processor.Environments?.Count ?? 0);
            if (settings.Processor.Environments != null) {
                foreach (KeyValuePair<string, Dictionary<string, ModelAssetProcessorSettings>> platformEnvironment in settings.Processor.Environments.OrderBy(entry => entry.Key, StringComparer.Ordinal)) {
                    writer.WriteString(platformEnvironment.Key);
                    writer.WriteInt32(platformEnvironment.Value?.Count ?? 0);
                    if (platformEnvironment.Value == null) continue;
                    foreach (KeyValuePair<string, ModelAssetProcessorSettings> environmentEntry in platformEnvironment.Value.OrderBy(entry => entry.Key, StringComparer.Ordinal)) {
                        writer.WriteString(environmentEntry.Key);
                        WriteModelSettings(writer, environmentEntry.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Deserializes model asset import settings from the supplied stream.
        /// </summary>
        /// <param name="stream">Source stream containing the payload.</param>
        /// <returns>Deserialized settings instance.</returns>
        public static ModelAssetImportSettings Deserialize(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            ValidateHeader(header);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, header.Endianness);
            ModelAssetImportSettings settings = new ModelAssetImportSettings();
            settings.Importer.ImporterId = reader.ReadString();
            settings.Importer.SourceChecksum = reader.ReadString();
            settings.Importer.AssetId = reader.ReadString();

            int platformCount = reader.ReadInt32();
            if (platformCount < 0) {
                throw new InvalidOperationException("Model asset import settings platform count cannot be negative.");
            }

            for (int index = 0; index < platformCount; index++) {
                string platformId = reader.ReadString();
                if (string.IsNullOrWhiteSpace(platformId)) {
                    throw new InvalidOperationException("Model asset import settings cannot contain a blank processor platform id.");
                }

                ModelAssetProcessorSettings platformSettings = ReadModelSettings(reader);

                settings.Processor.Platforms.Add(platformId, platformSettings);
            }

            int environmentPlatformCount = reader.ReadInt32();
            if (environmentPlatformCount < 0) throw new InvalidOperationException("Model asset import settings environment platform count cannot be negative.");
            for (int index = 0; index < environmentPlatformCount; index++) {
                string platformId = reader.ReadString();
                int environmentCount = reader.ReadInt32();
                if (environmentCount < 0) throw new InvalidOperationException("Model asset import settings environment count cannot be negative.");
                Dictionary<string, ModelAssetProcessorSettings> environments = new Dictionary<string, ModelAssetProcessorSettings>(StringComparer.OrdinalIgnoreCase);
                for (int environmentIndex = 0; environmentIndex < environmentCount; environmentIndex++) {
                    string environmentId = reader.ReadString();
                    environments.Add(environmentId, ReadModelSettings(reader));
                }
                settings.Processor.Environments.Add(platformId, environments);
            }

            return settings;
        }

        /// <summary>
        /// Validates that the provided header matches the model asset import settings format.
        /// </summary>
        /// <param name="header">Header metadata to validate.</param>
        static void ValidateHeader(EngineBinaryHeader header) {
            if (header == null) {
                throw new ArgumentNullException(nameof(header));
            } else if (header.FormatId != EditorAssetBinarySerializer.FormatId) {
                throw new InvalidOperationException($"Unsupported model asset import settings format id '{header.FormatId}'.");
            } else if (header.RecordKind != (ushort)RecordKind) {
                throw new InvalidOperationException($"Unexpected model asset import settings record kind '{header.RecordKind}'.");
            } else if (header.ValueKind != (ushort)AssetImportSettingsBinaryValueKind.ModelAssetImportSettings) {
                throw new InvalidOperationException($"Unexpected model asset import settings value kind '{header.ValueKind}'.");
            } else if (header.Version != CurrentVersion) {
                throw new InvalidOperationException(
                    $"Unsupported model asset import settings binary version received '{header.Version}'; current version is '{CurrentVersion}'. Regenerate the model import settings sidecar.");
            }
        }

        /// <summary>
        /// Validates one persisted tessellation edge-length setting.
        /// </summary>
        /// <param name="tessellationMaxEdgeLength">Configured maximum edge length.</param>
        static void ValidateTessellationMaxEdgeLength(double tessellationMaxEdgeLength) {
            if (double.IsNaN(tessellationMaxEdgeLength) || double.IsInfinity(tessellationMaxEdgeLength) || tessellationMaxEdgeLength <= 0d) {
                throw new InvalidOperationException("Model tessellation maximum edge length must be finite and greater than zero.");
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

            throw new InvalidOperationException($"Unsupported model asset import settings boolean value '{value}'.");
        }

        static void WriteModelSettings(EngineBinaryWriter writer, ModelAssetProcessorSettings settings) {
            if (settings == null) throw new InvalidOperationException("Model processor settings cannot be null.");
            writer.WriteByte(settings.FlipWinding ? (byte)1 : (byte)0);
            writer.WriteByte(settings.Tessellate ? (byte)1 : (byte)0);
            ValidateTessellationMaxEdgeLength(settings.TessellationMaxEdgeLength);
            writer.WriteDouble(settings.TessellationMaxEdgeLength);
        }

        static ModelAssetProcessorSettings ReadModelSettings(EngineBinaryReader reader) {
            ModelAssetProcessorSettings settings = new ModelAssetProcessorSettings {
                FlipWinding = ReadBooleanByte(reader),
                Tessellate = ReadBooleanByte(reader),
                TessellationMaxEdgeLength = reader.ReadDouble()
            };
            ValidateTessellationMaxEdgeLength(settings.TessellationMaxEdgeLength);
            return settings;
        }
    }
}

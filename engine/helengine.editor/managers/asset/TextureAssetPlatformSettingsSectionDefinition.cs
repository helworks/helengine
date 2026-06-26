namespace helengine.editor {
    /// <summary>
    /// Defines serialization and comparison behavior for the registered texture section.
    /// </summary>
    public sealed class TextureAssetPlatformSettingsSectionDefinition : IAssetPlatformSettingsSectionDefinition {
        /// <summary>
        /// Gets the stable section identifier.
        /// </summary>
        public const string SectionIdValue = "texture";

        /// <summary>
        /// Gets the registered section identifier.
        /// </summary>
        public string SectionId => SectionIdValue;

        /// <summary>
        /// Gets the payload type owned by the section.
        /// </summary>
        public Type SettingsType => typeof(TextureAssetProcessorSettings);

        /// <summary>
        /// Creates one default texture settings payload.
        /// </summary>
        /// <returns>Default texture settings payload.</returns>
        public object CreateDefaultSettings() {
            return new TextureAssetProcessorSettings {
                MaxResolution = 0,
                ColorFormatId = TextureAssetColorFormat.Rgba32.ToString(),
                AlphaPrecision = TextureAssetAlphaPrecision.A8,
                IndexingMethodId = string.Empty
            };
        }

        /// <summary>
        /// Creates one deep clone of the supplied texture settings payload.
        /// </summary>
        /// <param name="settings">Payload instance to clone.</param>
        /// <returns>Cloned texture settings payload.</returns>
        public object CloneSettings(object settings) {
            TextureAssetProcessorSettings source = RequireSettings(settings);
            return new TextureAssetProcessorSettings {
                MaxResolution = source.MaxResolution,
                ColorFormatId = source.ColorFormatId,
                AlphaPrecision = source.AlphaPrecision,
                IndexingMethodId = source.IndexingMethodId
            };
        }

        /// <summary>
        /// Returns whether two texture settings payloads carry the same values.
        /// </summary>
        /// <param name="left">First payload instance.</param>
        /// <param name="right">Second payload instance.</param>
        /// <returns>True when both texture payloads match.</returns>
        public bool SettingsEqual(object left, object right) {
            TextureAssetProcessorSettings leftSettings = RequireSettings(left);
            TextureAssetProcessorSettings rightSettings = RequireSettings(right);
            return leftSettings.MaxResolution == rightSettings.MaxResolution
                && string.Equals(leftSettings.ColorFormatId, rightSettings.ColorFormatId, StringComparison.Ordinal)
                && leftSettings.AlphaPrecision == rightSettings.AlphaPrecision
                && string.Equals(leftSettings.IndexingMethodId ?? string.Empty, rightSettings.IndexingMethodId ?? string.Empty, StringComparison.Ordinal);
        }

        /// <summary>
        /// Serializes one texture settings payload.
        /// </summary>
        /// <param name="writer">Writer that owns the destination stream.</param>
        /// <param name="settings">Payload instance to serialize.</param>
        public void Serialize(EngineBinaryWriter writer, object settings) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            TextureAssetProcessorSettings textureSettings = RequireSettings(settings);
            if (textureSettings.MaxResolution < 0) {
                throw new InvalidOperationException("Texture max resolution cannot be negative.");
            } else if (string.IsNullOrWhiteSpace(textureSettings.ColorFormatId)) {
                throw new InvalidOperationException("Texture color format id must be provided.");
            }

            writer.WriteInt32(textureSettings.MaxResolution);
            writer.WriteString(textureSettings.ColorFormatId);
            writer.WriteByte((byte)textureSettings.AlphaPrecision);
            writer.WriteString(textureSettings.IndexingMethodId ?? string.Empty);
        }

        /// <summary>
        /// Deserializes one texture settings payload.
        /// </summary>
        /// <param name="reader">Reader positioned at the payload body.</param>
        /// <returns>Deserialized texture settings payload.</returns>
        public object Deserialize(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            TextureAssetProcessorSettings textureSettings = new TextureAssetProcessorSettings {
                MaxResolution = reader.ReadInt32(),
                ColorFormatId = reader.ReadString(),
                AlphaPrecision = ReadAlphaPrecision(reader),
                IndexingMethodId = reader.ReadString()
            };
            if (textureSettings.MaxResolution < 0) {
                throw new InvalidOperationException("Texture max resolution cannot be negative.");
            } else if (string.IsNullOrWhiteSpace(textureSettings.ColorFormatId)) {
                throw new InvalidOperationException("Texture color format id must be provided.");
            }

            return textureSettings;
        }

        /// <summary>
        /// Validates that the supplied payload is one texture settings instance.
        /// </summary>
        /// <param name="settings">Payload instance to validate.</param>
        /// <returns>Validated texture settings payload.</returns>
        TextureAssetProcessorSettings RequireSettings(object settings) {
            if (settings is not TextureAssetProcessorSettings textureSettings) {
                throw new InvalidOperationException($"Section '{SectionIdValue}' requires one {nameof(TextureAssetProcessorSettings)} payload.");
            }

            return textureSettings;
        }

        /// <summary>
        /// Reads one texture alpha-precision enum value from the binary stream.
        /// </summary>
        /// <param name="reader">Reader positioned at the serialized enum byte.</param>
        /// <returns>Deserialized texture alpha precision.</returns>
        TextureAssetAlphaPrecision ReadAlphaPrecision(EngineBinaryReader reader) {
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
    }
}

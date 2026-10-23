namespace helengine.editor {
    /// <summary>
    /// Defines serialization and comparison behavior for the registered font section.
    /// </summary>
    public sealed class FontAssetPlatformSettingsSectionDefinition : IAssetPlatformSettingsSectionDefinition {
        /// <summary>
        /// Gets the stable section identifier.
        /// </summary>
        public const string SectionIdValue = "font";

        /// <summary>Distinguishes an explicitly versioned extension from legacy positive pixel-size payloads.</summary>
        const int ExtendedPayloadMarker = -1;

        /// <summary>Version storing pixel policy and canonical Unicode scalar coverage.</summary>
        const byte ExtendedPayloadVersion = 2;

        /// <summary>
        /// Gets the registered section identifier.
        /// </summary>
        public string SectionId => SectionIdValue;

        /// <summary>
        /// Gets the payload type owned by the section.
        /// </summary>
        public Type SettingsType => typeof(FontAssetProcessorSettings);

        /// <summary>
        /// Creates one default font settings payload.
        /// </summary>
        /// <returns>Default font settings payload.</returns>
        public object CreateDefaultSettings() {
            return new FontAssetProcessorSettings();
        }

        /// <summary>
        /// Creates one deep clone of the supplied font settings payload.
        /// </summary>
        /// <param name="settings">Payload instance to clone.</param>
        /// <returns>Cloned font settings payload.</returns>
        public object CloneSettings(object settings) {
            FontAssetProcessorSettings source = RequireSettings(settings);
            return new FontAssetProcessorSettings {
                PixelSize = source.PixelSize,
                SnapHorizontalMetricsToPixels = source.SnapHorizontalMetricsToPixels,
                Characters = source.Characters
            };
        }

        /// <summary>
        /// Returns whether two font settings payloads carry the same values.
        /// </summary>
        /// <param name="left">First payload instance.</param>
        /// <param name="right">Second payload instance.</param>
        /// <returns>True when both font payloads match.</returns>
        public bool SettingsEqual(object left, object right) {
            FontAssetProcessorSettings leftSettings = RequireSettings(left);
            FontAssetProcessorSettings rightSettings = RequireSettings(right);
            return leftSettings.PixelSize == rightSettings.PixelSize
                && leftSettings.SnapHorizontalMetricsToPixels == rightSettings.SnapHorizontalMetricsToPixels
                && leftSettings.Characters.Equals(rightSettings.Characters);
        }

        /// <summary>
        /// Serializes one font settings payload.
        /// </summary>
        /// <param name="writer">Writer that owns the destination stream.</param>
        /// <param name="settings">Payload instance to serialize.</param>
        public void Serialize(EngineBinaryWriter writer, object settings) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            FontAssetProcessorSettings fontSettings = RequireSettings(settings);
            if (fontSettings.PixelSize < 1) {
                throw new InvalidOperationException("Font pixel size must be greater than zero.");
            }

            writer.WriteInt32(ExtendedPayloadMarker);
            writer.WriteByte(ExtendedPayloadVersion);
            writer.WriteInt32(fontSettings.PixelSize);
            writer.WriteByte(fontSettings.SnapHorizontalMetricsToPixels ? (byte)1 : (byte)0);
            int[] characters = fontSettings.Characters.ToArray();
            writer.WriteInt32(characters.Length);
            foreach (int character in characters) { writer.WriteInt32(character); }
        }

        /// <summary>
        /// Deserializes one font settings payload.
        /// </summary>
        /// <param name="reader">Reader positioned at the payload body.</param>
        /// <returns>Deserialized font settings payload.</returns>
        public object Deserialize(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            int pixelSize = reader.ReadInt32();
            bool snapHorizontalMetrics = false;
            FontCharacterSet characters = FontCharacterSet.Legacy;
            if (pixelSize == ExtendedPayloadMarker) {
                byte version = reader.ReadByte();
                if (version != 1 && version != ExtendedPayloadVersion) { throw new InvalidOperationException("Unsupported font settings extension version."); }
                pixelSize = reader.ReadInt32();
                byte policy = reader.ReadByte();
                if (policy > 1) { throw new InvalidOperationException("Font horizontal pixel policy must be a boolean byte."); }
                snapHorizontalMetrics = policy == 1;
                if (version == ExtendedPayloadVersion) {
                    int count = reader.ReadInt32();
                    if (count < 0 || count > 0x110000 - 0x800) { throw new InvalidOperationException("Font character count is outside the Unicode scalar range."); }
                    int[] values = new int[count];
                    for (int index = 0; index < count; index++) {
                        int value = reader.ReadInt32();
                        if (value < 0 || value > 0x10ffff || value >= 0xd800 && value <= 0xdfff || index > 0 && value <= values[index - 1]) {
                            throw new InvalidOperationException("Persisted font characters must be valid, sorted, unique Unicode scalars.");
                        }
                        values[index] = value;
                    }
                    characters = FontCharacterSet.FromCodePoints(values);
                }
            }
            FontAssetProcessorSettings fontSettings = new FontAssetProcessorSettings {
                PixelSize = pixelSize,
                SnapHorizontalMetricsToPixels = snapHorizontalMetrics,
                Characters = characters
            };
            if (fontSettings.PixelSize < 1) {
                throw new InvalidOperationException("Font pixel size must be greater than zero.");
            }

            return fontSettings;
        }

        /// <summary>
        /// Validates that the supplied payload is one font settings instance.
        /// </summary>
        /// <param name="settings">Payload instance to validate.</param>
        /// <returns>Validated font settings payload.</returns>
        FontAssetProcessorSettings RequireSettings(object settings) {
            if (settings is not FontAssetProcessorSettings fontSettings) {
                throw new InvalidOperationException($"Section '{SectionIdValue}' requires one {nameof(FontAssetProcessorSettings)} payload.");
            }

            return fontSettings;
        }
    }
}

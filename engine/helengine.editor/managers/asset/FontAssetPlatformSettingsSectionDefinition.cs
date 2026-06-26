namespace helengine.editor {
    /// <summary>
    /// Defines serialization and comparison behavior for the registered font section.
    /// </summary>
    public sealed class FontAssetPlatformSettingsSectionDefinition : IAssetPlatformSettingsSectionDefinition {
        /// <summary>
        /// Gets the stable section identifier.
        /// </summary>
        public const string SectionIdValue = "font";

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
                PixelSize = source.PixelSize
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
            return leftSettings.PixelSize == rightSettings.PixelSize;
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

            writer.WriteInt32(fontSettings.PixelSize);
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

            FontAssetProcessorSettings fontSettings = new FontAssetProcessorSettings {
                PixelSize = reader.ReadInt32()
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

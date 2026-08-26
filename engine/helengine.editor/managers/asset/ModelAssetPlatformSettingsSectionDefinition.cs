namespace helengine.editor {
    /// <summary>
    /// Defines serialization and comparison behavior for the registered model section.
    /// </summary>
    public sealed class ModelAssetPlatformSettingsSectionDefinition : IAssetPlatformSettingsSectionDefinition {
        /// <summary>
        /// Gets the stable section identifier.
        /// </summary>
        public const string SectionIdValue = "model";

        /// <summary>
        /// Gets the registered section identifier.
        /// </summary>
        public string SectionId => SectionIdValue;

        /// <summary>
        /// Gets the payload type owned by the section.
        /// </summary>
        public Type SettingsType => typeof(ModelAssetProcessorSettings);

        /// <summary>
        /// Creates one default model settings payload.
        /// </summary>
        /// <returns>Default model settings payload.</returns>
        public object CreateDefaultSettings() {
            return new ModelAssetProcessorSettings();
        }

        /// <summary>
        /// Creates one deep clone of the supplied model settings payload.
        /// </summary>
        /// <param name="settings">Payload instance to clone.</param>
        /// <returns>Cloned model settings payload.</returns>
        public object CloneSettings(object settings) {
            ModelAssetProcessorSettings source = RequireSettings(settings);
            return new ModelAssetProcessorSettings {
                FlipWinding = source.FlipWinding,
                Tessellate = source.Tessellate,
                TessellationMaxEdgeLength = source.TessellationMaxEdgeLength
            };
        }

        /// <summary>
        /// Returns whether two model settings payloads carry the same values.
        /// </summary>
        /// <param name="left">First payload instance.</param>
        /// <param name="right">Second payload instance.</param>
        /// <returns>True when both model payloads match.</returns>
        public bool SettingsEqual(object left, object right) {
            ModelAssetProcessorSettings leftSettings = RequireSettings(left);
            ModelAssetProcessorSettings rightSettings = RequireSettings(right);
            return leftSettings.FlipWinding == rightSettings.FlipWinding
                && leftSettings.Tessellate == rightSettings.Tessellate
                && leftSettings.TessellationMaxEdgeLength == rightSettings.TessellationMaxEdgeLength;
        }

        /// <summary>
        /// Serializes one model settings payload.
        /// </summary>
        /// <param name="writer">Writer that owns the destination stream.</param>
        /// <param name="settings">Payload instance to serialize.</param>
        public void Serialize(EngineBinaryWriter writer, object settings) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            ModelAssetProcessorSettings modelSettings = RequireSettings(settings);
            writer.WriteByte(modelSettings.FlipWinding ? (byte)1 : (byte)0);
            writer.WriteByte(modelSettings.Tessellate ? (byte)1 : (byte)0);
            ValidateTessellationMaxEdgeLength(modelSettings.TessellationMaxEdgeLength);
            writer.WriteDouble(modelSettings.TessellationMaxEdgeLength);
        }

        /// <summary>
        /// Deserializes one model settings payload.
        /// </summary>
        /// <param name="reader">Reader positioned at the payload body.</param>
        /// <returns>Deserialized model settings payload.</returns>
        public object Deserialize(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            byte value = reader.ReadByte();
            ModelAssetProcessorSettings modelSettings = new ModelAssetProcessorSettings();
            if (value == 0) {
                modelSettings.FlipWinding = false;
            } else if (value == 1) {
                modelSettings.FlipWinding = true;
            } else {
                throw new InvalidOperationException($"Unsupported model flip-winding value '{value}'.");
            }

            modelSettings.Tessellate = ReadBooleanByte(reader, "tessellate");
            modelSettings.TessellationMaxEdgeLength = reader.ReadDouble();
            ValidateTessellationMaxEdgeLength(modelSettings.TessellationMaxEdgeLength);

            return modelSettings;
        }

        /// <summary>
        /// Reads one boolean byte from the model processor settings payload.
        /// </summary>
        /// <param name="reader">Reader positioned at the encoded boolean value.</param>
        /// <param name="settingName">Name of the setting represented by the byte.</param>
        /// <returns>Decoded boolean setting value.</returns>
        static bool ReadBooleanByte(EngineBinaryReader reader, string settingName) {
            byte value = reader.ReadByte();
            if (value == 0) {
                return false;
            } else if (value == 1) {
                return true;
            }

            throw new InvalidOperationException($"Unsupported model {settingName} value '{value}'.");
        }

        /// <summary>
        /// Validates one model tessellation edge-length setting.
        /// </summary>
        /// <param name="tessellationMaxEdgeLength">Configured maximum edge length.</param>
        static void ValidateTessellationMaxEdgeLength(double tessellationMaxEdgeLength) {
            if (double.IsNaN(tessellationMaxEdgeLength) || double.IsInfinity(tessellationMaxEdgeLength) || tessellationMaxEdgeLength <= 0d) {
                throw new InvalidOperationException("Model tessellation maximum edge length must be finite and greater than zero.");
            }
        }

        /// <summary>
        /// Validates that the supplied payload is one model settings instance.
        /// </summary>
        /// <param name="settings">Payload instance to validate.</param>
        /// <returns>Validated model settings payload.</returns>
        ModelAssetProcessorSettings RequireSettings(object settings) {
            if (settings is not ModelAssetProcessorSettings modelSettings) {
                throw new InvalidOperationException($"Section '{SectionIdValue}' requires one {nameof(ModelAssetProcessorSettings)} payload.");
            }

            return modelSettings;
        }
    }
}

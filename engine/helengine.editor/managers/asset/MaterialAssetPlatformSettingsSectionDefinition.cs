namespace helengine.editor {
    /// <summary>
    /// Defines serialization and comparison behavior for the registered material section.
    /// </summary>
    public sealed class MaterialAssetPlatformSettingsSectionDefinition : IAssetPlatformSettingsSectionDefinition {
        /// <summary>
        /// Gets the stable section identifier.
        /// </summary>
        public const string SectionIdValue = "material";

        /// <summary>
        /// Gets the registered section identifier.
        /// </summary>
        public string SectionId => SectionIdValue;

        /// <summary>
        /// Gets the payload type owned by the section.
        /// </summary>
        public Type SettingsType => typeof(MaterialAssetProcessorSettings);

        /// <summary>
        /// Creates one default material settings payload.
        /// </summary>
        /// <returns>Default material settings payload.</returns>
        public object CreateDefaultSettings() {
            return new MaterialAssetProcessorSettings();
        }

        /// <summary>
        /// Creates one deep clone of the supplied material settings payload.
        /// </summary>
        /// <param name="settings">Payload instance to clone.</param>
        /// <returns>Cloned material settings payload.</returns>
        public object CloneSettings(object settings) {
            MaterialAssetProcessorSettings source = RequireSettings(settings);
            MaterialAssetProcessorSettings clone = new MaterialAssetProcessorSettings {
                SchemaId = source.SchemaId
            };
            foreach (KeyValuePair<string, string> pair in source.FieldValues) {
                clone.FieldValues[pair.Key] = pair.Value;
            }

            return clone;
        }

        /// <summary>
        /// Returns whether two material settings payloads carry the same values.
        /// </summary>
        /// <param name="left">First payload instance.</param>
        /// <param name="right">Second payload instance.</param>
        /// <returns>True when both material payloads match.</returns>
        public bool SettingsEqual(object left, object right) {
            MaterialAssetProcessorSettings leftSettings = RequireSettings(left);
            MaterialAssetProcessorSettings rightSettings = RequireSettings(right);
            if (!string.Equals(leftSettings.SchemaId, rightSettings.SchemaId, StringComparison.Ordinal)) {
                return false;
            } else if (leftSettings.FieldValues.Count != rightSettings.FieldValues.Count) {
                return false;
            }

            foreach (KeyValuePair<string, string> pair in leftSettings.FieldValues) {
                if (!rightSettings.FieldValues.TryGetValue(pair.Key, out string otherValue)) {
                    return false;
                } else if (!string.Equals(pair.Value, otherValue, StringComparison.Ordinal)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Serializes one material settings payload.
        /// </summary>
        /// <param name="writer">Writer that owns the destination stream.</param>
        /// <param name="settings">Payload instance to serialize.</param>
        public void Serialize(EngineBinaryWriter writer, object settings) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            MaterialAssetProcessorSettings materialSettings = RequireSettings(settings);
            if (materialSettings.FieldValues == null) {
                throw new InvalidOperationException("Material field values must be provided.");
            }

            writer.WriteString(materialSettings.SchemaId ?? string.Empty);
            writer.WriteInt32(materialSettings.FieldValues.Count);
            foreach (KeyValuePair<string, string> pair in materialSettings.FieldValues) {
                if (string.IsNullOrWhiteSpace(pair.Key)) {
                    throw new InvalidOperationException("Material field id must be provided.");
                } else if (pair.Value == null) {
                    throw new InvalidOperationException($"Material field '{pair.Key}' must not be null.");
                }

                writer.WriteString(pair.Key);
                writer.WriteString(pair.Value);
            }
        }

        /// <summary>
        /// Deserializes one material settings payload.
        /// </summary>
        /// <param name="reader">Reader positioned at the payload body.</param>
        /// <returns>Deserialized material settings payload.</returns>
        public object Deserialize(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            MaterialAssetProcessorSettings materialSettings = new MaterialAssetProcessorSettings {
                SchemaId = reader.ReadString()
            };
            int fieldCount = reader.ReadInt32();
            if (fieldCount < 0) {
                throw new InvalidOperationException("Material field count cannot be negative.");
            }

            for (int fieldIndex = 0; fieldIndex < fieldCount; fieldIndex++) {
                string fieldId = reader.ReadString();
                if (string.IsNullOrWhiteSpace(fieldId)) {
                    throw new InvalidOperationException("Material field id must be provided.");
                }

                materialSettings.FieldValues[fieldId] = reader.ReadString();
            }

            return materialSettings;
        }

        /// <summary>
        /// Validates that the supplied payload is one material settings instance.
        /// </summary>
        /// <param name="settings">Payload instance to validate.</param>
        /// <returns>Validated material settings payload.</returns>
        MaterialAssetProcessorSettings RequireSettings(object settings) {
            if (settings is not MaterialAssetProcessorSettings materialSettings) {
                throw new InvalidOperationException($"Section '{SectionIdValue}' requires one {nameof(MaterialAssetProcessorSettings)} payload.");
            }

            return materialSettings;
        }
    }
}

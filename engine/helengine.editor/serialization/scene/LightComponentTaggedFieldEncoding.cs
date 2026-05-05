namespace helengine.editor {
    /// <summary>
    /// Provides shared tagged-field names and binary helpers for editor light component persistence.
    /// </summary>
    public static class LightComponentTaggedFieldEncoding {
        /// <summary>
        /// Stable tagged field name used for light color persistence.
        /// </summary>
        public const string ColorFieldName = "Color";

        /// <summary>
        /// Stable tagged field name used for light intensity persistence.
        /// </summary>
        public const string IntensityFieldName = "Intensity";

        /// <summary>
        /// Stable tagged field name used for light shadow-enabled persistence.
        /// </summary>
        public const string ShadowsEnabledFieldName = "ShadowsEnabled";

        /// <summary>
        /// Stable tagged field name used for light shadow-map mode persistence.
        /// </summary>
        public const string ShadowMapModeFieldName = "ShadowMapMode";

        /// <summary>
        /// Stable tagged field name used for light shadow-strength persistence.
        /// </summary>
        public const string ShadowStrengthFieldName = "ShadowStrength";

        /// <summary>
        /// Writes the common light fields shared by all built-in light component families.
        /// </summary>
        /// <param name="writer">Tagged field writer receiving the serialized fields.</param>
        /// <param name="lightComponent">Light whose common values should be serialized.</param>
        public static void WriteCommonFields(EditorTaggedSceneComponentFieldWriter writer, LightComponent lightComponent) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (lightComponent == null) {
                throw new ArgumentNullException(nameof(lightComponent));
            }

            writer.WriteField(ColorFieldName, fieldWriter => fieldWriter.WriteFloat4(lightComponent.Color));
            writer.WriteField(IntensityFieldName, fieldWriter => fieldWriter.WriteSingle(lightComponent.Intensity));
            writer.WriteField(ShadowsEnabledFieldName, fieldWriter => fieldWriter.WriteByte(lightComponent.ShadowsEnabled ? (byte)1 : (byte)0));
            writer.WriteField(ShadowMapModeFieldName, fieldWriter => fieldWriter.WriteByte((byte)lightComponent.ShadowMapMode));
            writer.WriteField(ShadowStrengthFieldName, fieldWriter => fieldWriter.WriteSingle(lightComponent.ShadowStrength));
        }

        /// <summary>
        /// Reads the common light fields shared by all built-in light component families.
        /// </summary>
        /// <param name="reader">Tagged field reader supplying the serialized fields.</param>
        /// <param name="lightComponent">Light that should receive the decoded values.</param>
        public static void ReadCommonFields(EditorTaggedSceneComponentFieldReader reader, LightComponent lightComponent) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            } else if (lightComponent == null) {
                throw new ArgumentNullException(nameof(lightComponent));
            }

            if (reader.TryGetFieldReader(ColorFieldName, out EngineBinaryReader colorReader)) {
                using (colorReader) {
                    lightComponent.Color = colorReader.ReadFloat4();
                }
            }

            if (reader.TryGetFieldReader(IntensityFieldName, out EngineBinaryReader intensityReader)) {
                using (intensityReader) {
                    lightComponent.Intensity = intensityReader.ReadSingle();
                }
            }

            if (reader.TryGetFieldReader(ShadowsEnabledFieldName, out EngineBinaryReader shadowsEnabledReader)) {
                using (shadowsEnabledReader) {
                    lightComponent.ShadowsEnabled = shadowsEnabledReader.ReadByte() != 0;
                }
            }

            if (reader.TryGetFieldReader(ShadowMapModeFieldName, out EngineBinaryReader shadowMapModeReader)) {
                using (shadowMapModeReader) {
                    lightComponent.ShadowMapMode = (ShadowMapMode)shadowMapModeReader.ReadByte();
                }
            }

            if (reader.TryGetFieldReader(ShadowStrengthFieldName, out EngineBinaryReader shadowStrengthReader)) {
                using (shadowStrengthReader) {
                    lightComponent.ShadowStrength = shadowStrengthReader.ReadSingle();
                }
            }
        }
    }
}

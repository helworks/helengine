namespace helengine {
    /// <summary>
    /// Deserializes packaged spot light components for player builds.
    /// </summary>
    public sealed class RuntimeSpotLightComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Stable serialized component id for spot light components.
        /// </summary>
        const string ComponentType = "helengine.SpotLightComponent";

        /// <inheritdoc />
        public string ComponentTypeId => ComponentType;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            } else if (!string.Equals(record.ComponentTypeId, ComponentType, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Spot light deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version == 1) {
                return LightComponentScenePayloadSerializer.ReadSpotLightVersion1(reader);
            }
            if (version != LightComponentScenePayloadSerializer.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported spot light payload version '{version}'.");
            }

            return LightComponentScenePayloadSerializer.ReadSpotLight(reader);
        }
    }
}

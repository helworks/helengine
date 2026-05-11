namespace helengine {
    /// <summary>
    /// Deserializes packaged point light components for player builds.
    /// </summary>
    public sealed class RuntimePointLightComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Stable serialized component id for point light components.
        /// </summary>
        const string ComponentType = "helengine.PointLightComponent";

        /// <inheritdoc />
        public string ComponentTypeId => ComponentType;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            } else if (!string.Equals(record.ComponentTypeId, ComponentType, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Point light deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != LightComponentScenePayloadSerializer.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported point light payload version '{version}'.");
            }

            return LightComponentScenePayloadSerializer.ReadPointLight(reader, version);
        }
    }
}

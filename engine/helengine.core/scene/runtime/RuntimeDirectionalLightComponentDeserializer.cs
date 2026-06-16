namespace helengine {
    /// <summary>
    /// Deserializes packaged directional-light components for player builds.
    /// </summary>
    public sealed class RuntimeDirectionalLightComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Stable serialized component id for directional-light components.
        /// </summary>
        const string ComponentType = "helengine.DirectionalLightComponent";

        /// <inheritdoc />
        public string ComponentTypeId => ComponentType;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentType, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Directional light component deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            return LightComponentScenePayloadSerializer.ReadDirectionalLight(reader, version);
        }
    }
}

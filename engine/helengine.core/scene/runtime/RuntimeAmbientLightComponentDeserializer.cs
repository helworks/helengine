namespace helengine {
    /// <summary>
    /// Deserializes packaged ambient light components for player builds.
    /// </summary>
    public sealed class RuntimeAmbientLightComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Stable serialized component id for ambient light components.
        /// </summary>
        const string ComponentType = "helengine.AmbientLightComponent";

        /// <inheritdoc />
        public string ComponentTypeId => ComponentType;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            } else if (!string.Equals(record.ComponentTypeId, ComponentType, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Ambient light deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != LightComponentScenePayloadSerializer.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported ambient light payload version '{version}'.");
            }

            return LightComponentScenePayloadSerializer.ReadAmbientLight(reader, version);
        }
    }
}

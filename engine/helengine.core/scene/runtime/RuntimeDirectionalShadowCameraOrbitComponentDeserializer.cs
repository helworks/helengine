namespace helengine {
    /// <summary>
    /// Deserializes packaged directional-shadow camera-orbit components for player builds.
    /// </summary>
    public sealed class RuntimeDirectionalShadowCameraOrbitComponentDeserializer : IRuntimeComponentDeserializer {
        /// <inheritdoc />
        public string ComponentTypeId => DirectionalShadowCameraOrbitComponent.SerializedComponentTypeId;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Directional-shadow camera-orbit deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != DirectionalShadowMotionComponentScenePayloadSerializer.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported directional-shadow camera-orbit payload version '{version}'.");
            }

            return DirectionalShadowMotionComponentScenePayloadSerializer.ReadCameraOrbit(reader);
        }
    }
}

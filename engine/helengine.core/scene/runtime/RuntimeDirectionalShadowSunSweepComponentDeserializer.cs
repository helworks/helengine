namespace helengine {
    /// <summary>
    /// Deserializes packaged directional-shadow sun-sweep components for player builds.
    /// </summary>
    public sealed class RuntimeDirectionalShadowSunSweepComponentDeserializer : IRuntimeComponentDeserializer {
        /// <inheritdoc />
        public string ComponentTypeId => DirectionalShadowSunSweepComponent.SerializedComponentTypeId;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Directional-shadow sun-sweep deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != DirectionalShadowMotionComponentScenePayloadSerializer.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported directional-shadow sun-sweep payload version '{version}'.");
            }

            return DirectionalShadowMotionComponentScenePayloadSerializer.ReadSunSweep(reader);
        }
    }
}

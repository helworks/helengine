namespace helengine {
    /// <summary>
    /// Deserializes packaged directional-shadow tower-spin components for player builds.
    /// </summary>
    public sealed class RuntimeDirectionalShadowTowerSpinComponentDeserializer : IRuntimeComponentDeserializer {
        /// <inheritdoc />
        public string ComponentTypeId => DirectionalShadowTowerSpinComponent.SerializedComponentTypeId;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            } else if (!string.Equals(record.ComponentTypeId, DirectionalShadowTowerSpinComponent.SerializedComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Directional shadow tower-spin deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != DirectionalShadowMotionComponentScenePayloadSerializer.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported directional shadow tower-spin payload version '{version}'.");
            }

            return DirectionalShadowMotionComponentScenePayloadSerializer.ReadTowerSpin(reader);
        }
    }
}

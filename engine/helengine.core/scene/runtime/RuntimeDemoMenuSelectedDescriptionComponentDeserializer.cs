namespace helengine {
    /// <summary>
    /// Deserializes the baked demo menu selected-description marker for player builds.
    /// </summary>
    public sealed class RuntimeDemoMenuSelectedDescriptionComponentDeserializer : IRuntimeComponentDeserializer {
        /// <inheritdoc />
        public string ComponentTypeId => DemoMenuSelectedDescriptionComponent.SerializedComponentTypeId;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != DemoMenuSelectedDescriptionComponent.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported demo menu selected-description component payload version '{version}'.");
            }

            return new DemoMenuSelectedDescriptionComponent();
        }
    }
}

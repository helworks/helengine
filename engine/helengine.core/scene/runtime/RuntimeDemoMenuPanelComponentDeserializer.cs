namespace helengine {
    /// <summary>
    /// Deserializes baked demo menu panel metadata for player builds.
    /// </summary>
    public sealed class RuntimeDemoMenuPanelComponentDeserializer : IRuntimeComponentDeserializer {
        /// <inheritdoc />
        public string ComponentTypeId => DemoMenuPanelComponent.SerializedComponentTypeId;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != DemoMenuPanelComponent.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported demo menu panel component payload version '{version}'.");
            }

            return new DemoMenuPanelComponent {
                PanelId = reader.ReadString()
            };
        }
    }
}

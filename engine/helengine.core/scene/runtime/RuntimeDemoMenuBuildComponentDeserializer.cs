namespace helengine {
    /// <summary>
    /// Deserializes baked demo menu root metadata for player builds.
    /// </summary>
    public sealed class RuntimeDemoMenuBuildComponentDeserializer : IRuntimeComponentDeserializer {
        /// <inheritdoc />
        public string ComponentTypeId => DemoMenuBuildComponent.SerializedComponentTypeId;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != DemoMenuBuildComponent.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported demo menu build component payload version '{version}'.");
            }

            return new DemoMenuBuildComponent {
                ProviderTypeName = reader.ReadString(),
                InitialPanelId = reader.ReadString()
            };
        }
    }
}

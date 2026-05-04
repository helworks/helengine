namespace helengine {
    /// <summary>
    /// Deserializes packaged menu-host components for player builds.
    /// </summary>
    public sealed class RuntimeMenuHostComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Gets the stable serialized component type id handled by the deserializer.
        /// </summary>
        public string ComponentTypeId => MenuHostComponent.SerializedComponentTypeId;

        /// <summary>
        /// Materializes one runtime menu-host component from its packaged scene record.
        /// </summary>
        /// <param name="record">Packaged scene record to deserialize.</param>
        /// <param name="referenceResolver">Resolver used to rebuild packaged asset references.</param>
        /// <returns>Loaded menu-host component instance.</returns>
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (referenceResolver == null) {
                throw new ArgumentNullException(nameof(referenceResolver));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Menu host component deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != MenuHostComponent.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported menu host component payload version '{version}'.");
            }

            MenuHostComponent component = new MenuHostComponent {
                ProviderTypeName = reader.ReadString()
            };
            return component;
        }
    }
}

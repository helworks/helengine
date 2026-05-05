namespace helengine.editor {
    /// <summary>
    /// Persists the baked demo menu root metadata stored on one scene entity.
    /// </summary>
    public class DemoMenuBuildComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Gets the concrete runtime component type handled by the descriptor.
        /// </summary>
        public Type ComponentType => typeof(DemoMenuBuildComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        public string ComponentTypeId => DemoMenuBuildComponent.SerializedComponentTypeId;

        /// <summary>
        /// Serializes one live baked demo menu root component into a scene component record.
        /// </summary>
        public SceneComponentAssetRecord SerializeComponent(Component component, int componentIndex, EntityComponentSaveState saveState) {
            if (component is not DemoMenuBuildComponent demoMenuBuildComponent) {
                throw new InvalidOperationException("Demo menu build descriptor received an unsupported component type.");
            }

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(DemoMenuBuildComponent.CurrentVersion);
            writer.WriteString(demoMenuBuildComponent.ProviderTypeName);
            writer.WriteString(demoMenuBuildComponent.InitialPanelId);

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Deserializes one scene component record back into a live baked demo menu root component.
        /// </summary>
        public Component DeserializeComponent(SceneComponentAssetRecord record, EntitySaveComponent saveComponent, ISceneAssetReferenceResolver referenceResolver) {
            if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Demo menu build descriptor cannot deserialize '{record.ComponentTypeId}'.");
            }

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

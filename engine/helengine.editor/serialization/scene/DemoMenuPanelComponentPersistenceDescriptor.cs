namespace helengine.editor {
    /// <summary>
    /// Persists one baked demo menu panel metadata component.
    /// </summary>
    public class DemoMenuPanelComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Gets the concrete runtime component type handled by the descriptor.
        /// </summary>
        public Type ComponentType => typeof(DemoMenuPanelComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        public string ComponentTypeId => DemoMenuPanelComponent.SerializedComponentTypeId;

        /// <summary>
        /// Serializes one baked demo menu panel metadata component into a scene record.
        /// </summary>
        public SceneComponentAssetRecord SerializeComponent(Component component, int componentIndex, EntityComponentSaveState saveState) {
            if (component is not DemoMenuPanelComponent demoMenuPanelComponent) {
                throw new InvalidOperationException("Demo menu panel descriptor received an unsupported component type.");
            }

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(DemoMenuPanelComponent.CurrentVersion);
            writer.WriteString(demoMenuPanelComponent.PanelId);

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Deserializes one scene record back into a baked demo menu panel metadata component.
        /// </summary>
        public Component DeserializeComponent(SceneComponentAssetRecord record, EntitySaveComponent saveComponent, ISceneAssetReferenceResolver referenceResolver) {
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

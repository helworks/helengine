namespace helengine.editor {
    /// <summary>
    /// Persists the baked demo menu root metadata stored on one scene entity inside tolerant editor scene payloads.
    /// </summary>
    public class DemoMenuBuildComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Stable tagged field name used for menu provider-type persistence.
        /// </summary>
        const string ProviderTypeNameFieldName = "ProviderTypeName";

        /// <summary>
        /// Stable tagged field name used for initial-panel id persistence.
        /// </summary>
        const string InitialPanelIdFieldName = "InitialPanelId";

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

            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField(ProviderTypeNameFieldName, fieldWriter => fieldWriter.WriteString(demoMenuBuildComponent.ProviderTypeName));
            writer.WriteField(InitialPanelIdFieldName, fieldWriter => fieldWriter.WriteString(demoMenuBuildComponent.InitialPanelId));

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = writer.BuildPayload()
            };
        }

        /// <summary>
        /// Deserializes one scene component record back into a live baked demo menu root component.
        /// </summary>
        public Component DeserializeComponent(SceneComponentAssetRecord record, EntitySaveComponent saveComponent, ISceneAssetReferenceResolver referenceResolver) {
            if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Demo menu build descriptor cannot deserialize '{record.ComponentTypeId}'.");
            }

            DemoMenuBuildComponent component = new DemoMenuBuildComponent();
            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());
            if (reader.TryGetFieldReader(ProviderTypeNameFieldName, out EngineBinaryReader providerTypeNameReader)) {
                using (providerTypeNameReader) {
                    component.ProviderTypeName = providerTypeNameReader.ReadString();
                }
            }
            if (reader.TryGetFieldReader(InitialPanelIdFieldName, out EngineBinaryReader initialPanelIdReader)) {
                using (initialPanelIdReader) {
                    component.InitialPanelId = initialPanelIdReader.ReadString();
                }
            }

            return component;
        }
    }
}

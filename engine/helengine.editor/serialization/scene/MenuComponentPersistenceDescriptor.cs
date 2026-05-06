namespace helengine.editor {
    /// <summary>
    /// Persists the baked demo menu root metadata stored on one scene entity inside tolerant editor scene payloads.
    /// </summary>
    public class MenuComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
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
        public Type ComponentType => typeof(MenuComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        public string ComponentTypeId => MenuComponent.SerializedComponentTypeId;

        /// <summary>
        /// Serializes one live baked demo menu root component into a scene component record.
        /// </summary>
        public SceneComponentAssetRecord SerializeComponent(Component component, int componentIndex, EntityComponentSaveState saveState) {
            if (component is not MenuComponent menuComponent) {
                throw new InvalidOperationException("Menu descriptor received an unsupported component type.");
            }

            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField(ProviderTypeNameFieldName, fieldWriter => fieldWriter.WriteString(menuComponent.ProviderTypeName));
            writer.WriteField(InitialPanelIdFieldName, fieldWriter => fieldWriter.WriteString(menuComponent.InitialPanelId));

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

            MenuComponent component = new MenuComponent();
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

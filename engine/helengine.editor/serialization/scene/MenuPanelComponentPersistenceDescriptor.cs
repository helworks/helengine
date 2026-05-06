namespace helengine.editor {
    /// <summary>
    /// Persists one baked demo menu panel metadata component inside tolerant editor scene payloads.
    /// </summary>
    public class MenuPanelComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Stable tagged field name used for menu panel-id persistence.
        /// </summary>
        const string PanelIdFieldName = "PanelId";

        /// <summary>
        /// Gets the concrete runtime component type handled by the descriptor.
        /// </summary>
        public Type ComponentType => typeof(MenuPanelComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        public string ComponentTypeId => MenuPanelComponent.SerializedComponentTypeId;

        /// <summary>
        /// Serializes one baked demo menu panel metadata component into a scene record.
        /// </summary>
        public SceneComponentAssetRecord SerializeComponent(Component component, int componentIndex, EntityComponentSaveState saveState) {
            if (component is not MenuPanelComponent menuPanelComponent) {
                throw new InvalidOperationException("Menu panel descriptor received an unsupported component type.");
            }

            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField(PanelIdFieldName, fieldWriter => fieldWriter.WriteString(menuPanelComponent.PanelId));

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = writer.BuildPayload()
            };
        }

        /// <summary>
        /// Deserializes one scene record back into a baked demo menu panel metadata component.
        /// </summary>
        public Component DeserializeComponent(SceneComponentAssetRecord record, EntitySaveComponent saveComponent, ISceneAssetReferenceResolver referenceResolver) {
            MenuPanelComponent component = new MenuPanelComponent();
            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());
            if (reader.TryGetFieldReader(PanelIdFieldName, out EngineBinaryReader panelIdReader)) {
                using (panelIdReader) {
                    component.PanelId = panelIdReader.ReadString();
                }
            }

            return component;
        }
    }
}

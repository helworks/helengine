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
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Menu panel descriptor cannot deserialize '{record.ComponentTypeId}'.");
            }

            try {
                return DeserializeTaggedComponent(record);
            } catch (Exception ex) when (ex is InvalidOperationException || ex is EndOfStreamException) {
                if (TryDeserializeCookedRuntimeComponent(record, out MenuPanelComponent cookedRuntimeComponent)) {
                    return cookedRuntimeComponent;
                }

                throw;
            }
        }

        /// <summary>
        /// Deserializes one authored tagged menu-panel payload back into a live runtime component.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <returns>Live menu-panel component reconstructed from the tagged editor payload.</returns>
        MenuPanelComponent DeserializeTaggedComponent(SceneComponentAssetRecord record) {
            MenuPanelComponent component = new MenuPanelComponent();
            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());
            if (reader.TryGetFieldReader(PanelIdFieldName, out EngineBinaryReader panelIdReader)) {
                using (panelIdReader) {
                    component.PanelId = panelIdReader.ReadString();
                }
            }

            return component;
        }

        /// <summary>
        /// Attempts to deserialize one strict cooked-runtime menu-panel payload written by the scene packager.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <param name="component">Cooked-runtime menu-panel component when deserialization succeeds.</param>
        /// <returns>True when the payload matched the cooked runtime layout; otherwise false.</returns>
        bool TryDeserializeCookedRuntimeComponent(SceneComponentAssetRecord record, out MenuPanelComponent component) {
            try {
                component = (MenuPanelComponent)new RuntimeMenuPanelComponentDeserializer().Deserialize(record, null);
                return true;
            } catch (Exception ex) when (ex is InvalidOperationException || ex is EndOfStreamException) {
                component = null;
                return false;
            }
        }
    }
}

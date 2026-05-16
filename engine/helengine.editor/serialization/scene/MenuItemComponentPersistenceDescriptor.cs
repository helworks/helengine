namespace helengine.editor {
    /// <summary>
    /// Persists one baked demo menu item metadata component inside tolerant editor scene payloads.
    /// </summary>
    public class MenuItemComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Stable tagged field name used for menu panel-id persistence.
        /// </summary>
        const string PanelIdFieldName = "PanelId";

        /// <summary>
        /// Stable tagged field name used for menu item-id persistence.
        /// </summary>
        const string ItemIdFieldName = "ItemId";

        /// <summary>
        /// Stable tagged field name used for menu description persistence.
        /// </summary>
        const string DescriptionFieldName = "Description";

        /// <summary>
        /// Stable tagged field name used for menu action-kind persistence.
        /// </summary>
        const string ActionKindFieldName = "ActionKind";

        /// <summary>
        /// Stable tagged field name used for menu target-id persistence.
        /// </summary>
        const string TargetIdFieldName = "TargetId";

        /// <summary>
        /// Stable tagged field name used for idle fill-color persistence.
        /// </summary>
        const string IdleFillColorFieldName = "IdleFillColor";

        /// <summary>
        /// Stable tagged field name used for idle border-color persistence.
        /// </summary>
        const string IdleBorderColorFieldName = "IdleBorderColor";

        /// <summary>
        /// Stable tagged field name used for selected fill-color persistence.
        /// </summary>
        const string SelectedFillColorFieldName = "SelectedFillColor";

        /// <summary>
        /// Stable tagged field name used for selected border-color persistence.
        /// </summary>
        const string SelectedBorderColorFieldName = "SelectedBorderColor";

        /// <summary>
        /// Gets the concrete runtime component type handled by the descriptor.
        /// </summary>
        public Type ComponentType => typeof(MenuItemComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        public string ComponentTypeId => MenuItemComponent.SerializedComponentTypeId;

        /// <summary>
        /// Serializes one baked demo menu item metadata component into a scene record.
        /// </summary>
        public SceneComponentAssetRecord SerializeComponent(Component component, int componentIndex, EntityComponentSaveState saveState) {
            if (component is not MenuItemComponent menuItemComponent) {
                throw new InvalidOperationException("Menu item descriptor received an unsupported component type.");
            }

            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField(PanelIdFieldName, fieldWriter => fieldWriter.WriteString(menuItemComponent.PanelId));
            writer.WriteField(ItemIdFieldName, fieldWriter => fieldWriter.WriteString(menuItemComponent.ItemId));
            writer.WriteField(DescriptionFieldName, fieldWriter => fieldWriter.WriteString(menuItemComponent.Description));
            writer.WriteField(ActionKindFieldName, fieldWriter => fieldWriter.WriteByte((byte)menuItemComponent.ActionKind));
            writer.WriteField(TargetIdFieldName, fieldWriter => fieldWriter.WriteString(menuItemComponent.TargetId));
            writer.WriteField(IdleFillColorFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteByte4(fieldWriter, menuItemComponent.IdleFillColor));
            writer.WriteField(IdleBorderColorFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteByte4(fieldWriter, menuItemComponent.IdleBorderColor));
            writer.WriteField(SelectedFillColorFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteByte4(fieldWriter, menuItemComponent.SelectedFillColor));
            writer.WriteField(SelectedBorderColorFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteByte4(fieldWriter, menuItemComponent.SelectedBorderColor));

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = writer.BuildPayload()
            };
        }

        /// <summary>
        /// Deserializes one scene record back into a baked demo menu item metadata component.
        /// </summary>
        public Component DeserializeComponent(SceneComponentAssetRecord record, EntitySaveComponent saveComponent, ISceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Menu item descriptor cannot deserialize '{record.ComponentTypeId}'.");
            }

            try {
                return DeserializeTaggedComponent(record);
            } catch (Exception ex) when (ex is InvalidOperationException || ex is EndOfStreamException) {
                if (TryDeserializeCookedRuntimeComponent(record, out MenuItemComponent cookedRuntimeComponent)) {
                    return cookedRuntimeComponent;
                }

                throw;
            }
        }

        /// <summary>
        /// Deserializes one authored tagged menu-item payload back into a live runtime component.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <returns>Live menu-item component reconstructed from the tagged editor payload.</returns>
        MenuItemComponent DeserializeTaggedComponent(SceneComponentAssetRecord record) {
            MenuItemComponent component = new MenuItemComponent();
            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());
            if (reader.TryGetFieldReader(PanelIdFieldName, out EngineBinaryReader panelIdReader)) {
                using (panelIdReader) {
                    component.PanelId = panelIdReader.ReadString();
                }
            }
            if (reader.TryGetFieldReader(ItemIdFieldName, out EngineBinaryReader itemIdReader)) {
                using (itemIdReader) {
                    component.ItemId = itemIdReader.ReadString();
                }
            }
            if (reader.TryGetFieldReader(DescriptionFieldName, out EngineBinaryReader descriptionReader)) {
                using (descriptionReader) {
                    component.Description = descriptionReader.ReadString();
                }
            }
            if (reader.TryGetFieldReader(ActionKindFieldName, out EngineBinaryReader actionKindReader)) {
                using (actionKindReader) {
                    component.ActionKind = (MenuActionKind)actionKindReader.ReadByte();
                }
            }
            if (reader.TryGetFieldReader(TargetIdFieldName, out EngineBinaryReader targetIdReader)) {
                using (targetIdReader) {
                    component.TargetId = targetIdReader.ReadString();
                }
            }
            if (reader.TryGetFieldReader(IdleFillColorFieldName, out EngineBinaryReader idleFillColorReader)) {
                using (idleFillColorReader) {
                    component.IdleFillColor = SceneComponentBinaryFieldEncoding.ReadByte4(idleFillColorReader);
                }
            }
            if (reader.TryGetFieldReader(IdleBorderColorFieldName, out EngineBinaryReader idleBorderColorReader)) {
                using (idleBorderColorReader) {
                    component.IdleBorderColor = SceneComponentBinaryFieldEncoding.ReadByte4(idleBorderColorReader);
                }
            }
            if (reader.TryGetFieldReader(SelectedFillColorFieldName, out EngineBinaryReader selectedFillColorReader)) {
                using (selectedFillColorReader) {
                    component.SelectedFillColor = SceneComponentBinaryFieldEncoding.ReadByte4(selectedFillColorReader);
                }
            }
            if (reader.TryGetFieldReader(SelectedBorderColorFieldName, out EngineBinaryReader selectedBorderColorReader)) {
                using (selectedBorderColorReader) {
                    component.SelectedBorderColor = SceneComponentBinaryFieldEncoding.ReadByte4(selectedBorderColorReader);
                }
            }

            return component;
        }

        /// <summary>
        /// Attempts to deserialize one strict cooked-runtime menu-item payload written by the scene packager.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <param name="component">Cooked-runtime menu-item component when deserialization succeeds.</param>
        /// <returns>True when the payload matched the cooked runtime layout; otherwise false.</returns>
        bool TryDeserializeCookedRuntimeComponent(SceneComponentAssetRecord record, out MenuItemComponent component) {
            try {
                component = (MenuItemComponent)new RuntimeMenuItemComponentDeserializer().Deserialize(record, null);
                return true;
            } catch (Exception ex) when (ex is InvalidOperationException || ex is EndOfStreamException) {
                component = null;
                return false;
            }
        }
    }
}

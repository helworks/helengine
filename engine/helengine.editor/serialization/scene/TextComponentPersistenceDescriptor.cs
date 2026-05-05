namespace helengine.editor {
    /// <summary>
    /// Persists authored text component state and font references inside tolerant editor scene payloads.
    /// </summary>
    public class TextComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Stable tagged field name used for text font-reference persistence.
        /// </summary>
        const string FontReferenceFieldName = "FontReference";

        /// <summary>
        /// Stable tagged field name used for text content persistence.
        /// </summary>
        const string TextFieldName = "Text";

        /// <summary>
        /// Stable tagged field name used for text wrap-mode persistence.
        /// </summary>
        const string WrapTextFieldName = "WrapText";

        /// <summary>
        /// Stable tagged field name used for text size persistence.
        /// </summary>
        const string SizeFieldName = "Size";

        /// <summary>
        /// Stable tagged field name used for text color persistence.
        /// </summary>
        const string ColorFieldName = "Color";

        /// <summary>
        /// Stable tagged field name used for text source-rectangle persistence.
        /// </summary>
        const string SourceRectFieldName = "SourceRect";

        /// <summary>
        /// Stable tagged field name used for text rotation persistence.
        /// </summary>
        const string RotationFieldName = "Rotation";

        /// <summary>
        /// Stable tagged field name used for text 2D render-order persistence.
        /// </summary>
        const string RenderOrder2DFieldName = "RenderOrder2D";

        /// <summary>
        /// Stable tagged field name used for text layer-mask persistence.
        /// </summary>
        const string LayerMaskFieldName = "LayerMask";

        /// <summary>
        /// Stable tagged field name used for text-selection persistence.
        /// </summary>
        const string SelectionEnabledFieldName = "SelectionEnabled";

        /// <summary>
        /// Gets the concrete runtime component type handled by the descriptor.
        /// </summary>
        public Type ComponentType => typeof(TextComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        public string ComponentTypeId => "helengine.TextComponent";

        /// <summary>
        /// Serializes one live text component into a scene component record.
        /// </summary>
        /// <param name="component">Live text component instance to serialize.</param>
        /// <param name="componentIndex">Entity-local index used to preserve component ordering.</param>
        /// <param name="saveState">Editor-time save metadata associated with the component.</param>
        /// <returns>Serialized scene component record.</returns>
        public SceneComponentAssetRecord SerializeComponent(Component component, int componentIndex, EntityComponentSaveState saveState) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            }
            if (component is not TextComponent textComponent) {
                throw new InvalidOperationException("Text component descriptor received an unsupported component type.");
            }

            SceneAssetReference fontReference = FontAssetScenePersistenceSupport.ResolveFontReference(nameof(TextComponent), textComponent.Font, saveState);
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField(FontReferenceFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteOptionalReference(fieldWriter, fontReference));
            writer.WriteField(TextFieldName, fieldWriter => fieldWriter.WriteString(textComponent.Text));
            writer.WriteField(WrapTextFieldName, fieldWriter => fieldWriter.WriteByte(textComponent.WrapText ? (byte)1 : (byte)0));
            writer.WriteField(SizeFieldName, fieldWriter => fieldWriter.WriteInt2(textComponent.Size));
            writer.WriteField(ColorFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteByte4(fieldWriter, textComponent.Color));
            writer.WriteField(SourceRectFieldName, fieldWriter => fieldWriter.WriteFloat4(textComponent.SourceRect));
            writer.WriteField(RotationFieldName, fieldWriter => fieldWriter.WriteSingle(textComponent.Rotation));
            writer.WriteField(RenderOrder2DFieldName, fieldWriter => fieldWriter.WriteByte(textComponent.RenderOrder2D));
            writer.WriteField(LayerMaskFieldName, fieldWriter => fieldWriter.WriteByte(textComponent.LayerMask));
            writer.WriteField(SelectionEnabledFieldName, fieldWriter => fieldWriter.WriteByte(textComponent.SelectionEnabled ? (byte)1 : (byte)0));

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = writer.BuildPayload()
            };
        }

        /// <summary>
        /// Deserializes one scene component record back into a live text component instance.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <param name="saveComponent">Hidden entity save component that should receive restored metadata.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime asset references.</param>
        /// <returns>Live text component reconstructed from the scene record.</returns>
        public Component DeserializeComponent(
            SceneComponentAssetRecord record,
            EntitySaveComponent saveComponent,
            ISceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Text component descriptor cannot deserialize '{record.ComponentTypeId}'.");
            }

            TextComponent textComponent = new TextComponent();
            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());
            if (reader.TryGetFieldReader(TextFieldName, out EngineBinaryReader textReader)) {
                using (textReader) {
                    textComponent.Text = textReader.ReadString();
                }
            }
            if (reader.TryGetFieldReader(WrapTextFieldName, out EngineBinaryReader wrapTextReader)) {
                using (wrapTextReader) {
                    textComponent.WrapText = wrapTextReader.ReadByte() != 0;
                }
            }
            if (reader.TryGetFieldReader(SizeFieldName, out EngineBinaryReader sizeReader)) {
                using (sizeReader) {
                    textComponent.Size = sizeReader.ReadInt2();
                }
            }
            if (reader.TryGetFieldReader(ColorFieldName, out EngineBinaryReader colorReader)) {
                using (colorReader) {
                    textComponent.Color = SceneComponentBinaryFieldEncoding.ReadByte4(colorReader);
                }
            }
            if (reader.TryGetFieldReader(SourceRectFieldName, out EngineBinaryReader sourceRectReader)) {
                using (sourceRectReader) {
                    textComponent.SourceRect = sourceRectReader.ReadFloat4();
                }
            }
            if (reader.TryGetFieldReader(RotationFieldName, out EngineBinaryReader rotationReader)) {
                using (rotationReader) {
                    textComponent.Rotation = rotationReader.ReadSingle();
                }
            }
            if (reader.TryGetFieldReader(RenderOrder2DFieldName, out EngineBinaryReader renderOrder2DReader)) {
                using (renderOrder2DReader) {
                    textComponent.RenderOrder2D = renderOrder2DReader.ReadByte();
                }
            }
            if (reader.TryGetFieldReader(LayerMaskFieldName, out EngineBinaryReader layerMaskReader)) {
                using (layerMaskReader) {
                    textComponent.LayerMask = layerMaskReader.ReadByte();
                }
            }
            if (reader.TryGetFieldReader(SelectionEnabledFieldName, out EngineBinaryReader selectionEnabledReader)) {
                using (selectionEnabledReader) {
                    textComponent.SelectionEnabled = selectionEnabledReader.ReadByte() != 0;
                }
            }

            if (reader.TryGetFieldReader(FontReferenceFieldName, out EngineBinaryReader fontReferenceReader)) {
                using (fontReferenceReader) {
                    SceneAssetReference fontReference = SceneComponentBinaryFieldEncoding.ReadOptionalReference(fontReferenceReader);
                    if (fontReference != null) {
                        textComponent.Font = FontAssetScenePersistenceSupport.ResolveFont(referenceResolver, fontReference);
                        if (saveComponent != null) {
                            saveComponent.SetAssetReference(textComponent, FontAssetScenePersistenceSupport.FontReferenceName, fontReference);
                        }
                    }
                }
            } else if (Core.Instance != null && Core.Instance.DefaultFontAsset != null) {
                textComponent.Font = Core.Instance.DefaultFontAsset;
                if (saveComponent != null) {
                    saveComponent.SetAssetReference(textComponent, FontAssetScenePersistenceSupport.FontReferenceName, FontAssetScenePersistenceSupport.BuildEditorFontReference());
                }
            } else if (textComponent.Font == null) {
                throw new InvalidOperationException("TextComponent requires a font asset reference before deserialization.");
            }

            return textComponent;
        }
    }
}

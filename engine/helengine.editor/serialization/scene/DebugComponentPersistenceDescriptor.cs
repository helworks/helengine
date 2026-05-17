namespace helengine.editor {
    /// <summary>
    /// Persists the authored configuration for the runtime debug overlay component inside tolerant editor scene payloads.
    /// </summary>
    public class DebugComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Stable tagged field name used for debug font-reference persistence.
        /// </summary>
        const string FontReferenceFieldName = "FontReference";

        /// <summary>
        /// Stable tagged field name used for debug refresh-interval persistence.
        /// </summary>
        const string RefreshIntervalSecondsFieldName = "RefreshIntervalSeconds";

        /// <summary>
        /// Stable tagged field name used for debug padding persistence.
        /// </summary>
        const string PaddingFieldName = "Padding";

        /// <summary>
        /// Stable tagged field name used for debug 2D render-order persistence.
        /// </summary>
        const string RenderOrder2DFieldName = "RenderOrder2D";

        /// <summary>
        /// Gets the concrete runtime component type handled by the descriptor.
        /// </summary>
        public Type ComponentType => typeof(DebugComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        public string ComponentTypeId => "helengine.DebugComponent";

        /// <summary>
        /// Serializes one live debug component into a scene component record.
        /// </summary>
        /// <param name="component">Live debug component instance to serialize.</param>
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
            if (component is not DebugComponent debugComponent) {
                throw new InvalidOperationException("Debug component descriptor received an unsupported component type.");
            }

            SceneAssetReference fontReference = debugComponent.Font == null
                ? null
                : FontAssetScenePersistenceSupport.ResolveFontReference(nameof(DebugComponent), debugComponent.Font, saveState);
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField(FontReferenceFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteOptionalReference(fieldWriter, fontReference));
            writer.WriteField(RefreshIntervalSecondsFieldName, fieldWriter => fieldWriter.WriteInt64(BitConverter.DoubleToInt64Bits(debugComponent.RefreshIntervalSeconds)));
            writer.WriteField(PaddingFieldName, fieldWriter => fieldWriter.WriteInt2(debugComponent.Padding));
            writer.WriteField(RenderOrder2DFieldName, fieldWriter => fieldWriter.WriteByte(debugComponent.RenderOrder2D));

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = writer.BuildPayload()
            };
        }

        /// <summary>
        /// Deserializes one scene component record back into a live debug component instance.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <param name="saveComponent">Hidden entity save component that should receive restored metadata.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime asset references.</param>
        /// <returns>Live debug component reconstructed from the scene record.</returns>
        public Component DeserializeComponent(
            SceneComponentAssetRecord record,
            EntitySaveComponent saveComponent,
            ISceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Debug component descriptor cannot deserialize '{record.ComponentTypeId}'.");
            }

            DebugComponent debugComponent = new DebugComponent();
            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());
            if (reader.TryGetFieldReader(RefreshIntervalSecondsFieldName, out EngineBinaryReader refreshIntervalReader)) {
                using (refreshIntervalReader) {
                    debugComponent.RefreshIntervalSeconds = BitConverter.Int64BitsToDouble(refreshIntervalReader.ReadInt64());
                }
            }
            if (reader.TryGetFieldReader(PaddingFieldName, out EngineBinaryReader paddingReader)) {
                using (paddingReader) {
                    debugComponent.Padding = paddingReader.ReadInt2();
                }
            }
            if (reader.TryGetFieldReader(RenderOrder2DFieldName, out EngineBinaryReader renderOrder2DReader)) {
                using (renderOrder2DReader) {
                    debugComponent.RenderOrder2D = renderOrder2DReader.ReadByte();
                }
            }

            if (reader.TryGetFieldReader(FontReferenceFieldName, out EngineBinaryReader fontReferenceReader)) {
                using (fontReferenceReader) {
                    SceneAssetReference fontReference = SceneComponentBinaryFieldEncoding.ReadOptionalReference(fontReferenceReader);
                    if (fontReference != null) {
                        debugComponent.Font = FontAssetScenePersistenceSupport.ResolveFont(referenceResolver, fontReference);
                        if (saveComponent != null) {
                            saveComponent.SetAssetReference(debugComponent, FontAssetScenePersistenceSupport.FontReferenceName, fontReference);
                        }
                    }
                }
            }

            return debugComponent;
        }
    }
}

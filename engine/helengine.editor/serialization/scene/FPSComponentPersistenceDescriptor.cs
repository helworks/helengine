namespace helengine.editor {
    /// <summary>
    /// Persists the authored configuration for the runtime FPS overlay component.
    /// </summary>
    public class FPSComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Current payload version for serialized FPS component records.
        /// </summary>
        const byte CurrentVersion = 2;

        /// <summary>
        /// Gets the concrete runtime component type handled by the descriptor.
        /// </summary>
        public Type ComponentType => typeof(FPSComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        public string ComponentTypeId => "helengine.FPSComponent";

        /// <summary>
        /// Attempts to resolve the serialized font reference for the component.
        /// </summary>
        /// <param name="component">FPS component being serialized.</param>
        /// <param name="saveState">Editor-time save metadata associated with the component.</param>
        /// <returns>Stable font reference for the component.</returns>
        /// <summary>
        /// Serializes one live FPS component into a scene component record.
        /// </summary>
        /// <param name="component">Live FPS component instance to serialize.</param>
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
            if (component is not FPSComponent fpsComponent) {
                throw new InvalidOperationException("FPS component descriptor received an unsupported component type.");
            }

            SceneAssetReference fontReference = FontAssetScenePersistenceSupport.ResolveFontReference(nameof(FPSComponent), fpsComponent.Font, saveState);
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(CurrentVersion);
            FontAssetScenePersistenceSupport.WriteOptionalReference(writer, fontReference);
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(fpsComponent.RefreshIntervalSeconds));
            writer.WriteInt2(fpsComponent.Padding);
            writer.WriteByte(fpsComponent.RenderOrder2D);

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Deserializes one scene component record back into a live FPS component instance.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <param name="saveComponent">Hidden entity save component that should receive restored metadata.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime asset references.</param>
        /// <returns>Live FPS component reconstructed from the scene record.</returns>
        public Component DeserializeComponent(
            SceneComponentAssetRecord record,
            EntitySaveComponent saveComponent,
            ISceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"FPS component descriptor cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CurrentVersion && version != 1) {
                throw new InvalidOperationException($"Unsupported FPS component payload version '{version}'.");
            }

            SceneAssetReference fontReference = version >= 2 ? FontAssetScenePersistenceSupport.ReadOptionalReference(reader) : null;

            FPSComponent fpsComponent = new FPSComponent {
                RefreshIntervalSeconds = BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                Padding = reader.ReadInt2(),
                RenderOrder2D = reader.ReadByte()
            };

            if (fontReference != null) {
                fpsComponent.Font = FontAssetScenePersistenceSupport.ResolveFont(referenceResolver, fontReference);
                if (saveComponent != null) {
                    saveComponent.SetAssetReference(fpsComponent, FontAssetScenePersistenceSupport.FontReferenceName, fontReference);
                }
            } else if (Core.Instance != null && Core.Instance.DefaultFontAsset != null) {
                fpsComponent.Font = Core.Instance.DefaultFontAsset;
                if (saveComponent != null) {
                    saveComponent.SetAssetReference(
                        fpsComponent,
                        FontAssetScenePersistenceSupport.FontReferenceName,
                        FontAssetScenePersistenceSupport.BuildEditorFontReference());
                }
            } else {
                throw new InvalidOperationException("FPSComponent requires a font asset reference before deserialization.");
            }

            return fpsComponent;
        }
    }
}

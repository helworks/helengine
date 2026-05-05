namespace helengine.editor {
    /// <summary>
    /// Persists point light component authored values inside tolerant editor scene payloads.
    /// </summary>
    public class PointLightComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Stable tagged field name used for point light range persistence.
        /// </summary>
        const string RangeFieldName = "Range";

        /// <summary>
        /// Gets the concrete runtime component type handled by the descriptor.
        /// </summary>
        public Type ComponentType => typeof(PointLightComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        public string ComponentTypeId => "helengine.PointLightComponent";

        /// <summary>
        /// Serializes one live point light into a scene component record.
        /// </summary>
        /// <param name="component">Live point light instance to serialize.</param>
        /// <param name="componentIndex">Entity-local index used to preserve component ordering.</param>
        /// <param name="saveState">Editor-time save metadata associated with the component.</param>
        /// <returns>Serialized scene component record.</returns>
        public SceneComponentAssetRecord SerializeComponent(Component component, int componentIndex, EntityComponentSaveState saveState) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            } else if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            } else if (component is not PointLightComponent) {
                throw new InvalidOperationException("Point light descriptor received an unsupported component type.");
            }

            PointLightComponent lightComponent = (PointLightComponent)component;
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            LightComponentTaggedFieldEncoding.WriteCommonFields(writer, lightComponent);
            writer.WriteField(RangeFieldName, fieldWriter => fieldWriter.WriteSingle(lightComponent.Range));
            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = writer.BuildPayload()
            };
        }

        /// <summary>
        /// Deserializes one scene component record back into a live point light instance.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <param name="saveComponent">Hidden entity save component that should receive restored metadata.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime asset references.</param>
        /// <returns>Live point light reconstructed from the scene record.</returns>
        public Component DeserializeComponent(SceneComponentAssetRecord record, EntitySaveComponent saveComponent, ISceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            } else if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Point light descriptor cannot deserialize '{record.ComponentTypeId}'.");
            }

            PointLightComponent lightComponent = new PointLightComponent();
            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());
            LightComponentTaggedFieldEncoding.ReadCommonFields(reader, lightComponent);
            if (reader.TryGetFieldReader(RangeFieldName, out EngineBinaryReader rangeReader)) {
                using (rangeReader) {
                    lightComponent.Range = rangeReader.ReadSingle();
                }
            }

            return lightComponent;
        }
    }
}

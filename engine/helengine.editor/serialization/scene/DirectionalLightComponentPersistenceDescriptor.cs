namespace helengine.editor {
    /// <summary>
    /// Persists directional light component authored values inside tolerant editor scene payloads.
    /// </summary>
    public class DirectionalLightComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Stable tagged field name used for directional light shadow-distance persistence.
        /// </summary>
        const string ShadowDistanceFieldName = "ShadowDistance";

        /// <summary>
        /// Gets the concrete runtime component type handled by the descriptor.
        /// </summary>
        public Type ComponentType => typeof(DirectionalLightComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        public string ComponentTypeId => "helengine.DirectionalLightComponent";

        /// <summary>
        /// Serializes one live directional light into a scene component record.
        /// </summary>
        /// <param name="component">Live directional light instance to serialize.</param>
        /// <param name="componentIndex">Entity-local index used to preserve component ordering.</param>
        /// <param name="saveState">Editor-time save metadata associated with the component.</param>
        /// <returns>Serialized scene component record.</returns>
        public SceneComponentAssetRecord SerializeComponent(Component component, int componentIndex, EntityComponentSaveState saveState) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            } else if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            } else if (component is not DirectionalLightComponent) {
                throw new InvalidOperationException("Directional light descriptor received an unsupported component type.");
            }

            DirectionalLightComponent lightComponent = (DirectionalLightComponent)component;
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            LightComponentTaggedFieldEncoding.WriteCommonFields(writer, lightComponent);
            writer.WriteField(ShadowDistanceFieldName, fieldWriter => fieldWriter.WriteSingle(lightComponent.ShadowDistance));

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = writer.BuildPayload()
            };
        }

        /// <summary>
        /// Deserializes one scene component record back into a live directional light instance.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <param name="saveComponent">Hidden entity save component that should receive restored metadata.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime asset references.</param>
        /// <returns>Live directional light reconstructed from the scene record.</returns>
        public Component DeserializeComponent(SceneComponentAssetRecord record, EntitySaveComponent saveComponent, ISceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            } else if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Directional light descriptor cannot deserialize '{record.ComponentTypeId}'.");
            }

            DirectionalLightComponent lightComponent = new DirectionalLightComponent();
            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());
            LightComponentTaggedFieldEncoding.ReadCommonFields(reader, lightComponent);
            if (reader.TryGetFieldReader(ShadowDistanceFieldName, out EngineBinaryReader shadowDistanceReader)) {
                using (shadowDistanceReader) {
                    lightComponent.ShadowDistance = shadowDistanceReader.ReadSingle();
                }
            }

            return lightComponent;
        }
    }
}

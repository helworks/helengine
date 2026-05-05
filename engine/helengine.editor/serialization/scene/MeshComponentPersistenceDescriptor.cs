namespace helengine.editor {
    /// <summary>
    /// Persists mesh component asset references and render order inside tolerant editor scene payloads.
    /// </summary>
    public class MeshComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Stable tagged field name used for mesh model-reference persistence.
        /// </summary>
        const string ModelReferenceFieldName = "ModelReference";

        /// <summary>
        /// Stable tagged field name used for mesh material-reference persistence.
        /// </summary>
        const string MaterialReferenceFieldName = "MaterialReference";

        /// <summary>
        /// Stable save-state slot name used for mesh model references.
        /// </summary>
        const string ModelReferenceName = "Model";

        /// <summary>
        /// Stable save-state slot name used for mesh material references.
        /// </summary>
        const string MaterialReferenceName = "Material";

        /// <summary>
        /// Stable tagged field name used for mesh render-order persistence.
        /// </summary>
        const string RenderOrder3DFieldName = "RenderOrder3D";

        /// <summary>
        /// Gets the concrete runtime component type handled by the descriptor.
        /// </summary>
        public Type ComponentType => typeof(MeshComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        public string ComponentTypeId => "helengine.MeshComponent";

        /// <summary>
        /// Serializes one live mesh component into a scene component record.
        /// </summary>
        /// <param name="component">Live mesh component instance to serialize.</param>
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
            if (component is not MeshComponent meshComponent) {
                throw new InvalidOperationException("Mesh component descriptor received an unsupported component type.");
            }

            SceneAssetReference modelReference = ResolveRequiredAssetReference(meshComponent.Model, saveState, ModelReferenceName);
            SceneAssetReference materialReference = ResolveRequiredAssetReference(meshComponent.Material, saveState, MaterialReferenceName);
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField(ModelReferenceFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteOptionalReference(fieldWriter, modelReference));
            writer.WriteField(MaterialReferenceFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteOptionalReference(fieldWriter, materialReference));
            writer.WriteField(RenderOrder3DFieldName, fieldWriter => fieldWriter.WriteByte(meshComponent.RenderOrder3D));

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = writer.BuildPayload()
            };
        }

        /// <summary>
        /// Deserializes one scene component record back into a live mesh component instance.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <param name="saveComponent">Hidden entity save component that should receive restored metadata.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime asset references.</param>
        /// <returns>Live mesh component reconstructed from the scene record.</returns>
        public Component DeserializeComponent(
            SceneComponentAssetRecord record,
            EntitySaveComponent saveComponent,
            ISceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (referenceResolver == null) {
                throw new ArgumentNullException(nameof(referenceResolver));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Mesh component descriptor cannot deserialize '{record.ComponentTypeId}'.");
            }

            MeshComponent meshComponent = new MeshComponent();
            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());
            if (reader.TryGetFieldReader(ModelReferenceFieldName, out EngineBinaryReader modelReferenceReader)) {
                using (modelReferenceReader) {
                    SceneAssetReference modelReference = SceneComponentBinaryFieldEncoding.ReadOptionalReference(modelReferenceReader);
                    if (modelReference != null) {
                        meshComponent.Model = referenceResolver.ResolveModel(modelReference);
                        if (saveComponent != null) {
                            saveComponent.SetAssetReference(meshComponent, ModelReferenceName, modelReference);
                        }
                    }
                }
            }

            if (reader.TryGetFieldReader(MaterialReferenceFieldName, out EngineBinaryReader materialReferenceReader)) {
                using (materialReferenceReader) {
                    SceneAssetReference materialReference = SceneComponentBinaryFieldEncoding.ReadOptionalReference(materialReferenceReader);
                    if (materialReference != null) {
                        meshComponent.Material = referenceResolver.ResolveMaterial(materialReference);
                        if (saveComponent != null) {
                            saveComponent.SetAssetReference(meshComponent, MaterialReferenceName, materialReference);
                        }
                    }
                }
            }

            if (reader.TryGetFieldReader(RenderOrder3DFieldName, out EngineBinaryReader renderOrder3DReader)) {
                using (renderOrder3DReader) {
                    meshComponent.RenderOrder3D = renderOrder3DReader.ReadByte();
                }
            }

            return meshComponent;
        }

        /// <summary>
        /// Resolves a required stable asset reference for one runtime asset value.
        /// </summary>
        /// <param name="runtimeValue">Runtime asset value currently assigned to the component.</param>
        /// <param name="saveState">Editor-time save metadata associated with the component.</param>
        /// <param name="referenceName">Stable save-state slot name.</param>
        /// <returns>Stable asset reference for the runtime value, or null when the runtime value is null.</returns>
        SceneAssetReference ResolveRequiredAssetReference(object runtimeValue, EntityComponentSaveState saveState, string referenceName) {
            if (runtimeValue == null) {
                return null;
            }
            if (saveState == null || !saveState.TryGetAssetReference(referenceName, out SceneAssetReference reference)) {
                throw new InvalidOperationException($"MeshComponent {referenceName} is assigned but does not have a stored scene asset reference.");
            }

            return reference;
        }
    }
}

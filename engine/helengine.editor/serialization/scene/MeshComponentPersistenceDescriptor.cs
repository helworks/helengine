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
        /// Stable tagged field name used for mesh material-reference array persistence.
        /// </summary>
        const string MaterialReferencesFieldName = "MaterialReferences";

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
            SceneAssetReference[] materialReferences = ResolveMaterialReferences(meshComponent, saveState);
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField(ModelReferenceFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteOptionalReference(fieldWriter, modelReference));
            writer.WriteField(MaterialReferencesFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteOptionalReferenceArray(fieldWriter, materialReferences));
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

            SceneAssetReference[] materialReferences = ReadMaterialReferences(reader);
            meshComponent.SetMaterials(ResolveMaterials(materialReferences, referenceResolver));
            RestoreMaterialReferenceState(saveComponent, meshComponent, materialReferences);

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

        /// <summary>
        /// Resolves the ordered material references required by the current mesh component.
        /// </summary>
        /// <param name="meshComponent">Mesh component being serialized.</param>
        /// <param name="saveState">Editor-time save metadata associated with the component.</param>
        /// <returns>Ordered material references by submesh slot.</returns>
        SceneAssetReference[] ResolveMaterialReferences(MeshComponent meshComponent, EntityComponentSaveState saveState) {
            if (meshComponent == null) {
                throw new ArgumentNullException(nameof(meshComponent));
            }

            RuntimeMaterial[] runtimeMaterials = meshComponent.Materials;
            if (runtimeMaterials.Length == 0) {
                return Array.Empty<SceneAssetReference>();
            }

            SceneAssetReference[] references = new SceneAssetReference[runtimeMaterials.Length];
            for (int materialIndex = 0; materialIndex < runtimeMaterials.Length; materialIndex++) {
                references[materialIndex] = ResolveRequiredAssetReference(
                    runtimeMaterials[materialIndex],
                    saveState,
                    BuildMaterialReferenceName(materialIndex));
            }

            return references;
        }

        /// <summary>
        /// Reads one ordered material-reference array from the tagged mesh payload.
        /// </summary>
        /// <param name="reader">Tagged field reader positioned at the mesh payload.</param>
        /// <returns>Ordered material references by submesh slot.</returns>
        static SceneAssetReference[] ReadMaterialReferences(EditorTaggedSceneComponentFieldReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            if (!reader.TryGetFieldReader(MaterialReferencesFieldName, out EngineBinaryReader materialReferencesReader)) {
                throw new InvalidOperationException("Mesh component payload must include MaterialReferences.");
            }

            using (materialReferencesReader) {
                return SceneComponentBinaryFieldEncoding.ReadOptionalReferenceArray(materialReferencesReader);
            }
        }

        /// <summary>
        /// Resolves one stable save-state material-reference name for the supplied slot index.
        /// </summary>
        /// <param name="slotIndex">Zero-based material slot index.</param>
        /// <returns>Stable save-state reference name.</returns>
        static string BuildMaterialReferenceName(int slotIndex) {
            if (slotIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), "Material slot index must be non-negative.");
            }

            return slotIndex == 0
                ? MaterialReferenceName
                : string.Concat(MaterialReferenceName, "[", slotIndex.ToString(), "]");
        }

        /// <summary>
        /// Resolves one ordered runtime material array from serialized scene references.
        /// </summary>
        /// <param name="references">Serialized scene references ordered by submesh slot.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime materials.</param>
        /// <returns>Ordered runtime materials by submesh slot.</returns>
        static RuntimeMaterial[] ResolveMaterials(SceneAssetReference[] references, ISceneAssetReferenceResolver referenceResolver) {
            if (references == null) {
                throw new ArgumentNullException(nameof(references));
            } else if (referenceResolver == null) {
                throw new ArgumentNullException(nameof(referenceResolver));
            }

            RuntimeMaterial[] runtimeMaterials = new RuntimeMaterial[references.Length];
            for (int materialIndex = 0; materialIndex < references.Length; materialIndex++) {
                if (references[materialIndex] != null) {
                    runtimeMaterials[materialIndex] = referenceResolver.ResolveMaterial(references[materialIndex]);
                }
            }

            return runtimeMaterials;
        }

        /// <summary>
        /// Restores one ordered material-reference array into the editor save state for the supplied mesh component.
        /// </summary>
        /// <param name="saveComponent">Save component that should receive restored metadata.</param>
        /// <param name="meshComponent">Mesh component that owns the restored references.</param>
        /// <param name="references">Serialized scene references ordered by submesh slot.</param>
        static void RestoreMaterialReferenceState(
            EntitySaveComponent saveComponent,
            MeshComponent meshComponent,
            SceneAssetReference[] references) {
            if (saveComponent == null || meshComponent == null || references == null) {
                return;
            }

            for (int materialIndex = 0; materialIndex < references.Length; materialIndex++) {
                if (references[materialIndex] != null) {
                    saveComponent.SetAssetReference(meshComponent, BuildMaterialReferenceName(materialIndex), references[materialIndex]);
                }
            }
        }
    }
}

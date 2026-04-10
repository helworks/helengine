namespace helengine.editor {
    /// <summary>
    /// Persists mesh component asset references and render order inside scene files.
    /// </summary>
    public class MeshComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Current payload version for serialized mesh component records.
        /// </summary>
        const byte CurrentVersion = 1;
        /// <summary>
        /// Stable save-state slot name used for mesh model references.
        /// </summary>
        const string ModelReferenceName = "Model";
        /// <summary>
        /// Stable save-state slot name used for mesh material references.
        /// </summary>
        const string MaterialReferenceName = "Material";

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

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(CurrentVersion);
            WriteOptionalReference(writer, modelReference);
            WriteOptionalReference(writer, materialReference);
            writer.WriteByte(meshComponent.RenderOrder3D);

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = stream.ToArray()
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

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported mesh component payload version '{version}'.");
            }

            SceneAssetReference modelReference = ReadOptionalReference(reader);
            SceneAssetReference materialReference = ReadOptionalReference(reader);
            byte renderOrder3D = reader.ReadByte();

            MeshComponent meshComponent = new MeshComponent {
                RenderOrder3D = renderOrder3D
            };

            if (modelReference != null) {
                meshComponent.Model = referenceResolver.ResolveModel(modelReference);
                if (saveComponent != null) {
                    saveComponent.SetAssetReference(meshComponent, ModelReferenceName, modelReference);
                }
            }

            if (materialReference != null) {
                meshComponent.Material = referenceResolver.ResolveMaterial(materialReference);
                if (saveComponent != null) {
                    saveComponent.SetAssetReference(meshComponent, MaterialReferenceName, materialReference);
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
        /// Writes one optional scene asset reference to the component payload.
        /// </summary>
        /// <param name="writer">Destination writer receiving the payload.</param>
        /// <param name="reference">Optional stable scene asset reference.</param>
        void WriteOptionalReference(EngineBinaryWriter writer, SceneAssetReference reference) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteByte(reference == null ? (byte)0 : (byte)1);
            if (reference == null) {
                return;
            }

            writer.WriteInt32((int)reference.SourceKind);
            writer.WriteString(reference.RelativePath);
            writer.WriteString(reference.ProviderId);
            writer.WriteString(reference.AssetId);
        }

        /// <summary>
        /// Reads one optional scene asset reference from the component payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the reference payload.</param>
        /// <returns>Stable scene asset reference when present; otherwise null.</returns>
        SceneAssetReference ReadOptionalReference(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            if (reader.ReadByte() == 0) {
                return null;
            }

            return new SceneAssetReference {
                SourceKind = (SceneAssetReferenceSourceKind)reader.ReadInt32(),
                RelativePath = reader.ReadString(),
                ProviderId = reader.ReadString(),
                AssetId = reader.ReadString()
            };
        }
    }
}

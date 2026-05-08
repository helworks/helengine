namespace helengine {
    /// <summary>
    /// Deserializes packaged mesh components for player builds.
    /// </summary>
    public sealed class RuntimeMeshComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Current payload version for serialized mesh component scene records.
        /// </summary>
        const byte CurrentVersion = MeshComponentScenePayloadSerializer.CurrentVersion;

        /// <summary>
        /// Stable serialized component id for mesh components.
        /// </summary>
        const string ComponentType = "helengine.MeshComponent";

        /// <inheritdoc />
        public string ComponentTypeId => ComponentType;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (referenceResolver == null) {
                throw new ArgumentNullException(nameof(referenceResolver));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentType, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Mesh component deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            MeshComponentScenePayloadSerializer.Read(reader, out SceneAssetReference modelReference, out SceneAssetReference[] materialReferences, out byte renderOrder3D);

            MeshComponent meshComponent = new MeshComponent {
                RenderOrder3D = renderOrder3D
            };

            if (modelReference != null) {
                meshComponent.Model = referenceResolver.ResolveModel(modelReference);
            }

            meshComponent.SetMaterials(ResolveMaterials(materialReferences, referenceResolver));

            return meshComponent;
        }

        /// <summary>
        /// Resolves one ordered runtime material array from serialized scene references.
        /// </summary>
        /// <param name="references">Serialized scene references ordered by submesh slot.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime materials.</param>
        /// <returns>Ordered runtime materials by submesh slot.</returns>
        static RuntimeMaterial[] ResolveMaterials(SceneAssetReference[] references, RuntimeSceneAssetReferenceResolver referenceResolver) {
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
    }
}

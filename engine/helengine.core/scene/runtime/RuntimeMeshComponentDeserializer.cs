namespace helengine {
    /// <summary>
    /// Deserializes packaged mesh components for player builds.
    /// </summary>
    public sealed class RuntimeMeshComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Current payload version for serialized mesh component scene records.
        /// </summary>
        const byte CurrentVersion = 1;

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
            }

            if (materialReference != null) {
                meshComponent.Material = referenceResolver.ResolveMaterial(materialReference);
            }

            return meshComponent;
        }

        /// <summary>
        /// Reads one optional scene asset reference from the current payload position.
        /// </summary>
        /// <param name="reader">Reader positioned at the optional-reference payload.</param>
        /// <returns>Decoded scene asset reference when present; otherwise null.</returns>
        static SceneAssetReference ReadOptionalReference(EngineBinaryReader reader) {
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

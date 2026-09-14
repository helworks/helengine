using helengine;

namespace helengine.files {
    /// <summary>
    /// Serializes the payload body of a model asset for the editor asset format, including its vertex streams, index buffers and submesh table.
    /// </summary>
    public sealed class ModelAssetPayloadSerializer : IEditorAssetPayloadSerializer {
        /// <summary>
        /// Gets the value kind stamped into the header for model asset payloads.
        /// </summary>
        public EditorAssetBinaryValueKind ValueKind {
            get {
                return EditorAssetBinaryValueKind.ModelAsset;
            }
        }

        /// <summary>
        /// Reports whether the supplied asset is a model asset.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        /// <returns>True when the asset is a model asset.</returns>
        public bool Handles(Asset asset) {
            return asset is ModelAsset;
        }

        /// <summary>
        /// Performs no validation because a model payload carries no cross-record invariants that must hold before the header is written.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        public void Validate(Asset asset) {
        }

        /// <summary>
        /// Writes a model asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Model asset to serialize.</param>
        public void Write(EngineBinaryWriter writer, Asset asset) {
            ModelAsset modelAsset = (ModelAsset)asset;
            EditorAssetPayloadPrimitives.EnsureRuntimeAssetIdentity(modelAsset);
            EditorAssetPayloadPrimitives.WriteAssetIdentity(writer, modelAsset);
            writer.WriteArray(modelAsset.Positions, EditorAssetPayloadPrimitives.WriteFloat3Value);
            writer.WriteArray(modelAsset.Normals, EditorAssetPayloadPrimitives.WriteFloat3Value);
            writer.WriteArray(modelAsset.TexCoords, EditorAssetPayloadPrimitives.WriteFloat2Value);
            writer.WriteArray(modelAsset.Indices16, EditorAssetPayloadPrimitives.WriteUInt16Value);
            writer.WriteArray(modelAsset.Indices32, EditorAssetPayloadPrimitives.WriteUInt32Value);
            writer.WriteArray(modelAsset.Submeshes, WriteModelSubmeshAsset);
        }

        /// <summary>
        /// Reads a model asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized model asset.</returns>
        public Asset Read(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            ModelAsset asset = new ModelAsset();
            EditorAssetPayloadPrimitives.ReadAssetIdentity(reader, asset);
            asset.Positions = reader.ReadArray(EditorAssetPayloadPrimitives.ReadFloat3Value);
            asset.Normals = reader.ReadArray(EditorAssetPayloadPrimitives.ReadFloat3Value);
            asset.TexCoords = reader.ReadArray(EditorAssetPayloadPrimitives.ReadFloat2Value);
            asset.Indices16 = reader.ReadArray(EditorAssetPayloadPrimitives.ReadUInt16Value);
            asset.Indices32 = reader.ReadArray(EditorAssetPayloadPrimitives.ReadUInt32Value);
            asset.Submeshes = reader.ReadArray(ReadModelSubmeshAsset);
            return asset;
        }

        /// <summary>
        /// Writes one model submesh payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="submesh">Model submesh to serialize.</param>
        static void WriteModelSubmeshAsset(EngineBinaryWriter writer, ModelSubmeshAsset submesh) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (submesh == null) {
                throw new ArgumentNullException(nameof(submesh));
            }

            writer.WriteString(submesh.MaterialSlotName ?? string.Empty);
            writer.WriteInt32(submesh.IndexStart);
            writer.WriteInt32(submesh.IndexCount);
        }

        /// <summary>
        /// Reads one model submesh payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized model submesh.</returns>
        static ModelSubmeshAsset ReadModelSubmeshAsset(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new ModelSubmeshAsset {
                MaterialSlotName = reader.ReadString(),
                IndexStart = reader.ReadInt32(),
                IndexCount = reader.ReadInt32()
            };
        }
    }
}

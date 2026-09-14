using helengine;

namespace helengine.files {
    /// <summary>
    /// Serializes the payload body of an authored material asset for the editor asset format, including its shadow flags and render state.
    /// </summary>
    public sealed class MaterialAssetPayloadSerializer : IEditorAssetPayloadSerializer {
        /// <summary>
        /// Gets the value kind stamped into the header for material asset payloads.
        /// </summary>
        public EditorAssetBinaryValueKind ValueKind {
            get {
                return EditorAssetBinaryValueKind.MaterialAsset;
            }
        }

        /// <summary>
        /// Reports whether the supplied asset is a material asset, which deliberately also claims derived material types such as shader materials so they keep resolving to the layout they have always been stored with.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        /// <returns>True when the asset is a material asset.</returns>
        public bool Handles(Asset asset) {
            return asset is MaterialAsset;
        }

        /// <summary>
        /// Performs no validation because a material payload carries no cross-record invariants that must hold before the header is written.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        public void Validate(Asset asset) {
        }

        /// <summary>
        /// Writes a material asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Material asset to serialize.</param>
        public void Write(EngineBinaryWriter writer, Asset asset) {
            MaterialAsset materialAsset = (MaterialAsset)asset;
            EditorAssetPayloadPrimitives.EnsureRuntimeAssetIdentity(materialAsset);
            EditorAssetPayloadPrimitives.WriteAssetIdentity(writer, materialAsset);
            writer.WriteByte(materialAsset.CastsShadows ? (byte)1 : (byte)0);
            writer.WriteByte(materialAsset.ReceivesShadows ? (byte)1 : (byte)0);
            WriteMaterialRenderState(writer, materialAsset.RenderState);
        }

        /// <summary>
        /// Reads a material asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized material asset.</returns>
        public Asset Read(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            MaterialAsset materialAsset = new MaterialAsset();
            EditorAssetPayloadPrimitives.ReadAssetIdentity(reader, materialAsset);
            materialAsset.CastsShadows = reader.ReadByte() != 0;
            materialAsset.ReceivesShadows = reader.ReadByte() != 0;
            materialAsset.RenderState = ReadMaterialRenderState(reader);
            return materialAsset;
        }

        /// <summary>
        /// Writes one material render-state payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="renderState">Render state to serialize.</param>
        static void WriteMaterialRenderState(EngineBinaryWriter writer, MaterialRenderState renderState) {
            if (renderState == null) {
                throw new ArgumentNullException(nameof(renderState));
            }

            writer.WriteInt32((int)renderState.BlendMode);
            writer.WriteInt32((int)renderState.CullMode);
            writer.WriteByte(renderState.DepthTestEnabled ? (byte)1 : (byte)0);
            writer.WriteByte(renderState.DepthWriteEnabled ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// Reads one material render-state payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized material render-state.</returns>
        static MaterialRenderState ReadMaterialRenderState(EngineBinaryReader reader) {
            return new MaterialRenderState {
                BlendMode = (MaterialBlendMode)reader.ReadInt32(),
                CullMode = (MaterialCullMode)reader.ReadInt32(),
                DepthTestEnabled = reader.ReadByte() != 0,
                DepthWriteEnabled = reader.ReadByte() != 0
            };
        }
    }
}

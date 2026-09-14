using helengine;

namespace helengine.files {
    /// <summary>
    /// Serializes the payload body of a platform-owned cooked material asset for the editor asset format.
    /// </summary>
    public sealed class PlatformMaterialAssetPayloadSerializer : IEditorAssetPayloadSerializer {
        /// <summary>
        /// Gets the value kind stamped into the header for platform-owned cooked material payloads.
        /// </summary>
        public EditorAssetBinaryValueKind ValueKind {
            get {
                return EditorAssetBinaryValueKind.PlatformMaterialAsset;
            }
        }

        /// <summary>
        /// Reports whether the supplied asset is a platform-owned cooked material asset.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        /// <returns>True when the asset is a platform-owned cooked material asset.</returns>
        public bool Handles(Asset asset) {
            return asset is PlatformMaterialAsset;
        }

        /// <summary>
        /// Performs no validation because a platform material payload carries no cross-record invariants that must hold before the header is written.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        public void Validate(Asset asset) {
        }

        /// <summary>
        /// Writes a generic platform-owned cooked material payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Platform-owned cooked material asset to serialize.</param>
        public void Write(EngineBinaryWriter writer, Asset asset) {
            PlatformMaterialAsset platformMaterialAsset = (PlatformMaterialAsset)asset;
            EditorAssetPayloadPrimitives.EnsureRuntimeAssetIdentity(platformMaterialAsset);
            EditorAssetPayloadPrimitives.WriteAssetIdentity(writer, platformMaterialAsset);
            writer.WriteString(platformMaterialAsset.RendererFamilyId);
            writer.WriteString(platformMaterialAsset.TextureRelativePath);
            writer.WriteByte(platformMaterialAsset.DoubleSided ? (byte)1 : (byte)0);
            writer.WriteByte(platformMaterialAsset.UseVertexColor ? (byte)1 : (byte)0);
            writer.WriteByte(platformMaterialAsset.Lit ? (byte)1 : (byte)0);
            writer.WriteByte(platformMaterialAsset.BaseColorR);
            writer.WriteByte(platformMaterialAsset.BaseColorG);
            writer.WriteByte(platformMaterialAsset.BaseColorB);
            writer.WriteByte(platformMaterialAsset.BaseColorA);
        }

        /// <summary>
        /// Reads a generic platform-owned cooked material payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized platform-owned cooked material asset.</returns>
        public Asset Read(EngineBinaryReader reader) {
            PlatformMaterialAsset asset = new PlatformMaterialAsset();
            EditorAssetPayloadPrimitives.ReadAssetIdentity(reader, asset);
            asset.RendererFamilyId = reader.ReadString();
            asset.TextureRelativePath = reader.ReadString();
            asset.DoubleSided = reader.ReadByte() != 0;
            asset.UseVertexColor = reader.ReadByte() != 0;
            asset.Lit = reader.ReadByte() != 0;
            asset.BaseColorR = reader.ReadByte();
            asset.BaseColorG = reader.ReadByte();
            asset.BaseColorB = reader.ReadByte();
            asset.BaseColorA = reader.ReadByte();
            return asset;
        }
    }
}

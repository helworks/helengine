using helengine;

namespace helengine.files {
    /// <summary>
    /// Serializes the payload body of a text asset for the editor asset format.
    /// </summary>
    public sealed class TextAssetPayloadSerializer : IEditorAssetPayloadSerializer {
        /// <summary>
        /// Gets the value kind stamped into the header for text asset payloads.
        /// </summary>
        public EditorAssetBinaryValueKind ValueKind {
            get {
                return EditorAssetBinaryValueKind.TextAsset;
            }
        }

        /// <summary>
        /// Reports whether the supplied asset is a text asset.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        /// <returns>True when the asset is a text asset.</returns>
        public bool Handles(Asset asset) {
            return asset is TextAsset;
        }

        /// <summary>
        /// Performs no validation because a text payload carries no cross-record invariants that must hold before the header is written.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        public void Validate(Asset asset) {
        }

        /// <summary>
        /// Writes a text asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Text asset to serialize.</param>
        public void Write(EngineBinaryWriter writer, Asset asset) {
            TextAsset textAsset = (TextAsset)asset;
            EditorAssetPayloadPrimitives.EnsureRuntimeAssetIdentity(textAsset);
            EditorAssetPayloadPrimitives.WriteAssetIdentity(writer, textAsset);
            writer.WriteString(textAsset.Text);
        }

        /// <summary>
        /// Reads a text asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized text asset.</returns>
        public Asset Read(EngineBinaryReader reader) {
            TextAsset asset = new TextAsset();
            EditorAssetPayloadPrimitives.ReadAssetIdentity(reader, asset);
            asset.Text = reader.ReadString();
            return asset;
        }
    }
}

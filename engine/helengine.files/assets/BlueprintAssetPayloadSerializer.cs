using helengine;

namespace helengine.files {
    /// <summary>
    /// Serializes the payload body of a blueprint asset for the editor asset format: its single root entity tree and its asset-reference table.
    /// </summary>
    public sealed class BlueprintAssetPayloadSerializer : IEditorAssetPayloadSerializer {
        /// <summary>
        /// Gets the value kind stamped into the header for blueprint asset payloads.
        /// </summary>
        public EditorAssetBinaryValueKind ValueKind {
            get {
                return EditorAssetBinaryValueKind.BlueprintAsset;
            }
        }

        /// <summary>
        /// Reports whether the supplied asset is a blueprint asset.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        /// <returns>True when the asset is a blueprint asset.</returns>
        public bool Handles(Asset asset) {
            return asset is BlueprintAsset;
        }

        /// <summary>
        /// Rejects duplicate platform override scopes anywhere in the blueprint's entity tree, before the header is written, because two overrides claiming the same scope cannot be written back in a deterministic order.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        public void Validate(Asset asset) {
            BlueprintAsset blueprintAsset = (BlueprintAsset)asset;
            SceneEntityPayloadFormat.ValidateDeterministicSceneEntityOverrides(blueprintAsset.RootEntity);
        }

        /// <summary>
        /// Writes a blueprint asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Blueprint asset to serialize.</param>
        public void Write(EngineBinaryWriter writer, Asset asset) {
            BlueprintAsset blueprintAsset = (BlueprintAsset)asset;
            if (blueprintAsset.RootEntity == null) {
                throw new InvalidOperationException("Blueprint assets must define exactly one root entity.");
            }

            EditorAssetPayloadPrimitives.EnsureRuntimeAssetIdentity(blueprintAsset);
            EditorAssetPayloadPrimitives.WriteAssetIdentity(writer, blueprintAsset);
            SceneEntityPayloadFormat.WriteSceneEntityAsset(writer, blueprintAsset.RootEntity);
            writer.WriteArray(
                SceneEntityPayloadFormat.SortSceneAssetReferences(blueprintAsset.AssetReferences),
                SceneEntityPayloadFormat.WriteSceneAssetReference);
        }

        /// <summary>
        /// Reads a blueprint asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized blueprint asset.</returns>
        public Asset Read(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            BlueprintAsset asset = new BlueprintAsset();
            EditorAssetPayloadPrimitives.ReadAssetIdentity(reader, asset);
            asset.RootEntity = SceneEntityPayloadFormat.ReadSceneEntityAsset(reader);
            if (asset.RootEntity == null) {
                throw new InvalidOperationException("Blueprint assets must define exactly one root entity.");
            }

            asset.AssetReferences = SceneEntityPayloadFormat.ReadSceneAssetReferenceArray(reader) ?? Array.Empty<SceneAssetReference>();
            return asset;
        }
    }
}

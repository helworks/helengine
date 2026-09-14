using helengine;

namespace helengine.files {
    /// <summary>
    /// Serializes the payload body of a scene asset for the editor asset format: its root entity tree, its asset-reference table, its physics feature flags and its scene settings.
    /// </summary>
    public sealed class SceneAssetPayloadSerializer : IEditorAssetPayloadSerializer {
        /// <summary>
        /// Gets the value kind stamped into the header for scene asset payloads.
        /// </summary>
        public EditorAssetBinaryValueKind ValueKind {
            get {
                return EditorAssetBinaryValueKind.SceneAsset;
            }
        }

        /// <summary>
        /// Reports whether the supplied asset is a scene asset.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        /// <returns>True when the asset is a scene asset.</returns>
        public bool Handles(Asset asset) {
            return asset is SceneAsset;
        }

        /// <summary>
        /// Rejects duplicate platform override scopes anywhere in the scene's entity tree, before the header is written, because two overrides claiming the same scope cannot be written back in a deterministic order.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        public void Validate(Asset asset) {
            SceneAsset sceneAsset = (SceneAsset)asset;
            for (int index = 0; index < (sceneAsset.RootEntities?.Length ?? 0); index++) {
                SceneEntityPayloadFormat.ValidateDeterministicSceneEntityOverrides(sceneAsset.RootEntities[index]);
            }
        }

        /// <summary>
        /// Writes a scene asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Scene asset to serialize.</param>
        public void Write(EngineBinaryWriter writer, Asset asset) {
            SceneAsset sceneAsset = (SceneAsset)asset;
            EditorAssetPayloadPrimitives.EnsureRuntimeAssetIdentity(sceneAsset);
            EditorAssetPayloadPrimitives.WriteAssetIdentity(writer, sceneAsset);
            writer.WriteArray(sceneAsset.RootEntities, SceneEntityPayloadFormat.WriteSceneEntityAsset);
            writer.WriteArray(
                SceneEntityPayloadFormat.SortSceneAssetReferences(sceneAsset.AssetReferences),
                SceneEntityPayloadFormat.WriteSceneAssetReference);
            writer.WriteUInt32(sceneAsset.Physics3DSceneFeatureFlags);
            SceneEntityPayloadFormat.WriteSceneSettingsAsset(writer, sceneAsset.SceneSettings);
        }

        /// <summary>
        /// Reads a scene asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized scene asset.</returns>
        public Asset Read(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            SceneAsset asset = new SceneAsset();
            EditorAssetPayloadPrimitives.ReadAssetIdentity(reader, asset);
            asset.RootEntities = SceneEntityPayloadFormat.ReadSceneEntityAssetArray(reader) ?? Array.Empty<SceneEntityAsset>();
            asset.AssetReferences = SceneEntityPayloadFormat.ReadSceneAssetReferenceArray(reader) ?? Array.Empty<SceneAssetReference>();
            asset.Physics3DSceneFeatureFlags = reader.ReadUInt32();
            asset.SceneSettings = SceneEntityPayloadFormat.ReadSceneSettingsAsset(reader);
            return asset;
        }
    }
}

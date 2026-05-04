namespace helengine {
    /// <summary>
    /// Represents one serialized editor scene stored as a HELE asset.
    /// </summary>
    public class SceneAsset : Asset {
        /// <summary>
        /// File extension used for serialized editor scenes.
        /// </summary>
        public const string FileExtension = ".helen";

        /// <summary>
        /// Gets or sets the serialized root entities stored in the scene.
        /// </summary>
        public SceneEntityAsset[] RootEntities { get; set; } = Array.Empty<SceneEntityAsset>();

        /// <summary>
        /// Gets or sets the stable asset references required by the scene.
        /// </summary>
        public SceneAssetReference[] AssetReferences { get; set; } = Array.Empty<SceneAssetReference>();

        /// <summary>
        /// Gets or sets the raw packaged 3D physics feature bitmask inferred from the serialized scene contents.
        /// </summary>
        public uint Physics3DSceneFeatureFlags { get; set; }
    }
}

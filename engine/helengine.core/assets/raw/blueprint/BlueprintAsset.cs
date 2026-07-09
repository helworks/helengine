namespace helengine {
    /// <summary>
    /// Represents one serialized blueprint asset stored as a reusable single-root authored hierarchy.
    /// </summary>
    public class BlueprintAsset : Asset {
        /// <summary>
        /// File extension used for serialized blueprint assets.
        /// </summary>
        public const string FileExtension = ".hblueprint";

        /// <summary>
        /// Gets or sets the single serialized root entity stored in the blueprint asset.
        /// </summary>
        public SceneEntityAsset RootEntity { get; set; }

        /// <summary>
        /// Gets or sets the stable asset references required by the blueprint.
        /// </summary>
        public SceneAssetReference[] AssetReferences { get; set; } = Array.Empty<SceneAssetReference>();
    }
}

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
    }
}

namespace helengine {
    /// <summary>
    /// Distinguishes the backing source used to resolve a serialized scene asset reference.
    /// </summary>
    public enum SceneAssetReferenceSourceKind {
        /// <summary>
        /// The reference points at a file stored under the project assets folder.
        /// </summary>
        FileSystem = 1,

        /// <summary>
        /// The reference points at a generated asset exposed by a provider.
        /// </summary>
        Generated = 2
    }
}

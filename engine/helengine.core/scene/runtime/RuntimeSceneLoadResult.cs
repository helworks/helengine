namespace helengine {
    /// <summary>
    /// Stores the runtime entities and scene-owned runtime assets materialized from one packaged scene load.
    /// </summary>
    public sealed class RuntimeSceneLoadResult {
        /// <summary>
        /// Initializes one scene-load result.
        /// </summary>
        /// <param name="rootEntities">Root entities materialized from the packaged scene asset.</param>
        /// <param name="ownedAssets">Scene-owned runtime assets resolved during materialization.</param>
        public RuntimeSceneLoadResult(IReadOnlyList<Entity> rootEntities, RuntimeSceneOwnedAssetSet ownedAssets) {
            RootEntities = rootEntities ?? throw new ArgumentNullException(nameof(rootEntities));
            OwnedAssets = ownedAssets ?? throw new ArgumentNullException(nameof(ownedAssets));
        }

        /// <summary>
        /// Gets the root entities materialized from the packaged scene asset.
        /// </summary>
        public IReadOnlyList<Entity> RootEntities { get; }

        /// <summary>
        /// Gets the scene-owned runtime assets resolved during materialization.
        /// </summary>
        public RuntimeSceneOwnedAssetSet OwnedAssets { get; }
    }
}

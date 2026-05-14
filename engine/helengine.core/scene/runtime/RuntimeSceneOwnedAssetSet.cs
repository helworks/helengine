namespace helengine {
    /// <summary>
    /// Stores the scene-owned runtime assets materialized from one packaged scene load.
    /// </summary>
    public sealed class RuntimeSceneOwnedAssetSet {
        /// <summary>
        /// Initializes one scene-owned asset set.
        /// </summary>
        /// <param name="ownedTextures">Scene-owned runtime textures resolved during materialization.</param>
        /// <param name="ownedFonts">Scene-owned font assets resolved during materialization.</param>
        public RuntimeSceneOwnedAssetSet(IReadOnlyList<RuntimeTexture> ownedTextures, IReadOnlyList<FontAsset> ownedFonts) {
            OwnedTextures = ownedTextures ?? throw new ArgumentNullException(nameof(ownedTextures));
            OwnedFonts = ownedFonts ?? throw new ArgumentNullException(nameof(ownedFonts));
        }

        /// <summary>
        /// Gets the scene-owned runtime textures resolved during materialization.
        /// </summary>
        public IReadOnlyList<RuntimeTexture> OwnedTextures { get; }

        /// <summary>
        /// Gets the scene-owned font assets resolved during materialization.
        /// </summary>
        public IReadOnlyList<FontAsset> OwnedFonts { get; }
    }
}

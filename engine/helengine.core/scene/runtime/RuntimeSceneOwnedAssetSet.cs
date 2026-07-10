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
        /// <param name="ownedAudio">Scene-owned audio assets resolved during materialization.</param>
        /// <param name="ownedModels">Scene-owned runtime models resolved during materialization.</param>
        /// <param name="ownedMaterials">Scene-owned runtime materials resolved during materialization.</param>
        public RuntimeSceneOwnedAssetSet(
            IReadOnlyList<RuntimeTexture> ownedTextures,
            IReadOnlyList<FontAsset> ownedFonts,
            IReadOnlyList<AudioAsset> ownedAudio,
            IReadOnlyList<RuntimeModel> ownedModels,
            IReadOnlyList<RuntimeMaterial> ownedMaterials) {
            OwnedTextures = ownedTextures ?? throw new ArgumentNullException(nameof(ownedTextures));
            OwnedFonts = ownedFonts ?? throw new ArgumentNullException(nameof(ownedFonts));
            OwnedAudio = ownedAudio ?? throw new ArgumentNullException(nameof(ownedAudio));
            OwnedModels = ownedModels ?? throw new ArgumentNullException(nameof(ownedModels));
            OwnedMaterials = ownedMaterials ?? throw new ArgumentNullException(nameof(ownedMaterials));
        }

        /// <summary>
        /// Gets the scene-owned runtime textures resolved during materialization.
        /// </summary>
        public IReadOnlyList<RuntimeTexture> OwnedTextures { get; }

        /// <summary>
        /// Gets the scene-owned font assets resolved during materialization.
        /// </summary>
        public IReadOnlyList<FontAsset> OwnedFonts { get; }

        /// <summary>
        /// Gets the scene-owned audio assets resolved during materialization.
        /// </summary>
        public IReadOnlyList<AudioAsset> OwnedAudio { get; }

        /// <summary>
        /// Gets the scene-owned runtime models resolved during materialization.
        /// </summary>
        public IReadOnlyList<RuntimeModel> OwnedModels { get; }

        /// <summary>
        /// Gets the scene-owned runtime materials resolved during materialization.
        /// </summary>
        public IReadOnlyList<RuntimeMaterial> OwnedMaterials { get; }
    }
}

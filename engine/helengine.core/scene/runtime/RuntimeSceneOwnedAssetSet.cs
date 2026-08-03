namespace helengine {
    /// <summary>
    /// Stores the scene-owned runtime assets materialized from one packaged scene load.
    /// </summary>
    public sealed class RuntimeSceneOwnedAssetSet : IDisposable {
        /// <summary>
        /// Stores the runtime-texture reference container owned by this set.
        /// </summary>
        [NativeOwnedMember]
        List<RuntimeTexture> OwnedTexturesValue;

        /// <summary>
        /// Stores the font-asset reference container owned by this set.
        /// </summary>
        [NativeOwnedMember]
        List<FontAsset> OwnedFontsValue;

        /// <summary>
        /// Stores the audio-asset reference container owned by this set.
        /// </summary>
        [NativeOwnedMember]
        List<AudioAsset> OwnedAudioValue;

        /// <summary>
        /// Stores the runtime-model reference container owned by this set.
        /// </summary>
        [NativeOwnedMember]
        List<RuntimeModel> OwnedModelsValue;

        /// <summary>
        /// Stores the runtime-material reference container owned by this set.
        /// </summary>
        [NativeOwnedMember]
        List<RuntimeMaterial> OwnedMaterialsValue;

        /// <summary>
        /// Initializes one scene-owned asset set.
        /// </summary>
        /// <param name="ownedTextures">Scene-owned runtime textures resolved during materialization.</param>
        /// <param name="ownedFonts">Scene-owned font assets resolved during materialization.</param>
        /// <param name="ownedAudio">Scene-owned audio assets resolved during materialization.</param>
        /// <param name="ownedModels">Scene-owned runtime models resolved during materialization.</param>
        /// <param name="ownedMaterials">Scene-owned runtime materials resolved during materialization.</param>
        public RuntimeSceneOwnedAssetSet(
            [NativeNoEscape] IReadOnlyList<RuntimeTexture> ownedTextures,
            [NativeNoEscape] IReadOnlyList<FontAsset> ownedFonts,
            [NativeNoEscape] IReadOnlyList<AudioAsset> ownedAudio,
            [NativeNoEscape] IReadOnlyList<RuntimeModel> ownedModels,
            [NativeNoEscape] IReadOnlyList<RuntimeMaterial> ownedMaterials) {
            OwnedTexturesValue = CopyItems(ownedTextures);
            OwnedFontsValue = CopyItems(ownedFonts);
            OwnedAudioValue = CopyItems(ownedAudio);
            OwnedModelsValue = CopyItems(ownedModels);
            OwnedMaterialsValue = CopyItems(ownedMaterials);
        }

        /// <summary>
        /// Initializes one asset set by assuming ownership of already materialized list containers.
        /// </summary>
        /// <param name="ownedTextures">Runtime-texture list whose container transfers to this set.</param>
        /// <param name="ownedFonts">Font-asset list whose container transfers to this set.</param>
        /// <param name="ownedAudio">Audio-asset list whose container transfers to this set.</param>
        /// <param name="ownedModels">Runtime-model list whose container transfers to this set.</param>
        /// <param name="ownedMaterials">Runtime-material list whose container transfers to this set.</param>
        /// <param name="takesOwnership">Required marker confirming that all supplied containers transfer ownership.</param>
        RuntimeSceneOwnedAssetSet(
            [NativeTakesOwnership] List<RuntimeTexture> ownedTextures,
            [NativeTakesOwnership] List<FontAsset> ownedFonts,
            [NativeTakesOwnership] List<AudioAsset> ownedAudio,
            [NativeTakesOwnership] List<RuntimeModel> ownedModels,
            [NativeTakesOwnership] List<RuntimeMaterial> ownedMaterials,
            bool takesOwnership) {
            if (!takesOwnership) {
                throw new ArgumentException("The ownership constructor requires an explicit transfer marker.", nameof(takesOwnership));
            }

            OwnedTexturesValue = ownedTextures ?? throw new ArgumentNullException(nameof(ownedTextures));
            OwnedFontsValue = ownedFonts ?? throw new ArgumentNullException(nameof(ownedFonts));
            OwnedAudioValue = ownedAudio ?? throw new ArgumentNullException(nameof(ownedAudio));
            OwnedModelsValue = ownedModels ?? throw new ArgumentNullException(nameof(ownedModels));
            OwnedMaterialsValue = ownedMaterials ?? throw new ArgumentNullException(nameof(ownedMaterials));
        }

        /// <summary>
        /// Gets the scene-owned runtime textures resolved during materialization.
        /// </summary>
        public IReadOnlyList<RuntimeTexture> OwnedTextures => OwnedTexturesValue;

        /// <summary>
        /// Gets the scene-owned font assets resolved during materialization.
        /// </summary>
        public IReadOnlyList<FontAsset> OwnedFonts => OwnedFontsValue;

        /// <summary>
        /// Gets the scene-owned audio assets resolved during materialization.
        /// </summary>
        public IReadOnlyList<AudioAsset> OwnedAudio => OwnedAudioValue;

        /// <summary>
        /// Gets the scene-owned runtime models resolved during materialization.
        /// </summary>
        public IReadOnlyList<RuntimeModel> OwnedModels => OwnedModelsValue;

        /// <summary>
        /// Gets the scene-owned runtime materials resolved during materialization.
        /// </summary>
        public IReadOnlyList<RuntimeMaterial> OwnedMaterials => OwnedMaterialsValue;

        /// <summary>
        /// Creates an asset set that assumes native ownership of all supplied list containers.
        /// </summary>
        /// <param name="ownedTextures">Runtime-texture list whose container transfers to the result.</param>
        /// <param name="ownedFonts">Font-asset list whose container transfers to the result.</param>
        /// <param name="ownedAudio">Audio-asset list whose container transfers to the result.</param>
        /// <param name="ownedModels">Runtime-model list whose container transfers to the result.</param>
        /// <param name="ownedMaterials">Runtime-material list whose container transfers to the result.</param>
        /// <returns>Asset set responsible for releasing all transferred list containers.</returns>
        [NativeOwnedReturn]
        public static RuntimeSceneOwnedAssetSet CreateOwned(
            [NativeTakesOwnership] List<RuntimeTexture> ownedTextures,
            [NativeTakesOwnership] List<FontAsset> ownedFonts,
            [NativeTakesOwnership] List<AudioAsset> ownedAudio,
            [NativeTakesOwnership] List<RuntimeModel> ownedModels,
            [NativeTakesOwnership] List<RuntimeMaterial> ownedMaterials) {
            return new RuntimeSceneOwnedAssetSet(
                ownedTextures,
                ownedFonts,
                ownedAudio,
                ownedModels,
                ownedMaterials,
                true);
        }

        /// <summary>
        /// Releases every reference-list container owned by this set without deleting the referenced runtime assets.
        /// </summary>
        public void Dispose() {
            NativeOwnership.Release(ref OwnedTexturesValue);
            NativeOwnership.Release(ref OwnedFontsValue);
            NativeOwnership.Release(ref OwnedAudioValue);
            NativeOwnership.Release(ref OwnedModelsValue);
            NativeOwnership.Release(ref OwnedMaterialsValue);
        }

        /// <summary>
        /// Copies one borrowed asset-reference list into a new container owned by the caller.
        /// </summary>
        /// <typeparam name="T">Runtime asset reference type stored in the list.</typeparam>
        /// <param name="source">Borrowed source references to copy.</param>
        /// <returns>New list container holding the same borrowed runtime asset references.</returns>
        static List<T> CopyItems<T>([NativeNoEscape] IReadOnlyList<T> source) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }

            List<T> copy = new List<T>(source.Count);
            for (int index = 0; index < source.Count; index++) {
                copy.Add(source[index]);
            }

            return copy;
        }
    }
}

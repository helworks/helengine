namespace helengine {
    /// <summary>
    /// Resolves packaged file-backed scene asset references into runtime assets for player builds.
    /// </summary>
    public sealed class RuntimeSceneAssetReferenceResolver : IDisposable {
        /// <summary>
        /// Core that owns every runtime asset and entity materialized through this resolver.
        /// </summary>
        readonly Core OwnerCore;
        readonly RenderManager3D RenderManager3D;
        readonly RenderManager2D RenderManager2D;

        /// <summary>
        /// Gets the core that owns this resolver's runtime assets.
        /// </summary>
        internal Core OwningCore => OwnerCore;
        /// <summary>
        /// Content manager used to load packaged runtime assets.
        /// </summary>
        readonly ContentManager AssetContentManager;

        /// <summary>
        /// Tracks scene-owned runtime textures resolved during the active scene materialization scope.
        /// </summary>
        [NativeOwnedMember]
        List<RuntimeTexture> ActiveOwnedTextures;

        /// <summary>
        /// Tracks scene-owned font assets resolved during the active scene materialization scope.
        /// </summary>
        [NativeOwnedMember]
        List<FontAsset> ActiveOwnedFonts;

        /// <summary>
        /// Tracks scene-owned audio assets resolved during the active scene materialization scope.
        /// </summary>
        [NativeOwnedMember]
        List<AudioAsset> ActiveOwnedAudio;

        /// <summary>
        /// Reuses packaged font assets resolved by absolute path during the active scene materialization scope.
        /// </summary>
        [NativeOwnedMember]
        Dictionary<string, FontAsset> ActiveResolvedFontsByPath;

        /// <summary>
        /// Tracks scene-owned runtime models resolved during the active scene materialization scope.
        /// </summary>
        [NativeOwnedMember]
        List<RuntimeModel> ActiveOwnedModels;

        /// <summary>
        /// Tracks scene-owned runtime materials resolved during the active scene materialization scope.
        /// </summary>
        [NativeOwnedMember]
        List<RuntimeMaterial> ActiveOwnedMaterials;

        /// <summary>
        /// Reuses generated runtime models during the active scene materialization scope so repeated generated references share one runtime model instance.
        /// </summary>
        [NativeOwnedMember]
        Dictionary<string, RuntimeModel> ActiveGeneratedModelsByKey;

        /// <summary>
        /// Reuses generated runtime materials during the active scene materialization scope so repeated generated references share one runtime material instance.
        /// </summary>
        [NativeOwnedMember]
        Dictionary<string, RuntimeMaterial> ActiveGeneratedMaterialsByKey;

        /// <summary>
        /// Gets the last recorded text-load stage that passed through this resolver.
        /// </summary>
        public string LastTextLoadStage { get; set; } = string.Empty;

        /// <summary>
        /// Gets the last recorded packaged font relative path that passed through this resolver.
        /// </summary>
        public string LastTextFontRelativePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets the last recorded texture-load stage that passed through this resolver.
        /// </summary>
        public string LastTextureLoadStage { get; set; } = string.Empty;

        /// <summary>
        /// Gets the last recorded packaged texture relative path that passed through this resolver.
        /// </summary>
        public string LastTextureRelativePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets the most recent packaged font-deserialization stage reached by the active content loader.
        /// </summary>
        public string LastFontDeserializeStage => FontAssetBinarySerializer.LastDeserializeStage;

        /// <summary>
        /// Initializes a new packaged scene asset resolver.
        /// </summary>
        /// <param name="assetContentManager">Content manager used to load packaged assets.</param>
        public RuntimeSceneAssetReferenceResolver(Core ownerCore, ContentManager assetContentManager) {
            OwnerCore = ownerCore ?? throw new ArgumentNullException(nameof(ownerCore));
            if (assetContentManager == null) {
                throw new ArgumentNullException(nameof(assetContentManager));
            }
            // Headless cores are valid composition roots.  Keep the resolver bound to
            // the explicit owner and validate a renderer only when that asset kind is used.
            RenderManager3D = ownerCore.RenderManager3D;
            RenderManager2D = ownerCore.RenderManager2D;

            AssetContentManager = assetContentManager;
            ActiveGeneratedModelsByKey = new Dictionary<string, RuntimeModel>(StringComparer.Ordinal);
            ActiveGeneratedMaterialsByKey = new Dictionary<string, RuntimeMaterial>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Resolves one packaged model reference into a runtime model instance.
        /// </summary>
        /// <param name="reference">Packaged scene asset reference to resolve.</param>
        /// <returns>A scene-owned runtime model borrowed by the materialized component.</returns>
        [NativeBorrowedReturn]
        public RuntimeModel ResolveModel(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }
            RenderManager3D renderer = RequireRenderManager3D();

            if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                string generatedAssetKey = BuildGeneratedAssetCacheKey(reference);
                if (ActiveGeneratedModelsByKey.TryGetValue(generatedAssetKey, out RuntimeModel generatedRuntimeModel)) {
                    return generatedRuntimeModel;
                }

                string generatedFullPath = ResolveFileBackedAssetPath(reference);
#if HELENGINE_RUNTIME_MODEL_RESOLUTION_COOKED_PLATFORM_OWNED
                RuntimeModel generatedModel = TrackOwnedModel(
                    renderer.BuildModelFromCooked(generatedFullPath, AssetContentManager.ContentStreamSource));
                ActiveGeneratedModelsByKey.Add(generatedAssetKey, generatedModel);
                return generatedModel;
#else
                ModelAsset generatedModelAsset = AssetContentManager.Load<ModelAsset>(generatedFullPath, RuntimeContentProcessorIds.ModelAsset);
                try {
                    RuntimeModel generatedModel = TrackOwnedModel(renderer.BuildModelFromRaw(generatedModelAsset));
                    ActiveGeneratedModelsByKey.Add(generatedAssetKey, generatedModel);
                    return generatedModel;
                } finally {
                    ReleaseTransientModelAsset(generatedModelAsset);
                }
#endif
            }

            string fullPath = ResolveFileBackedAssetPath(reference);
#if HELENGINE_RUNTIME_MODEL_RESOLUTION_COOKED_PLATFORM_OWNED
            RuntimeModel runtimeModel = renderer.BuildModelFromCooked(fullPath, AssetContentManager.ContentStreamSource);
            return TrackOwnedModel(runtimeModel);
#else
            ModelAsset modelAsset = AssetContentManager.Load<ModelAsset>(fullPath, RuntimeContentProcessorIds.ModelAsset);
            try {
                RuntimeModel runtimeModel = renderer.BuildModelFromRaw(modelAsset);
                return TrackOwnedModel(runtimeModel);
            } finally {
                ReleaseTransientModelAsset(modelAsset);
            }
#endif
        }

        /// <summary>
        /// Resolves one packaged material reference into a runtime material instance.
        /// </summary>
        /// <param name="reference">Packaged scene asset reference to resolve.</param>
        /// <returns>A scene-owned runtime material borrowed by the materialized component.</returns>
        [NativeBorrowedReturn]
        public RuntimeMaterial ResolveMaterial(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }
            RenderManager3D renderer = RequireRenderManager3D();

            if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                string generatedAssetKey = BuildGeneratedAssetCacheKey(reference);
                if (ActiveGeneratedMaterialsByKey.TryGetValue(generatedAssetKey, out RuntimeMaterial generatedRuntimeMaterial)) {
                    return generatedRuntimeMaterial;
                }

                string generatedFullPath = ResolveFileBackedAssetPath(reference);
#if HELENGINE_RUNTIME_MATERIAL_RESOLUTION_COOKED_PLATFORM_OWNED
                RuntimeMaterial generatedCookedRuntimeMaterial = TrackOwnedMaterial(
                    renderer.BuildMaterialFromCooked(generatedFullPath, AssetContentManager.ContentStreamSource));
                ActiveGeneratedMaterialsByKey.Add(generatedAssetKey, generatedCookedRuntimeMaterial);
                return generatedCookedRuntimeMaterial;
#else
                RuntimeMaterial generatedRawRuntimeMaterial = TrackOwnedMaterial(
                    renderer.BuildMaterialFromRawAsset(
                        AssetContentManager,
                        generatedFullPath));
                ActiveGeneratedMaterialsByKey.Add(generatedAssetKey, generatedRawRuntimeMaterial);
                return generatedRawRuntimeMaterial;
#endif
            }

            string fullPath = ResolveFileBackedAssetPath(reference);
#if HELENGINE_RUNTIME_MATERIAL_RESOLUTION_COOKED_PLATFORM_OWNED
            RuntimeMaterial runtimeMaterial = renderer.BuildMaterialFromCooked(fullPath, AssetContentManager.ContentStreamSource);
            return TrackOwnedMaterial(runtimeMaterial);
#else
            RuntimeMaterial runtimeMaterial = renderer.BuildMaterialFromRawAsset(
                AssetContentManager,
                fullPath);
            return TrackOwnedMaterial(runtimeMaterial);
#endif
        }

        /// <summary>
        /// Resolves one packaged font reference into a runtime font asset instance.
        /// </summary>
        /// <param name="reference">Packaged scene asset reference to resolve.</param>
        /// <returns>A scene-owned font asset borrowed by the materialized component.</returns>
        [NativeBorrowedReturn]
        public FontAsset ResolveFont(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            LastTextLoadStage = "ResolveFontBegin";
            LastTextFontRelativePath = reference.RelativePath ?? string.Empty;
            string fullPath = ResolveFileBackedAssetPath(reference);
            if (ActiveResolvedFontsByPath != null) {
                if (ActiveResolvedFontsByPath.TryGetValue(fullPath, out FontAsset cachedFontAsset)) {
                    LastTextLoadStage = "ResolveFontFromCache";
                    return cachedFontAsset;
                }
            }

            LastTextLoadStage = "ResolveFontBeforeContentLoad";
            FontAsset fontAsset = AssetContentManager.Load<FontAsset>(fullPath, RuntimeContentProcessorIds.FontAsset);
            AttachExternalCookedFontAtlasIfPresent(fontAsset);
            LastTextLoadStage = "ResolveFontAfterContentLoad";
            if (ActiveResolvedFontsByPath != null) {
                ActiveResolvedFontsByPath.Add(fullPath, fontAsset);
            }
            return TrackOwnedFont(fontAsset);
        }

        /// <summary>
        /// Resolves one packaged texture reference into a runtime texture instance.
        /// </summary>
        /// <param name="reference">Packaged scene asset reference to resolve.</param>
        /// <returns>A scene-owned runtime texture borrowed by the materialized component.</returns>
        [NativeBorrowedReturn]
        public RuntimeTexture ResolveTexture(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }
            RenderManager2D renderer = RequireRenderManager2D();

            LastTextureLoadStage = "ResolveTextureBegin";
            LastTextureRelativePath = reference.RelativePath ?? string.Empty;
            string fullPath = ResolveFileBackedAssetPath(reference);
#if HELENGINE_RUNTIME_TEXTURE_RESOLUTION_COOKED_PLATFORM_OWNED
            LastTextureLoadStage = "ResolveTextureBeforeBuild";
            RuntimeTexture runtimeTexture = renderer.BuildTextureFromCooked(fullPath, AssetContentManager.ContentStreamSource);
            OwnerCore.ReportSceneTransitionStage("Ownership:ResolveTextureAfterBuild");
            LastTextureLoadStage = "ResolveTextureAfterBuild";
            RuntimeTexture trackedRuntimeTexture = TrackOwnedTexture(runtimeTexture);
            OwnerCore.ReportSceneTransitionStage("Ownership:ResolveTextureAfterTrack");
            LastTextureLoadStage = "ResolveTextureTracked";
            OwnerCore.ReportSceneTransitionStage("Ownership:ResolveTextureBeforeReturn");
            return trackedRuntimeTexture;
#else
            LastTextureLoadStage = "ResolveTextureBeforeContentLoad";
            TextureAsset textureAsset = AssetContentManager.Load<TextureAsset>(fullPath, RuntimeContentProcessorIds.TextureAsset);
            try {
                LastTextureLoadStage = "ResolveTextureBeforeBuild";
                RuntimeTexture runtimeTexture = renderer.BuildTextureFromRaw(textureAsset);
                LastTextureLoadStage = "ResolveTextureAfterBuild";
                RuntimeTexture trackedRuntimeTexture = TrackOwnedTexture(runtimeTexture);
                LastTextureLoadStage = "ResolveTextureTracked";
                return trackedRuntimeTexture;
            } finally {
                ReleaseTransientTextureAsset(textureAsset);
            }
#endif
        }

        /// <summary>
        /// Resolves one packaged animation-clip reference into an animation clip asset instance.
        /// </summary>
        /// <param name="reference">Packaged scene asset reference to resolve.</param>
        /// <returns>Animation clip asset loaded from packaged content.</returns>
        public AnimationClipAsset ResolveAnimationClip(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            string fullPath = ResolveFileBackedAssetPath(reference);
            return AssetContentManager.Load<AnimationClipAsset>(fullPath, RuntimeContentProcessorIds.AnimationClipAsset);
        }

        /// <summary>
        /// Resolves one packaged audio reference into an audio asset instance.
        /// </summary>
        /// <param name="reference">Packaged scene asset reference to resolve.</param>
        /// <returns>A scene-owned audio asset borrowed by the materialized component.</returns>
        [NativeBorrowedReturn]
        public AudioAsset ResolveAudio(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            string fullPath = ResolveFileBackedAssetPath(reference);
            AudioAsset audioAsset = AssetContentManager.Load<AudioAsset>(fullPath, RuntimeContentProcessorIds.AudioAsset);
            return TrackOwnedAudio(audioAsset);
        }

        /// <summary>
        /// Starts one scene-owned asset tracking scope for the next packaged scene materialization.
        /// </summary>
        public void BeginOwnedAssetTracking() {
            if (ActiveOwnedTextures != null ||
                ActiveOwnedFonts != null ||
                ActiveOwnedAudio != null ||
                ActiveResolvedFontsByPath != null ||
                ActiveOwnedModels != null ||
                ActiveOwnedMaterials != null) {
                throw new InvalidOperationException("Runtime scene asset tracking is already active.");
            }

            ResetGeneratedRuntimeAssetCaches();
            NativeOwnership.Release(ref ActiveOwnedTextures);
            NativeOwnership.Release(ref ActiveOwnedFonts);
            NativeOwnership.Release(ref ActiveOwnedAudio);
            NativeOwnership.Release(ref ActiveResolvedFontsByPath);
            NativeOwnership.Release(ref ActiveOwnedModels);
            NativeOwnership.Release(ref ActiveOwnedMaterials);
            ActiveOwnedTextures = new List<RuntimeTexture>();
            ActiveOwnedFonts = new List<FontAsset>();
            ActiveOwnedAudio = new List<AudioAsset>();
            ActiveResolvedFontsByPath = new Dictionary<string, FontAsset>(StringComparer.OrdinalIgnoreCase);
            ActiveOwnedModels = new List<RuntimeModel>();
            ActiveOwnedMaterials = new List<RuntimeMaterial>();
        }

        /// <summary>
        /// Completes the active scene-owned asset tracking scope and returns the resolved assets.
        /// </summary>
        /// <returns>Scene-owned runtime assets resolved during the active materialization scope.</returns>
        public RuntimeSceneOwnedAssetSet CompleteOwnedAssetTracking() {
            if (ActiveOwnedTextures == null || ActiveOwnedFonts == null || ActiveOwnedAudio == null || ActiveOwnedModels == null || ActiveOwnedMaterials == null) {
                throw new InvalidOperationException("Runtime scene asset tracking is not active.");
            }

            List<RuntimeTexture> ownedTextures = new List<RuntimeTexture>(ActiveOwnedTextures);
            List<FontAsset> ownedFonts = new List<FontAsset>(ActiveOwnedFonts);
            List<AudioAsset> ownedAudio = new List<AudioAsset>(ActiveOwnedAudio);
            List<RuntimeModel> ownedModels = new List<RuntimeModel>(ActiveOwnedModels);
            List<RuntimeMaterial> ownedMaterials = new List<RuntimeMaterial>(ActiveOwnedMaterials);
            NativeOwnership.Release(ref ActiveOwnedTextures);
            NativeOwnership.Release(ref ActiveOwnedFonts);
            NativeOwnership.Release(ref ActiveOwnedAudio);
            NativeOwnership.Release(ref ActiveResolvedFontsByPath);
            NativeOwnership.Release(ref ActiveOwnedModels);
            NativeOwnership.Release(ref ActiveOwnedMaterials);
            ResetGeneratedRuntimeAssetCaches();
            return RuntimeSceneOwnedAssetSet.CreateOwned(ownedTextures, ownedFonts, ownedAudio, ownedModels, ownedMaterials);
        }

        /// <summary>
        /// Cancels the active scene-owned asset tracking scope after one failed materialization attempt.
        /// </summary>
        public void CancelOwnedAssetTracking() {
            NativeOwnership.Release(ref ActiveOwnedTextures);
            NativeOwnership.Release(ref ActiveOwnedFonts);
            NativeOwnership.Release(ref ActiveOwnedAudio);
            NativeOwnership.Release(ref ActiveResolvedFontsByPath);
            NativeOwnership.Release(ref ActiveOwnedModels);
            NativeOwnership.Release(ref ActiveOwnedMaterials);
            ResetGeneratedRuntimeAssetCaches();
        }

        /// <summary>
        /// Releases active tracking containers and generated-reference caches owned by this resolver.
        /// </summary>
        public void Dispose() {
            NativeOwnership.Release(ref ActiveOwnedTextures);
            NativeOwnership.Release(ref ActiveOwnedFonts);
            NativeOwnership.Release(ref ActiveOwnedAudio);
            NativeOwnership.Release(ref ActiveResolvedFontsByPath);
            NativeOwnership.Release(ref ActiveOwnedModels);
            NativeOwnership.Release(ref ActiveOwnedMaterials);
            NativeOwnership.Release(ref ActiveGeneratedModelsByKey);
            NativeOwnership.Release(ref ActiveGeneratedMaterialsByKey);
        }

        /// <summary>
        /// Clears the per-load generated runtime asset caches so generated references participate in normal scene ownership across transitions.
        /// </summary>
        void ResetGeneratedRuntimeAssetCaches() {
            ActiveGeneratedModelsByKey.Clear();
            ActiveGeneratedMaterialsByKey.Clear();
        }

        /// <summary>
        /// Releases one transient texture asset that exists only long enough to build a runtime texture.
        /// </summary>
        /// <param name="asset">Transient texture asset to release.</param>
        static void ReleaseTransientTextureAsset(TextureAsset asset) {
            if (asset == null) {
                return;
            }

            NativeOwnership.DisposeAndDelete(asset);
        }

        /// <summary>
        /// Releases one transient model asset and all deserialized mesh buffers used only during runtime-model construction.
        /// </summary>
        /// <param name="asset">Transient model asset to release.</param>
        static void ReleaseTransientModelAsset(ModelAsset asset) {
            if (asset == null) {
                return;
            }

            NativeOwnership.DisposeAndDelete(asset);
        }

        /// <summary>
        /// Releases one transient audio asset and all deserialized payload buffers used only while the scene remains loaded.
        /// </summary>
        /// <param name="asset">Transient audio asset to release.</param>
        internal static void ReleaseTransientAudioAsset(AudioAsset asset) {
            if (asset == null) {
                return;
            }

            byte[] encodedBytes = asset.EncodedBytes;
            AudioChunkDescriptor[] chunks = asset.Chunks;
            AudioAssetPlatformOverrideAsset[] platformOverrides = asset.PlatformOverrides;
            asset.EncodedBytes = null;
            asset.Chunks = null;
            asset.PlatformOverrides = null;
            if (chunks != null) {
                for (int index = 0; index < chunks.Length; index++) {
                    NativeOwnership.Delete(chunks[index]);
                }
            }
            if (platformOverrides != null) {
                for (int index = 0; index < platformOverrides.Length; index++) {
                    AudioAssetPlatformOverrideAsset platformOverride = platformOverrides[index];
                    if (platformOverride == null) {
                        continue;
                    }

                    byte[] overrideEncodedBytes = platformOverride.EncodedBytes;
                    AudioChunkDescriptor[] overrideChunks = platformOverride.Chunks;
                    platformOverride.EncodedBytes = null;
                    platformOverride.Chunks = null;
                    if (overrideChunks != null) {
                        for (int chunkIndex = 0; chunkIndex < overrideChunks.Length; chunkIndex++) {
                            NativeOwnership.Delete(overrideChunks[chunkIndex]);
                        }
                    }

                    DeleteTransientArray(overrideEncodedBytes);
                    DeleteTransientArray(overrideChunks);
                    NativeOwnership.Delete(platformOverrides[index]);
                }
            }

            DeleteTransientArray(encodedBytes);
            DeleteTransientArray(chunks);
            DeleteTransientArray(platformOverrides);
            NativeOwnership.Delete(asset);
        }

        /// <summary>
        /// Deletes one transient array only when it is backed by heap allocation instead of the shared empty-array singleton.
        /// </summary>
        /// <typeparam name="T">Element type stored in the transient array.</typeparam>
        /// <param name="values">Transient array to delete on the native side.</param>
        static void DeleteTransientArray<T>(T[] values) {
            if (values == null || ReferenceEquals(values, Array.Empty<T>())) {
                return;
            }

            NativeOwnership.Delete(values);
        }

        /// <summary>
        /// Builds one stable cache key for a generated scene asset reference.
        /// </summary>
        /// <param name="reference">Generated scene asset reference to key.</param>
        /// <returns>Stable cache key for the generated asset.</returns>
        string BuildGeneratedAssetCacheKey(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }
            if (reference.SourceKind != SceneAssetReferenceSourceKind.Generated) {
                throw new InvalidOperationException("Generated asset cache keys require generated scene asset references.");
            }
            if (string.IsNullOrWhiteSpace(reference.ProviderId)) {
                throw new InvalidOperationException("Generated scene asset references require a provider id.");
            }
            if (string.IsNullOrWhiteSpace(reference.AssetId)) {
                throw new InvalidOperationException("Generated scene asset references require an asset id.");
            }

            return string.Concat(reference.ProviderId, "::", reference.AssetId);
        }

        /// <summary>
        /// Resolves one packaged file-backed scene asset reference to the runtime asset path consumed by the active content source.
        /// </summary>
        /// <param name="reference">Scene asset reference to resolve.</param>
        /// <returns>Runtime asset path understood by the active content source.</returns>
        string ResolveFileBackedAssetPath(SceneAssetReference reference) {
            if (reference.SourceKind != SceneAssetReferenceSourceKind.FileSystem
                && reference.SourceKind != SceneAssetReferenceSourceKind.Generated) {
                throw new InvalidOperationException("Player builds currently require file-backed packaged scene references.");
            }
            if (string.IsNullOrWhiteSpace(reference.RelativePath)) {
                throw new InvalidOperationException("Packaged scene asset references must include a relative path.");
            }

#if HELENGINE_RUNTIME_ALLOW_ROOTED_PACKAGED_PATHS
            if (Path.IsPathRooted(reference.RelativePath)) {
                return Path.GetFullPath(reference.RelativePath);
            }
#endif
            return CanonicalPackagedAssetPath.ValidateCanonical(reference.RelativePath);
        }

        /// <summary>
        /// Attaches one external cooked atlas texture when the packaged font payload references one instead of embedding raw atlas bytes.
        /// </summary>
        /// <param name="fontAsset">Packaged font asset that may reference one external cooked atlas path.</param>
        void AttachExternalCookedFontAtlasIfPresent(FontAsset fontAsset) {
            if (fontAsset == null) {
                throw new ArgumentNullException(nameof(fontAsset));
            }
            if (string.IsNullOrWhiteSpace(fontAsset.CookedAtlasTextureRelativePath)) {
                return;
            }
            RenderManager2D renderer = RequireRenderManager2D();
            string atlasFullPath = ResolvePackagedContentPath(fontAsset.CookedAtlasTextureRelativePath);
#if HELENGINE_RUNTIME_TEXTURE_RESOLUTION_COOKED_PLATFORM_OWNED
            RuntimeTexture runtimeTexture = renderer.BuildTextureFromCooked(atlasFullPath, AssetContentManager.ContentStreamSource);
            fontAsset.AttachCookedRuntimeTexture(runtimeTexture);
#else
            TextureAsset cookedAtlasTextureAsset = AssetContentManager.Load<TextureAsset>(atlasFullPath, RuntimeContentProcessorIds.TextureAsset);
            RuntimeTexture runtimeTexture = renderer.BuildTextureFromRaw(cookedAtlasTextureAsset);
            fontAsset.AttachProcessedTexture(runtimeTexture, cookedAtlasTextureAsset);
#endif
        }

        RenderManager3D RequireRenderManager3D() {
            return RenderManager3D ?? throw new InvalidOperationException("The owning core must provide a 3D renderer to resolve this asset.");
        }

        RenderManager2D RequireRenderManager2D() {
            return RenderManager2D ?? throw new InvalidOperationException("The owning core must provide a 2D renderer to resolve this asset.");
        }

        /// <summary>
        /// Resolves one packaged content-relative path to the runtime asset path consumed by the active content source.
        /// </summary>
        /// <param name="relativePath">Packaged content-relative path to resolve.</param>
        /// <returns>Runtime asset path understood by the active content source.</returns>
        string ResolvePackagedContentPath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

#if HELENGINE_RUNTIME_ALLOW_ROOTED_PACKAGED_PATHS
            if (Path.IsPathRooted(relativePath)) {
                return Path.GetFullPath(relativePath);
            }
#endif
            return CanonicalPackagedAssetPath.ValidateCanonical(relativePath);
        }

        /// <summary>
        /// Tracks one runtime asset so the owning scene can release it during unload.
        /// </summary>
        /// <param name="asset">Runtime asset resolved during scene materialization.</param>
        /// <returns>A borrowed alias to the runtime texture now owned by the active scene load.</returns>
        [NativeBorrowedReturn]
        RuntimeTexture TrackOwnedTexture([NativeTakesOwnership] RuntimeTexture asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }
            if (ActiveOwnedTextures == null) {
                throw new InvalidOperationException("Runtime texture ownership can only be transferred during active scene asset tracking.");
            }

            if (!ActiveOwnedTextures.Contains(asset)) {
                ActiveOwnedTextures.Add(asset);
            }
            return asset;
        }

        /// <summary>
        /// Tracks one scene-owned font asset so the owning scene can release it during unload.
        /// </summary>
        /// <param name="asset">Font asset resolved during scene materialization.</param>
        /// <returns>A borrowed alias to the font asset now owned by the active scene load.</returns>
        [NativeBorrowedReturn]
        FontAsset TrackOwnedFont([NativeTakesOwnership] FontAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }
            if (ActiveOwnedFonts == null) {
                throw new InvalidOperationException("Font ownership can only be transferred during active scene asset tracking.");
            }

            if (!ActiveOwnedFonts.Contains(asset)) {
                ActiveOwnedFonts.Add(asset);
            }
            return asset;
        }

        /// <summary>
        /// Tracks one scene-owned audio asset so the owning scene can release it during unload.
        /// </summary>
        /// <param name="asset">Audio asset resolved during scene materialization.</param>
        /// <returns>A borrowed alias to the audio asset now owned by the active scene load.</returns>
        [NativeBorrowedReturn]
        AudioAsset TrackOwnedAudio([NativeTakesOwnership] AudioAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }
            if (ActiveOwnedAudio == null) {
                throw new InvalidOperationException("Audio ownership can only be transferred during active scene asset tracking.");
            }

            if (!ActiveOwnedAudio.Contains(asset)) {
                ActiveOwnedAudio.Add(asset);
            }
            return asset;
        }

        /// <summary>
        /// Tracks one scene-owned runtime model so the owning scene can release it during unload.
        /// </summary>
        /// <param name="asset">Runtime model resolved during scene materialization.</param>
        /// <returns>A borrowed alias to the runtime model now owned by the active scene load.</returns>
        [NativeBorrowedReturn]
        RuntimeModel TrackOwnedModel([NativeTakesOwnership] RuntimeModel asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }
            if (ActiveOwnedModels == null) {
                throw new InvalidOperationException("Runtime model ownership can only be transferred during active scene asset tracking.");
            }

            if (!ActiveOwnedModels.Contains(asset)) {
                ActiveOwnedModels.Add(asset);
            }
            return asset;
        }

        /// <summary>
        /// Tracks a runtime model created after ordinary reference resolution but before the active scene load completes.
        /// </summary>
        /// <param name="asset">Prepared runtime model owned by the active scene load.</param>
        public void TrackAdditionalOwnedModel([NativeTakesOwnership] RuntimeModel asset) {
            TrackOwnedModel(asset);
        }

        /// <summary>
        /// Tracks one scene-owned runtime material so the owning scene can release it during unload.
        /// </summary>
        /// <param name="asset">Runtime material resolved during scene materialization.</param>
        /// <returns>A borrowed alias to the runtime material now owned by the active scene load.</returns>
        [NativeBorrowedReturn]
        RuntimeMaterial TrackOwnedMaterial([NativeTakesOwnership] RuntimeMaterial asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }
            if (ActiveOwnedMaterials == null) {
                throw new InvalidOperationException("Runtime material ownership can only be transferred during active scene asset tracking.");
            }

            if (!ActiveOwnedMaterials.Contains(asset)) {
                ActiveOwnedMaterials.Add(asset);
            }
            return asset;
        }
    }
}


namespace helengine {
    /// <summary>
    /// Resolves packaged file-backed scene asset references into runtime assets for player builds.
    /// </summary>
    public sealed class RuntimeSceneAssetReferenceResolver {
        /// <summary>
        /// Content manager used to load packaged runtime assets.
        /// </summary>
        readonly ContentManager AssetContentManager;

        /// <summary>
        /// Tracks scene-owned runtime textures resolved during the active scene materialization scope.
        /// </summary>
        List<RuntimeTexture> ActiveOwnedTextures;

        /// <summary>
        /// Tracks scene-owned font assets resolved during the active scene materialization scope.
        /// </summary>
        List<FontAsset> ActiveOwnedFonts;

        /// <summary>
        /// Reuses packaged font assets resolved by absolute path during the active scene materialization scope.
        /// </summary>
        Dictionary<string, FontAsset> ActiveResolvedFontsByPath;

        /// <summary>
        /// Tracks scene-owned runtime models resolved during the active scene materialization scope.
        /// </summary>
        List<RuntimeModel> ActiveOwnedModels;

        /// <summary>
        /// Tracks scene-owned runtime materials resolved during the active scene materialization scope.
        /// </summary>
        List<RuntimeMaterial> ActiveOwnedMaterials;

        /// <summary>
        /// Reuses generated runtime models during the active scene materialization scope so repeated generated references share one runtime model instance.
        /// </summary>
        readonly Dictionary<string, RuntimeModel> ActiveGeneratedModelsByKey;

        /// <summary>
        /// Reuses generated runtime materials during the active scene materialization scope so repeated generated references share one runtime material instance.
        /// </summary>
        readonly Dictionary<string, RuntimeMaterial> ActiveGeneratedMaterialsByKey;

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
        public RuntimeSceneAssetReferenceResolver(ContentManager assetContentManager) {
            if (assetContentManager == null) {
                throw new ArgumentNullException(nameof(assetContentManager));
            }

            AssetContentManager = assetContentManager;
            ActiveGeneratedModelsByKey = new Dictionary<string, RuntimeModel>(StringComparer.Ordinal);
            ActiveGeneratedMaterialsByKey = new Dictionary<string, RuntimeMaterial>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Initializes a new packaged scene asset resolver while accepting the legacy third constructor argument kept only for compile-surface compatibility.
        /// </summary>
        /// <param name="assetContentManager">Content manager used to load packaged assets.</param>
        /// <param name="legacyShaderTarget">Legacy shader-target argument ignored by the generic runtime resolver.</param>
        public RuntimeSceneAssetReferenceResolver(ContentManager assetContentManager, object legacyShaderTarget)
            : this(assetContentManager) {
            if (legacyShaderTarget == null) {
                throw new ArgumentNullException(nameof(legacyShaderTarget));
            }
        }

        /// <summary>
        /// Resolves one packaged model reference into a runtime model instance.
        /// </summary>
        /// <param name="reference">Packaged scene asset reference to resolve.</param>
        /// <returns>Runtime model instance rebuilt from packaged data.</returns>
        public RuntimeModel ResolveModel(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                string generatedAssetKey = BuildGeneratedAssetCacheKey(reference);
                if (ActiveGeneratedModelsByKey.TryGetValue(generatedAssetKey, out RuntimeModel generatedRuntimeModel)) {
                    TrackOwnedModel(generatedRuntimeModel);
                    return generatedRuntimeModel;
                }

                string generatedFullPath = ResolveFileBackedAssetPath(reference);
#if HELENGINE_RUNTIME_MODEL_RESOLUTION_COOKED_PLATFORM_OWNED
                RuntimeModel generatedModel = Core.Instance.RenderManager3D.BuildModelFromCooked(generatedFullPath, AssetContentManager.ContentStreamSource);
                ActiveGeneratedModelsByKey.Add(generatedAssetKey, generatedModel);
                TrackOwnedModel(generatedModel);
                return generatedModel;
#else
                ModelAsset generatedModelAsset = AssetContentManager.Load<ModelAsset>(generatedFullPath, RuntimeContentProcessorIds.ModelAsset);
                try {
                    RuntimeModel generatedModel = Core.Instance.RenderManager3D.BuildModelFromRaw(generatedModelAsset);
                    ActiveGeneratedModelsByKey.Add(generatedAssetKey, generatedModel);
                    TrackOwnedModel(generatedModel);
                    return generatedModel;
                } finally {
                    ReleaseTransientModelAsset(generatedModelAsset);
                }
#endif
            }

            string fullPath = ResolveFileBackedAssetPath(reference);
#if HELENGINE_RUNTIME_MODEL_RESOLUTION_COOKED_PLATFORM_OWNED
            RuntimeModel runtimeModel = Core.Instance.RenderManager3D.BuildModelFromCooked(fullPath, AssetContentManager.ContentStreamSource);
            TrackOwnedModel(runtimeModel);
            return runtimeModel;
#else
            ModelAsset modelAsset = AssetContentManager.Load<ModelAsset>(fullPath, RuntimeContentProcessorIds.ModelAsset);
            try {
                RuntimeModel runtimeModel = Core.Instance.RenderManager3D.BuildModelFromRaw(modelAsset);
                TrackOwnedModel(runtimeModel);
                return runtimeModel;
            } finally {
                ReleaseTransientModelAsset(modelAsset);
            }
#endif
        }

        /// <summary>
        /// Resolves one packaged material reference into a runtime material instance.
        /// </summary>
        /// <param name="reference">Packaged scene asset reference to resolve.</param>
        /// <returns>Runtime material instance rebuilt from packaged data.</returns>
        public RuntimeMaterial ResolveMaterial(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                string generatedAssetKey = BuildGeneratedAssetCacheKey(reference);
                if (ActiveGeneratedMaterialsByKey.TryGetValue(generatedAssetKey, out RuntimeMaterial generatedRuntimeMaterial)) {
                    TrackOwnedMaterial(generatedRuntimeMaterial);
                    return generatedRuntimeMaterial;
                }

                string generatedFullPath = ResolveFileBackedAssetPath(reference);
#if HELENGINE_RUNTIME_MATERIAL_RESOLUTION_COOKED_PLATFORM_OWNED
                RuntimeMaterial generatedCookedRuntimeMaterial = Core.Instance.RenderManager3D.BuildMaterialFromCooked(generatedFullPath, AssetContentManager.ContentStreamSource);
                ActiveGeneratedMaterialsByKey.Add(generatedAssetKey, generatedCookedRuntimeMaterial);
                TrackOwnedMaterial(generatedCookedRuntimeMaterial);
                return generatedCookedRuntimeMaterial;
#else
                RuntimeMaterial generatedRawRuntimeMaterial = Core.Instance.RenderManager3D.BuildMaterialFromRawAsset(
                    AssetContentManager,
                    generatedFullPath);
                ActiveGeneratedMaterialsByKey.Add(generatedAssetKey, generatedRawRuntimeMaterial);
                TrackOwnedMaterial(generatedRawRuntimeMaterial);
                return generatedRawRuntimeMaterial;
#endif
            }

            string fullPath = ResolveFileBackedAssetPath(reference);
#if HELENGINE_RUNTIME_MATERIAL_RESOLUTION_COOKED_PLATFORM_OWNED
            RuntimeMaterial runtimeMaterial = Core.Instance.RenderManager3D.BuildMaterialFromCooked(fullPath, AssetContentManager.ContentStreamSource);
            TrackOwnedMaterial(runtimeMaterial);
            return runtimeMaterial;
#else
            RuntimeMaterial runtimeMaterial = Core.Instance.RenderManager3D.BuildMaterialFromRawAsset(
                AssetContentManager,
                fullPath);
            TrackOwnedMaterial(runtimeMaterial);
            return runtimeMaterial;
#endif
        }

        /// <summary>
        /// Resolves one packaged font reference into a runtime font asset instance.
        /// </summary>
        /// <param name="reference">Packaged scene asset reference to resolve.</param>
        /// <returns>Runtime font asset instance rebuilt from packaged data.</returns>
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
            TrackOwnedFont(fontAsset);
            return fontAsset;
        }

        /// <summary>
        /// Resolves one packaged texture reference into a runtime texture instance.
        /// </summary>
        /// <param name="reference">Packaged scene asset reference to resolve.</param>
        /// <returns>Runtime texture instance rebuilt from packaged data.</returns>
        public RuntimeTexture ResolveTexture(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            LastTextureLoadStage = "ResolveTextureBegin";
            LastTextureRelativePath = reference.RelativePath ?? string.Empty;
            string fullPath = ResolveFileBackedAssetPath(reference);
#if HELENGINE_RUNTIME_TEXTURE_RESOLUTION_COOKED_PLATFORM_OWNED
            LastTextureLoadStage = "ResolveTextureBeforeBuild";
            RuntimeTexture runtimeTexture = Core.Instance.RenderManager2D.BuildTextureFromCooked(fullPath, AssetContentManager.ContentStreamSource);
            LastTextureLoadStage = "ResolveTextureAfterBuild";
            TrackOwnedTexture(runtimeTexture);
            LastTextureLoadStage = "ResolveTextureTracked";
            return runtimeTexture;
#else
            LastTextureLoadStage = "ResolveTextureBeforeContentLoad";
            TextureAsset textureAsset = AssetContentManager.Load<TextureAsset>(fullPath, RuntimeContentProcessorIds.TextureAsset);
            try {
                LastTextureLoadStage = "ResolveTextureBeforeBuild";
                RuntimeTexture runtimeTexture = Core.Instance.RenderManager2D.BuildTextureFromRaw(textureAsset);
                LastTextureLoadStage = "ResolveTextureAfterBuild";
                TrackOwnedTexture(runtimeTexture);
                LastTextureLoadStage = "ResolveTextureTracked";
                return runtimeTexture;
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
        /// Starts one scene-owned asset tracking scope for the next packaged scene materialization.
        /// </summary>
        public void BeginOwnedAssetTracking() {
            if (ActiveOwnedTextures != null || ActiveOwnedFonts != null || ActiveOwnedModels != null || ActiveOwnedMaterials != null) {
                throw new InvalidOperationException("Runtime scene asset tracking is already active.");
            }

            ResetGeneratedRuntimeAssetCaches();
            ActiveOwnedTextures = new List<RuntimeTexture>();
            ActiveOwnedFonts = new List<FontAsset>();
            ActiveResolvedFontsByPath = new Dictionary<string, FontAsset>(StringComparer.OrdinalIgnoreCase);
            ActiveOwnedModels = new List<RuntimeModel>();
            ActiveOwnedMaterials = new List<RuntimeMaterial>();
        }

        /// <summary>
        /// Completes the active scene-owned asset tracking scope and returns the resolved assets.
        /// </summary>
        /// <returns>Scene-owned runtime assets resolved during the active materialization scope.</returns>
        public RuntimeSceneOwnedAssetSet CompleteOwnedAssetTracking() {
            if (ActiveOwnedTextures == null || ActiveOwnedFonts == null || ActiveOwnedModels == null || ActiveOwnedMaterials == null) {
                throw new InvalidOperationException("Runtime scene asset tracking is not active.");
            }

            List<RuntimeTexture> ownedTextures = ActiveOwnedTextures;
            List<FontAsset> ownedFonts = ActiveOwnedFonts;
            List<RuntimeModel> ownedModels = ActiveOwnedModels;
            List<RuntimeMaterial> ownedMaterials = ActiveOwnedMaterials;
            Dictionary<string, FontAsset> resolvedFontsByPath = ActiveResolvedFontsByPath;
            ActiveOwnedTextures = null;
            ActiveOwnedFonts = null;
            ActiveResolvedFontsByPath = null;
            ActiveOwnedModels = null;
            ActiveOwnedMaterials = null;
            ResetGeneratedRuntimeAssetCaches();
            NativeOwnership.Delete(resolvedFontsByPath);
            return new RuntimeSceneOwnedAssetSet(ownedTextures, ownedFonts, ownedModels, ownedMaterials);
        }

        /// <summary>
        /// Cancels the active scene-owned asset tracking scope after one failed materialization attempt.
        /// </summary>
        public void CancelOwnedAssetTracking() {
            List<RuntimeTexture> activeOwnedTextures = ActiveOwnedTextures;
            List<FontAsset> activeOwnedFonts = ActiveOwnedFonts;
            Dictionary<string, FontAsset> activeResolvedFontsByPath = ActiveResolvedFontsByPath;
            List<RuntimeModel> activeOwnedModels = ActiveOwnedModels;
            List<RuntimeMaterial> activeOwnedMaterials = ActiveOwnedMaterials;
            ActiveOwnedTextures = null;
            ActiveOwnedFonts = null;
            ActiveResolvedFontsByPath = null;
            ActiveOwnedModels = null;
            ActiveOwnedMaterials = null;
            ResetGeneratedRuntimeAssetCaches();
            NativeOwnership.Delete(activeOwnedTextures);
            NativeOwnership.Delete(activeOwnedFonts);
            NativeOwnership.Delete(activeResolvedFontsByPath);
            NativeOwnership.Delete(activeOwnedModels);
            NativeOwnership.Delete(activeOwnedMaterials);
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

            byte[] colors = asset.Colors;
            byte[] paletteColors = asset.PaletteColors;
            asset.Colors = null;
            asset.PaletteColors = null;
            DeleteTransientArray(colors);
            DeleteTransientArray(paletteColors);
            NativeOwnership.Delete(asset);
        }

        /// <summary>
        /// Releases one transient model asset and all deserialized mesh buffers used only during runtime-model construction.
        /// </summary>
        /// <param name="asset">Transient model asset to release.</param>
        static void ReleaseTransientModelAsset(ModelAsset asset) {
            if (asset == null) {
                return;
            }

            float3[] positions = asset.Positions;
            float3[] normals = asset.Normals;
            float2[] texCoords = asset.TexCoords;
            ushort[] indices16 = asset.Indices16;
            uint[] indices32 = asset.Indices32;
            ModelSubmeshAsset[] submeshes = asset.Submeshes;
            asset.Positions = null;
            asset.Normals = null;
            asset.TexCoords = null;
            asset.Indices16 = null;
            asset.Indices32 = null;
            asset.Submeshes = null;
            if (submeshes != null) {
                for (int index = 0; index < submeshes.Length; index++) {
                    NativeOwnership.Delete(submeshes[index]);
                }
            }

            DeleteTransientArray(positions);
            DeleteTransientArray(normals);
            DeleteTransientArray(texCoords);
            DeleteTransientArray(indices16);
            DeleteTransientArray(indices32);
            DeleteTransientArray(submeshes);
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
            if (Core.Instance == null || Core.Instance.RenderManager2D == null) {
                throw new InvalidOperationException("External cooked font atlases require an initialized 2D render manager.");
            }

            string atlasFullPath = ResolvePackagedContentPath(fontAsset.CookedAtlasTextureRelativePath);
#if HELENGINE_RUNTIME_TEXTURE_RESOLUTION_COOKED_PLATFORM_OWNED
            RuntimeTexture runtimeTexture = Core.Instance.RenderManager2D.BuildTextureFromCooked(atlasFullPath, AssetContentManager.ContentStreamSource);
            fontAsset.AttachCookedRuntimeTexture(runtimeTexture);
#else
            TextureAsset cookedAtlasTextureAsset = AssetContentManager.Load<TextureAsset>(atlasFullPath, RuntimeContentProcessorIds.TextureAsset);
            RuntimeTexture runtimeTexture = Core.Instance.RenderManager2D.BuildTextureFromRaw(cookedAtlasTextureAsset);
            fontAsset.AttachProcessedTexture(runtimeTexture, cookedAtlasTextureAsset);
#endif
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
        void TrackOwnedTexture(RuntimeTexture asset) {
            if (asset == null || ActiveOwnedTextures == null) {
                return;
            }

            if (!ActiveOwnedTextures.Contains(asset)) {
                ActiveOwnedTextures.Add(asset);
            }
        }

        /// <summary>
        /// Tracks one scene-owned font asset so the owning scene can release it during unload.
        /// </summary>
        /// <param name="asset">Font asset resolved during scene materialization.</param>
        void TrackOwnedFont(FontAsset asset) {
            if (asset == null || ActiveOwnedFonts == null) {
                return;
            }

            if (!ActiveOwnedFonts.Contains(asset)) {
                ActiveOwnedFonts.Add(asset);
            }
        }

        /// <summary>
        /// Tracks one scene-owned runtime model so the owning scene can release it during unload.
        /// </summary>
        /// <param name="asset">Runtime model resolved during scene materialization.</param>
        void TrackOwnedModel(RuntimeModel asset) {
            if (asset == null || ActiveOwnedModels == null) {
                return;
            }

            if (!ActiveOwnedModels.Contains(asset)) {
                ActiveOwnedModels.Add(asset);
            }
        }

        /// <summary>
        /// Tracks one scene-owned runtime material so the owning scene can release it during unload.
        /// </summary>
        /// <param name="asset">Runtime material resolved during scene materialization.</param>
        void TrackOwnedMaterial(RuntimeMaterial asset) {
            if (asset == null || ActiveOwnedMaterials == null) {
                return;
            }

            if (!ActiveOwnedMaterials.Contains(asset)) {
                ActiveOwnedMaterials.Add(asset);
            }
        }
    }
}


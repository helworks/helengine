namespace helengine {
    /// <summary>
    /// Resolves packaged file-backed scene asset references into runtime assets for player builds.
    /// </summary>
    public sealed class RuntimeSceneAssetReferenceResolver {
        /// <summary>
        /// Folder name used for packaged shader assets.
        /// </summary>
        const string ShaderDirectoryName = "cooked/shaders";
        /// <summary>
        /// Folder name used for packaged imported texture assets.
        /// </summary>
        const string ImportedTextureDirectoryName = "cooked/imported";

        /// <summary>
        /// Folder name used for packaged font assets.
        /// </summary>
        const string FontDirectoryName = "fonts";

        /// <summary>
        /// Shared shader package file extension.
        /// </summary>
        const string ShaderPackageExtension = ".hasset";

        /// <summary>
        /// Absolute packaged content root used to resolve file-backed scene references.
        /// </summary>
        readonly string ContentRootPath;

        /// <summary>
        /// Content manager used to load packaged runtime assets.
        /// </summary>
        readonly ContentManager AssetContentManager;

        /// <summary>
        /// Shader target used to resolve packaged shader assets.
        /// </summary>
        readonly ShaderCompileTarget ShaderTarget;

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
        /// Reuses generated runtime models across scene loads so built-in engine primitives are not tracked as scene-owned assets.
        /// </summary>
        readonly Dictionary<string, RuntimeModel> ActiveGeneratedModelsByKey;

        /// <summary>
        /// Reuses generated runtime materials across scene loads so built-in engine materials are not tracked as scene-owned assets.
        /// </summary>
        readonly Dictionary<string, RuntimeMaterial> ActiveGeneratedMaterialsByKey;

        /// <summary>
        /// Gets the last recorded text-load stage that passed through this resolver.
        /// </summary>
        public string LastTextLoadStage { get; set; }

        /// <summary>
        /// Gets the last recorded packaged font relative path that passed through this resolver.
        /// </summary>
        public string LastTextFontRelativePath { get; set; }

        /// <summary>
        /// Gets the most recent packaged font-deserialization stage reached by the active content loader.
        /// </summary>
        public string LastFontDeserializeStage => FontAssetBinarySerializer.LastDeserializeStage;

        /// <summary>
        /// Initializes a new packaged scene asset resolver.
        /// </summary>
        /// <param name="assetContentManager">Content manager used to load packaged assets.</param>
        /// <param name="contentRootPath">Absolute packaged content root path.</param>
        /// <param name="shaderTarget">Shader target used to resolve packaged shader assets.</param>
        public RuntimeSceneAssetReferenceResolver(ContentManager assetContentManager, string contentRootPath, ShaderCompileTarget shaderTarget) {
            if (assetContentManager == null) {
                throw new ArgumentNullException(nameof(assetContentManager));
            }
            if (string.IsNullOrWhiteSpace(contentRootPath)) {
                throw new ArgumentException("Content root path must be provided.", nameof(contentRootPath));
            }

            ContentRootPath = Path.GetFullPath(contentRootPath);
            AssetContentManager = assetContentManager;
            ShaderTarget = shaderTarget;
            ActiveGeneratedModelsByKey = new Dictionary<string, RuntimeModel>(StringComparer.Ordinal);
            ActiveGeneratedMaterialsByKey = new Dictionary<string, RuntimeMaterial>(StringComparer.Ordinal);
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
                    return generatedRuntimeModel;
                }

                string generatedFullPath = ResolveFileBackedAssetPath(reference);
                ModelAsset generatedModelAsset = AssetContentManager.Load<ModelAsset>(generatedFullPath, RuntimeContentProcessorIds.ModelAsset);
                RuntimeModel generatedModel = Core.Instance.RenderManager3D.BuildModelFromRaw(generatedModelAsset);
                ActiveGeneratedModelsByKey.Add(generatedAssetKey, generatedModel);
                return generatedModel;
            }

            string fullPath = ResolveFileBackedAssetPath(reference);
            ModelAsset modelAsset = AssetContentManager.Load<ModelAsset>(fullPath, RuntimeContentProcessorIds.ModelAsset);
            RuntimeModel runtimeModel = Core.Instance.RenderManager3D.BuildModelFromRaw(modelAsset);
            TrackOwnedModel(runtimeModel);
            return runtimeModel;
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
                    return generatedRuntimeMaterial;
                }

                string generatedFullPath = ResolveFileBackedAssetPath(reference);
#if HELENGINE_RUNTIME_MATERIAL_RESOLUTION_COOKED_PLATFORM_OWNED
                PlatformMaterialAsset generatedPlatformMaterialAsset = AssetContentManager.Load<PlatformMaterialAsset>(generatedFullPath, RuntimeContentProcessorIds.MaterialAsset);
                RuntimeMaterial generatedCookedRuntimeMaterial = Core.Instance.RenderManager3D.BuildMaterialFromCooked(generatedPlatformMaterialAsset);
                ActiveGeneratedMaterialsByKey.Add(generatedAssetKey, generatedCookedRuntimeMaterial);
                return generatedCookedRuntimeMaterial;
#else
                MaterialAsset generatedMaterialAsset = AssetContentManager.Load<MaterialAsset>(generatedFullPath, RuntimeContentProcessorIds.MaterialAsset);
                ShaderAsset generatedShaderAsset = AssetContentManager.Load<ShaderAsset>(
                    ResolveShaderPackagePath(generatedMaterialAsset.ShaderAssetId),
                    RuntimeContentProcessorIds.ShaderAsset);
                RuntimeMaterial generatedRawRuntimeMaterial = Core.Instance.RenderManager3D.BuildMaterialFromRaw(generatedMaterialAsset, generatedShaderAsset);
                ApplyMaterialDiffuseTexture(generatedRawRuntimeMaterial, generatedMaterialAsset, generatedFullPath);
                ActiveGeneratedMaterialsByKey.Add(generatedAssetKey, generatedRawRuntimeMaterial);
                return generatedRawRuntimeMaterial;
#endif
            }

            string fullPath = ResolveFileBackedAssetPath(reference);
#if HELENGINE_RUNTIME_MATERIAL_RESOLUTION_COOKED_PLATFORM_OWNED
            PlatformMaterialAsset materialAsset = AssetContentManager.Load<PlatformMaterialAsset>(fullPath, RuntimeContentProcessorIds.MaterialAsset);
            return Core.Instance.RenderManager3D.BuildMaterialFromCooked(materialAsset);
#else
            MaterialAsset materialAsset = AssetContentManager.Load<MaterialAsset>(fullPath, RuntimeContentProcessorIds.MaterialAsset);
            ShaderAsset shaderAsset = AssetContentManager.Load<ShaderAsset>(
                ResolveShaderPackagePath(materialAsset.ShaderAssetId),
                RuntimeContentProcessorIds.ShaderAsset);
            RuntimeMaterial runtimeMaterial = Core.Instance.RenderManager3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
            TrackOwnedMaterial(runtimeMaterial);
            ApplyMaterialDiffuseTexture(runtimeMaterial, materialAsset, fullPath);
            return runtimeMaterial;
#endif
        }

        /// <summary>
        /// Applies one authored diffuse texture to the resolved runtime material when the packaged material asset references one.
        /// </summary>
        /// <param name="runtimeMaterial">Runtime material that should receive the diffuse texture.</param>
        /// <param name="materialAsset">Packaged material asset that declares the authored diffuse texture asset id.</param>
        /// <param name="materialPath">Absolute path to the serialized material asset.</param>
        void ApplyMaterialDiffuseTexture(RuntimeMaterial runtimeMaterial, MaterialAsset materialAsset, string materialPath) {
            if (runtimeMaterial == null) {
                throw new ArgumentNullException(nameof(runtimeMaterial));
            }
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }
            if (string.IsNullOrWhiteSpace(materialPath)) {
                throw new ArgumentException("Material path must be provided.", nameof(materialPath));
            }
            if (string.IsNullOrWhiteSpace(materialAsset.DiffuseTextureAssetId)) {
                return;
            }

            string diffuseTexturePath;
            if (TryResolveSourceTexturePath(materialPath, materialAsset.DiffuseTextureAssetId, out diffuseTexturePath)) {
                TextureAsset sourceTextureAsset = AssetContentManager.Load<TextureAsset>(diffuseTexturePath, RuntimeContentProcessorIds.TextureAsset);
                RuntimeTexture sourceRuntimeTexture = Core.Instance.RenderManager2D.BuildTextureFromRaw(sourceTextureAsset);
                TrackOwnedTexture(sourceRuntimeTexture);
                runtimeMaterial.Properties.SetTexture(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName, sourceRuntimeTexture);
                return;
            }

            diffuseTexturePath = ResolveImportedTexturePackagePath(materialAsset.DiffuseTextureAssetId);
            TextureAsset textureAsset = AssetContentManager.Load<TextureAsset>(diffuseTexturePath, RuntimeContentProcessorIds.TextureAsset);
            RuntimeTexture runtimeTexture = Core.Instance.RenderManager2D.BuildTextureFromRaw(textureAsset);
            TrackOwnedTexture(runtimeTexture);
            runtimeMaterial.Properties.SetTexture(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName, runtimeTexture);
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
            LastTextLoadStage = "ResolveFontAfterContentLoad";
            if (ActiveResolvedFontsByPath != null) {
                ActiveResolvedFontsByPath.Add(fullPath, fontAsset);
            }
            TrackOwnedFont(fontAsset);
            if (fontAsset.Texture != null) {
                TrackOwnedTexture(fontAsset.Texture);
            }
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

            string fullPath = ResolveFileBackedAssetPath(reference);
            TextureAsset textureAsset = AssetContentManager.Load<TextureAsset>(fullPath, RuntimeContentProcessorIds.TextureAsset);
            RuntimeTexture runtimeTexture = Core.Instance.RenderManager2D.BuildTextureFromRaw(textureAsset);
            TrackOwnedTexture(runtimeTexture);
            return runtimeTexture;
        }

        /// <summary>
        /// Starts one scene-owned asset tracking scope for the next packaged scene materialization.
        /// </summary>
        public void BeginOwnedAssetTracking() {
            if (ActiveOwnedTextures != null || ActiveOwnedFonts != null || ActiveOwnedModels != null || ActiveOwnedMaterials != null) {
                throw new InvalidOperationException("Runtime scene asset tracking is already active.");
            }

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

            List<RuntimeTexture> ownedTextures = new List<RuntimeTexture>(ActiveOwnedTextures.Count);
            for (int index = 0; index < ActiveOwnedTextures.Count; index++) {
                ownedTextures.Add(ActiveOwnedTextures[index]);
            }
            List<FontAsset> ownedFonts = new List<FontAsset>(ActiveOwnedFonts.Count);
            for (int index = 0; index < ActiveOwnedFonts.Count; index++) {
                ownedFonts.Add(ActiveOwnedFonts[index]);
            }
            List<RuntimeModel> ownedModels = new List<RuntimeModel>(ActiveOwnedModels.Count);
            for (int index = 0; index < ActiveOwnedModels.Count; index++) {
                ownedModels.Add(ActiveOwnedModels[index]);
            }
            List<RuntimeMaterial> ownedMaterials = new List<RuntimeMaterial>(ActiveOwnedMaterials.Count);
            for (int index = 0; index < ActiveOwnedMaterials.Count; index++) {
                ownedMaterials.Add(ActiveOwnedMaterials[index]);
            }

            ActiveOwnedTextures = null;
            ActiveOwnedFonts = null;
            ActiveResolvedFontsByPath = null;
            ActiveOwnedModels = null;
            ActiveOwnedMaterials = null;
            return new RuntimeSceneOwnedAssetSet(ownedTextures, ownedFonts, ownedModels, ownedMaterials);
        }

        /// <summary>
        /// Cancels the active scene-owned asset tracking scope after one failed materialization attempt.
        /// </summary>
        public void CancelOwnedAssetTracking() {
            ActiveOwnedTextures = null;
            ActiveOwnedFonts = null;
            ActiveResolvedFontsByPath = null;
            ActiveOwnedModels = null;
            ActiveOwnedMaterials = null;
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
        /// Resolves one packaged file-backed scene asset reference to an absolute file path inside the packaged content root.
        /// </summary>
        /// <param name="reference">Scene asset reference to resolve.</param>
        /// <returns>Absolute packaged file path.</returns>
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
            string fullPath = Path.GetFullPath(Path.Combine(ContentRootPath, reference.RelativePath));
            string contentRootPrefix = EnsureTrailingDirectorySeparator(ContentRootPath);
            if (!fullPath.StartsWith(contentRootPrefix, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Packaged scene asset reference path must stay inside the content root.");
            }

            return fullPath;
        }

        /// <summary>
        /// Resolves one shader asset id into the packaged shader-asset path used by shader-backed player builds.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier stored on the packaged material asset.</param>
        /// <returns>Absolute packaged shader-asset path.</returns>
        string ResolveShaderPackagePath(string shaderAssetId) {
            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                throw new InvalidOperationException("Packaged material assets must include a shader asset id.");
            }

            string fileName = string.Concat(shaderAssetId, ".", ShaderTargetNames.GetTargetName(ShaderTarget), ShaderPackageExtension);
            return Path.Combine(ContentRootPath, ShaderDirectoryName, fileName);
        }

        /// <summary>
        /// Resolves one imported texture asset id into the packaged texture-asset path used by shader-backed player builds.
        /// </summary>
        /// <param name="assetId">Imported texture asset identifier stored on the packaged material asset.</param>
        /// <returns>Absolute packaged texture-asset path.</returns>
        string ResolveImportedTexturePackagePath(string assetId) {
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new InvalidOperationException("Packaged material assets must include a diffuse texture asset id before resolving imported textures.");
            }

            return Path.Combine(ContentRootPath, ImportedTextureDirectoryName, assetId);
        }

        /// <summary>
        /// Resolves one authored diffuse texture file that lives beside the serialized material asset.
        /// </summary>
        /// <param name="materialPath">Absolute path to the serialized material asset.</param>
        /// <param name="assetId">Imported texture asset identifier stored on the material asset.</param>
        /// <param name="texturePath">Resolved source texture path when one exists.</param>
        /// <returns>True when the source texture path exists; otherwise false.</returns>
        bool TryResolveSourceTexturePath(string materialPath, string assetId, out string texturePath) {
            if (string.IsNullOrWhiteSpace(materialPath)) {
                throw new ArgumentException("Material path must be provided.", nameof(materialPath));
            }
            if (string.IsNullOrWhiteSpace(assetId)) {
                texturePath = string.Empty;
                return false;
            }

            string materialDirectoryPath = Path.GetDirectoryName(Path.GetFullPath(materialPath));
            if (string.IsNullOrWhiteSpace(materialDirectoryPath)) {
                texturePath = string.Empty;
                return false;
            }

            string candidateTexturePath = Path.IsPathRooted(assetId)
                ? Path.GetFullPath(assetId)
                : Path.GetFullPath(Path.Combine(materialDirectoryPath, assetId));
            if (File.Exists(candidateTexturePath)) {
                texturePath = candidateTexturePath;
                return true;
            }

            texturePath = string.Empty;
            return false;
        }

        /// <summary>
        /// Ensures one directory path ends with a trailing separator before prefix comparisons occur.
        /// </summary>
        /// <param name="path">Directory path that should end with a separator.</param>
        /// <returns>Directory path with a trailing separator.</returns>
        string EnsureTrailingDirectorySeparator(string path) {
            if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)) {
                return path;
            }

            return string.Concat(path, Path.DirectorySeparatorChar);
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

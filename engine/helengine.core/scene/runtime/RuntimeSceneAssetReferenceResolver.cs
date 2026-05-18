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
                    TrackOwnedModel(generatedRuntimeModel);
                    return generatedRuntimeModel;
                }

                string generatedFullPath = ResolveFileBackedAssetPath(reference);
                ModelAsset generatedModelAsset = AssetContentManager.Load<ModelAsset>(generatedFullPath, RuntimeContentProcessorIds.ModelAsset);
                try {
                    RuntimeModel generatedModel = Core.Instance.RenderManager3D.BuildModelFromRaw(generatedModelAsset);
                    ActiveGeneratedModelsByKey.Add(generatedAssetKey, generatedModel);
                    TrackOwnedModel(generatedModel);
                    return generatedModel;
                } finally {
                    ReleaseTransientModelAsset(generatedModelAsset);
                }
            }

            string fullPath = ResolveFileBackedAssetPath(reference);
            ModelAsset modelAsset = AssetContentManager.Load<ModelAsset>(fullPath, RuntimeContentProcessorIds.ModelAsset);
            try {
                RuntimeModel runtimeModel = Core.Instance.RenderManager3D.BuildModelFromRaw(modelAsset);
                TrackOwnedModel(runtimeModel);
                return runtimeModel;
            } finally {
                ReleaseTransientModelAsset(modelAsset);
            }
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
                PlatformMaterialAsset generatedPlatformMaterialAsset = AssetContentManager.Load<PlatformMaterialAsset>(generatedFullPath, RuntimeContentProcessorIds.MaterialAsset);
                RuntimeMaterial generatedCookedRuntimeMaterial = Core.Instance.RenderManager3D.BuildMaterialFromCooked(generatedPlatformMaterialAsset);
                ActiveGeneratedMaterialsByKey.Add(generatedAssetKey, generatedCookedRuntimeMaterial);
                TrackOwnedMaterial(generatedCookedRuntimeMaterial);
                return generatedCookedRuntimeMaterial;
#else
                MaterialAsset generatedMaterialAsset = AssetContentManager.Load<MaterialAsset>(generatedFullPath, RuntimeContentProcessorIds.MaterialAsset);
                ShaderAsset generatedShaderAsset = AssetContentManager.Load<ShaderAsset>(
                    ResolveShaderPackagePath(generatedMaterialAsset.ShaderAssetId),
                    RuntimeContentProcessorIds.ShaderAsset);
                try {
                    RuntimeMaterial generatedRawRuntimeMaterial = Core.Instance.RenderManager3D.BuildMaterialFromRaw(generatedMaterialAsset, generatedShaderAsset);
                    ApplyMaterialDiffuseTexture(generatedRawRuntimeMaterial, generatedMaterialAsset, generatedFullPath);
                    ActiveGeneratedMaterialsByKey.Add(generatedAssetKey, generatedRawRuntimeMaterial);
                    TrackOwnedMaterial(generatedRawRuntimeMaterial);
                    return generatedRawRuntimeMaterial;
                } finally {
                    ReleaseTransientShaderAsset(generatedShaderAsset);
                    ReleaseTransientMaterialAsset(generatedMaterialAsset);
                }
#endif
            }

            string fullPath = ResolveFileBackedAssetPath(reference);
#if HELENGINE_RUNTIME_MATERIAL_RESOLUTION_COOKED_PLATFORM_OWNED
            PlatformMaterialAsset materialAsset = AssetContentManager.Load<PlatformMaterialAsset>(fullPath, RuntimeContentProcessorIds.MaterialAsset);
            RuntimeMaterial runtimeMaterial = Core.Instance.RenderManager3D.BuildMaterialFromCooked(materialAsset);
            TrackOwnedMaterial(runtimeMaterial);
            return runtimeMaterial;
#else
            MaterialAsset materialAsset = AssetContentManager.Load<MaterialAsset>(fullPath, RuntimeContentProcessorIds.MaterialAsset);
            ShaderAsset shaderAsset = AssetContentManager.Load<ShaderAsset>(
                ResolveShaderPackagePath(materialAsset.ShaderAssetId),
                RuntimeContentProcessorIds.ShaderAsset);
            try {
                RuntimeMaterial runtimeMaterial = Core.Instance.RenderManager3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
                TrackOwnedMaterial(runtimeMaterial);
                ApplyMaterialDiffuseTexture(runtimeMaterial, materialAsset, fullPath);
                return runtimeMaterial;
            } finally {
                ReleaseTransientShaderAsset(shaderAsset);
                ReleaseTransientMaterialAsset(materialAsset);
            }
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
                try {
                    RuntimeTexture sourceRuntimeTexture = Core.Instance.RenderManager2D.BuildTextureFromRaw(sourceTextureAsset);
                    TrackOwnedTexture(sourceRuntimeTexture);
                    runtimeMaterial.Properties.SetTexture(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName, sourceRuntimeTexture);
                    return;
                } finally {
                    ReleaseTransientTextureAsset(sourceTextureAsset);
                }
            }

            diffuseTexturePath = ResolveImportedTexturePackagePath(materialAsset.DiffuseTextureAssetId);
            TextureAsset textureAsset = AssetContentManager.Load<TextureAsset>(diffuseTexturePath, RuntimeContentProcessorIds.TextureAsset);
            try {
                RuntimeTexture runtimeTexture = Core.Instance.RenderManager2D.BuildTextureFromRaw(textureAsset);
                TrackOwnedTexture(runtimeTexture);
                runtimeMaterial.Properties.SetTexture(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName, runtimeTexture);
            } finally {
                ReleaseTransientTextureAsset(textureAsset);
            }
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
            try {
                RuntimeTexture runtimeTexture = Core.Instance.RenderManager2D.BuildTextureFromRaw(textureAsset);
                TrackOwnedTexture(runtimeTexture);
                return runtimeTexture;
            } finally {
                ReleaseTransientTextureAsset(textureAsset);
            }
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
            NativeOwnership.Delete(colors);
            NativeOwnership.Delete(paletteColors);
            NativeOwnership.Delete(asset);
        }

        /// <summary>
        /// Releases one transient material constant-buffer asset and its packed byte payload.
        /// </summary>
        /// <param name="asset">Transient material constant-buffer asset to release.</param>
        static void ReleaseTransientMaterialConstantBufferAsset(MaterialConstantBufferAsset asset) {
            if (asset == null) {
                return;
            }

            byte[] data = asset.Data;
            asset.Data = null;
            NativeOwnership.Delete(data);
            NativeOwnership.Delete(asset);
        }

        /// <summary>
        /// Releases one transient material asset and any authored constant-buffer payloads that were deserialized for conversion.
        /// </summary>
        /// <param name="asset">Transient material asset to release.</param>
        static void ReleaseTransientMaterialAsset(MaterialAsset asset) {
            if (asset == null) {
                return;
            }

            MaterialConstantBufferAsset[] constantBuffers = asset.ConstantBuffers;
            MaterialRenderState renderState = asset.RenderState;
            asset.ConstantBuffers = null;
            asset.RenderState = null;
            if (constantBuffers != null) {
                for (int index = 0; index < constantBuffers.Length; index++) {
                    ReleaseTransientMaterialConstantBufferAsset(constantBuffers[index]);
                }
            }

            NativeOwnership.Delete(constantBuffers);
            NativeOwnership.Delete(renderState);
            NativeOwnership.Delete(asset);
        }

        /// <summary>
        /// Releases one transient shader constant-member asset.
        /// </summary>
        /// <param name="asset">Transient shader constant-member asset to release.</param>
        static void ReleaseTransientShaderConstantMemberAsset(ShaderConstantMemberAsset asset) {
            if (asset == null) {
                return;
            }

            NativeOwnership.Delete(asset);
        }

        /// <summary>
        /// Releases one transient shader binding asset and any deserialized constant-member metadata it owns.
        /// </summary>
        /// <param name="asset">Transient shader binding asset to release.</param>
        static void ReleaseTransientShaderBindingAsset(ShaderBindingAsset asset) {
            if (asset == null) {
                return;
            }

            ShaderConstantMemberAsset[] members = asset.Members;
            asset.Members = null;
            if (members != null) {
                for (int index = 0; index < members.Length; index++) {
                    ReleaseTransientShaderConstantMemberAsset(members[index]);
                }
            }

            NativeOwnership.Delete(members);
            NativeOwnership.Delete(asset);
        }

        /// <summary>
        /// Releases one transient shader variant asset and its define array.
        /// </summary>
        /// <param name="asset">Transient shader variant asset to release.</param>
        static void ReleaseTransientShaderVariantAsset(ShaderVariantAsset asset) {
            if (asset == null) {
                return;
            }

            string[] defines = asset.Defines;
            asset.Defines = null;
            NativeOwnership.Delete(defines);
            NativeOwnership.Delete(asset);
        }

        /// <summary>
        /// Releases one transient shader program asset and all nested binding, signature, and variant metadata.
        /// </summary>
        /// <param name="asset">Transient shader program asset to release.</param>
        static void ReleaseTransientShaderProgramAsset(ShaderProgramAsset asset) {
            if (asset == null) {
                return;
            }

            ShaderBindingAsset[] bindings = asset.Bindings;
            ShaderVertexElementAsset[] inputs = asset.Inputs;
            ShaderVertexElementAsset[] outputs = asset.Outputs;
            ShaderVariantAsset[] variants = asset.Variants;
            asset.Bindings = null;
            asset.Inputs = null;
            asset.Outputs = null;
            asset.Variants = null;
            if (bindings != null) {
                for (int index = 0; index < bindings.Length; index++) {
                    ReleaseTransientShaderBindingAsset(bindings[index]);
                }
            }
            if (inputs != null) {
                for (int index = 0; index < inputs.Length; index++) {
                    NativeOwnership.Delete(inputs[index]);
                }
            }
            if (outputs != null) {
                for (int index = 0; index < outputs.Length; index++) {
                    NativeOwnership.Delete(outputs[index]);
                }
            }
            if (variants != null) {
                for (int index = 0; index < variants.Length; index++) {
                    ReleaseTransientShaderVariantAsset(variants[index]);
                }
            }

            NativeOwnership.Delete(bindings);
            NativeOwnership.Delete(inputs);
            NativeOwnership.Delete(outputs);
            NativeOwnership.Delete(variants);
            NativeOwnership.Delete(asset);
        }

        /// <summary>
        /// Releases one transient shader binary asset and its bytecode payload.
        /// </summary>
        /// <param name="asset">Transient shader binary asset to release.</param>
        static void ReleaseTransientShaderBinaryAsset(ShaderBinaryAsset asset) {
            if (asset == null) {
                return;
            }

            byte[] bytecode = asset.Bytecode;
            asset.Bytecode = null;
            NativeOwnership.Delete(bytecode);
            NativeOwnership.Delete(asset);
        }

        /// <summary>
        /// Releases one transient shader asset and every deserialized nested program and binary payload.
        /// </summary>
        /// <param name="asset">Transient shader asset to release.</param>
        static void ReleaseTransientShaderAsset(ShaderAsset asset) {
            if (asset == null) {
                return;
            }

            ShaderProgramAsset[] programs = asset.Programs;
            ShaderBinaryAsset[] binaries = asset.Binaries;
            asset.Programs = null;
            asset.Binaries = null;
            if (programs != null) {
                for (int index = 0; index < programs.Length; index++) {
                    ReleaseTransientShaderProgramAsset(programs[index]);
                }
            }
            if (binaries != null) {
                for (int index = 0; index < binaries.Length; index++) {
                    ReleaseTransientShaderBinaryAsset(binaries[index]);
                }
            }

            NativeOwnership.Delete(programs);
            NativeOwnership.Delete(binaries);
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
            byte[] ps2PackedMeshBytes = asset.Ps2PackedMeshBytes;
            asset.Positions = null;
            asset.Normals = null;
            asset.TexCoords = null;
            asset.Indices16 = null;
            asset.Indices32 = null;
            asset.Submeshes = null;
            asset.Ps2PackedMeshBytes = null;
            if (submeshes != null) {
                for (int index = 0; index < submeshes.Length; index++) {
                    NativeOwnership.Delete(submeshes[index]);
                }
            }

            NativeOwnership.Delete(positions);
            NativeOwnership.Delete(normals);
            NativeOwnership.Delete(texCoords);
            NativeOwnership.Delete(indices16);
            NativeOwnership.Delete(indices32);
            NativeOwnership.Delete(submeshes);
            NativeOwnership.Delete(ps2PackedMeshBytes);
            NativeOwnership.Delete(asset);
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

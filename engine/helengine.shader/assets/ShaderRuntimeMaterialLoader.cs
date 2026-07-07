namespace helengine {
    /// <summary>
    /// Rebuilds shader-backed runtime materials from packaged material assets by resolving the companion cooked shader package through generic content loading.
    /// </summary>
    public static class ShaderRuntimeMaterialLoader {
        /// <summary>
        /// Folder name used for packaged shader assets.
        /// </summary>
        const string ShaderDirectoryName = "cooked/shaders";

        /// <summary>
        /// Folder name used for packaged imported texture assets referenced by shader-backed materials.
        /// </summary>
        const string ImportedTextureDirectoryName = "cooked/imported";

        /// <summary>
        /// Builds one shader-backed runtime material from a packaged material asset.
        /// </summary>
        /// <param name="renderManager3D">Shader-aware renderer that will own the runtime material.</param>
        /// <param name="assetContentManager">Content manager that can deserialize the cooked shader package.</param>
        /// <param name="materialAssetPath">Runtime asset path to the serialized material asset.</param>
        /// <returns>Runtime material instance.</returns>
        public static RuntimeMaterial BuildMaterialFromRawAsset(
            IShaderRenderManager3D renderManager3D,
            ContentManager assetContentManager,
            string materialAssetPath) {
            if (renderManager3D == null) {
                throw new ArgumentNullException(nameof(renderManager3D));
            }
            if (assetContentManager == null) {
                throw new ArgumentNullException(nameof(assetContentManager));
            }
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            }

            RuntimeContentManagerConfiguration.ConfigureSharedAssetContentManager(assetContentManager);
            ShaderRuntimeContentRegistration.Register(assetContentManager);
            ShaderMaterialAsset materialAsset = assetContentManager.Load<ShaderMaterialAsset>(materialAssetPath, ShaderRuntimeContentProcessorIds.ShaderMaterialAsset);
            string shaderPackagePath = ResolveShaderPackagePath(materialAsset.ShaderAssetId, renderManager3D.ShaderCompileTarget);
            ShaderAsset shaderAsset = assetContentManager.Load<ShaderAsset>(shaderPackagePath, ShaderRuntimeContentProcessorIds.ShaderAsset);
            RuntimeMaterial runtimeMaterial = renderManager3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
            ApplyImportedDiffuseTexture(assetContentManager, materialAsset, runtimeMaterial);
            return runtimeMaterial;
        }

        /// <summary>
        /// Resolves one shader asset id into the packaged shader asset path used by shader-backed runtime builds.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier stored on the packaged material asset.</param>
        /// <param name="shaderTarget">Renderer shader target used to choose the correct cooked package variant.</param>
        /// <returns>Runtime asset path of the packaged shader asset.</returns>
        public static string ResolveShaderPackagePath(string shaderAssetId, ShaderCompileTarget shaderTarget) {
            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                throw new InvalidOperationException("Packaged material assets must include a shader asset id.");
            }

            string fileName = string.Concat(shaderAssetId, ".", ShaderTargetNames.GetTargetName(shaderTarget), ShaderRuntimeContentRegistration.ShaderPackageExtension);
            return CanonicalPackagedAssetPath.Normalize(Path.Combine(ShaderDirectoryName, fileName));
        }

        /// <summary>
        /// Applies one packaged imported diffuse texture to a shader-backed runtime material when the authored material payload references one.
        /// </summary>
        /// <param name="assetContentManager">Content manager that can deserialize packaged texture assets.</param>
        /// <param name="materialAsset">Packaged material asset that may reference an imported diffuse texture.</param>
        /// <param name="runtimeMaterial">Runtime material that should receive the diffuse texture binding.</param>
        static void ApplyImportedDiffuseTexture(
            ContentManager assetContentManager,
            ShaderMaterialAsset materialAsset,
            RuntimeMaterial runtimeMaterial) {
            if (assetContentManager == null) {
                throw new ArgumentNullException(nameof(assetContentManager));
            } else if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            } else if (runtimeMaterial == null) {
                throw new ArgumentNullException(nameof(runtimeMaterial));
            } else if (string.IsNullOrWhiteSpace(materialAsset.DiffuseTextureAssetId)) {
                return;
            }

            ShaderRuntimeMaterial shaderRuntimeMaterial = ShaderRuntimeMaterialAccess.Require(runtimeMaterial);
            int diffuseTextureBindingIndex = shaderRuntimeMaterial.Layout.FindTextureBindingIndex(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName);
            if (diffuseTextureBindingIndex < 0) {
                return;
            }

            Core core = Core.Instance;
            if (core == null || core.RenderManager2D == null) {
                throw new InvalidOperationException("Shader-backed runtime material loading requires a 2D render manager before diffuse textures can be materialized.");
            }

            string diffuseTexturePath = ResolveImportedTexturePackagePath(materialAsset.DiffuseTextureAssetId);
#if HELENGINE_RUNTIME_TEXTURE_RESOLUTION_COOKED_PLATFORM_OWNED
            RuntimeTexture runtimeTexture = core.RenderManager2D.BuildTextureFromCooked(diffuseTexturePath, assetContentManager.ContentStreamSource);
#else
            TextureAsset textureAsset = assetContentManager.Load<TextureAsset>(diffuseTexturePath, RuntimeContentProcessorIds.TextureAsset);
            RuntimeTexture runtimeTexture = core.RenderManager2D.BuildTextureFromRaw(textureAsset);
#endif
            shaderRuntimeMaterial.Properties.SetTexture(diffuseTextureBindingIndex, runtimeTexture);
        }

        /// <summary>
        /// Resolves one imported texture asset id into the packaged texture path used by shader-backed runtime material loading.
        /// </summary>
        /// <param name="assetId">Imported texture asset identifier stored on the packaged material asset.</param>
        /// <returns>Runtime asset path of the packaged texture payload.</returns>
        static string ResolveImportedTexturePackagePath(string assetId) {
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new InvalidOperationException("Packaged material assets must include a diffuse texture asset id before imported textures can be resolved.");
            }

            return CanonicalPackagedAssetPath.Normalize(Path.Combine(ImportedTextureDirectoryName, assetId));
        }
    }
}

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

            string fullPath = ResolveFileBackedAssetPath(reference);
            ModelAsset modelAsset = AssetContentManager.Load<ModelAsset>(fullPath, RuntimeContentProcessorIds.ModelAsset);
            return Core.Instance.RenderManager3D.BuildModelFromRaw(modelAsset);
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

            string fullPath = ResolveFileBackedAssetPath(reference);
#if PS2_PLATFORM
            Ps2MaterialAsset materialAsset = AssetContentManager.Load<Ps2MaterialAsset>(fullPath, RuntimeContentProcessorIds.MaterialAsset);
            return Core.Instance.RenderManager3D.BuildMaterialFromCooked(materialAsset);
#else
            MaterialAsset materialAsset = AssetContentManager.Load<MaterialAsset>(fullPath, RuntimeContentProcessorIds.MaterialAsset);
            ShaderAsset shaderAsset = AssetContentManager.Load<ShaderAsset>(
                ResolveShaderPackagePath(materialAsset.ShaderAssetId),
                RuntimeContentProcessorIds.ShaderAsset);
            RuntimeMaterial runtimeMaterial = Core.Instance.RenderManager3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
            ApplyMaterialDiffuseTexture(runtimeMaterial, materialAsset);
            return runtimeMaterial;
#endif
        }

        /// <summary>
        /// Applies one authored diffuse texture to the resolved runtime material when the packaged material asset references one.
        /// </summary>
        /// <param name="runtimeMaterial">Runtime material that should receive the diffuse texture.</param>
        /// <param name="materialAsset">Packaged material asset that declares the authored diffuse texture asset id.</param>
        void ApplyMaterialDiffuseTexture(RuntimeMaterial runtimeMaterial, MaterialAsset materialAsset) {
            if (runtimeMaterial == null) {
                throw new ArgumentNullException(nameof(runtimeMaterial));
            }
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }
            if (string.IsNullOrWhiteSpace(materialAsset.DiffuseTextureAssetId)) {
                return;
            }

            string diffuseTexturePath = ResolveImportedTexturePackagePath(materialAsset.DiffuseTextureAssetId);
            TextureAsset textureAsset = AssetContentManager.Load<TextureAsset>(diffuseTexturePath, RuntimeContentProcessorIds.TextureAsset);
            RuntimeTexture runtimeTexture = Core.Instance.RenderManager2D.BuildTextureFromRaw(textureAsset);
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

            string fullPath = ResolveFileBackedAssetPath(reference);
            return AssetContentManager.Load<FontAsset>(fullPath, RuntimeContentProcessorIds.FontAsset);
        }

        /// <summary>
        /// Resolves one packaged file-backed scene asset reference to an absolute file path inside the packaged content root.
        /// </summary>
        /// <param name="reference">Scene asset reference to resolve.</param>
        /// <returns>Absolute packaged file path.</returns>
        string ResolveFileBackedAssetPath(SceneAssetReference reference) {
            if (reference.SourceKind != SceneAssetReferenceSourceKind.FileSystem) {
                throw new InvalidOperationException("Player builds currently require file-backed packaged scene references.");
            }
            if (string.IsNullOrWhiteSpace(reference.RelativePath)) {
                throw new InvalidOperationException("Packaged scene asset references must include a relative path.");
            }

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
    }
}

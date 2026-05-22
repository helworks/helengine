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
        /// Builds one shader-backed runtime material from a packaged material asset.
        /// </summary>
        /// <param name="renderManager3D">Shader-aware renderer that will own the runtime material.</param>
        /// <param name="assetContentManager">Content manager that can deserialize the cooked shader package.</param>
        /// <param name="contentRootPath">Absolute packaged content root.</param>
        /// <param name="materialAssetPath">Absolute path to the serialized material asset.</param>
        /// <param name="materialAsset">Raw material asset definition.</param>
        /// <returns>Runtime material instance.</returns>
        public static RuntimeMaterial BuildMaterialFromRawAsset(
            IShaderRenderManager3D renderManager3D,
            ContentManager assetContentManager,
            string contentRootPath,
            string materialAssetPath,
            MaterialAsset materialAsset) {
            if (renderManager3D == null) {
                throw new ArgumentNullException(nameof(renderManager3D));
            }
            if (assetContentManager == null) {
                throw new ArgumentNullException(nameof(assetContentManager));
            }
            if (string.IsNullOrWhiteSpace(contentRootPath)) {
                throw new ArgumentException("Content root path must be provided.", nameof(contentRootPath));
            }
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            }
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }

            ShaderRuntimeContentRegistration.Register(assetContentManager);
            string shaderPackagePath = ResolveShaderPackagePath(contentRootPath, materialAsset.ShaderAssetId, renderManager3D.ShaderCompileTarget);
            ShaderAsset shaderAsset = assetContentManager.Load<ShaderAsset>(shaderPackagePath, ShaderRuntimeContentProcessorIds.ShaderAsset);
            return renderManager3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
        }

        /// <summary>
        /// Resolves one shader asset id into the packaged shader asset path used by shader-backed runtime builds.
        /// </summary>
        /// <param name="contentRootPath">Absolute packaged content root.</param>
        /// <param name="shaderAssetId">Shader asset identifier stored on the packaged material asset.</param>
        /// <param name="shaderTarget">Renderer shader target used to choose the correct cooked package variant.</param>
        /// <returns>Absolute packaged shader asset path.</returns>
        public static string ResolveShaderPackagePath(string contentRootPath, string shaderAssetId, ShaderCompileTarget shaderTarget) {
            if (string.IsNullOrWhiteSpace(contentRootPath)) {
                throw new ArgumentException("Content root path must be provided.", nameof(contentRootPath));
            }
            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                throw new InvalidOperationException("Packaged material assets must include a shader asset id.");
            }

            string fileName = string.Concat(shaderAssetId, ".", ShaderTargetNames.GetTargetName(shaderTarget), ShaderRuntimeContentRegistration.ShaderPackageExtension);
            return Path.Combine(contentRootPath, ShaderDirectoryName, fileName);
        }
    }
}

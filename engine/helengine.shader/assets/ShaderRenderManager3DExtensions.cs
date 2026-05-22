namespace helengine {
    /// <summary>
    /// Provides shader-aware renderer extension helpers so shader-editing flows can operate on the generic render-manager abstraction without teaching the core renderer base about shader types.
    /// </summary>
    public static class ShaderRenderManager3DExtensions {
        /// <summary>
        /// Builds one runtime material from a raw material asset and its resolved shader asset.
        /// </summary>
        /// <param name="renderManager3D">Renderer that should own the created runtime material.</param>
        /// <param name="materialAsset">Raw material asset definition.</param>
        /// <param name="shaderAsset">Resolved shader asset consumed by the material.</param>
        /// <returns>Runtime material instance.</returns>
        public static RuntimeMaterial BuildMaterialFromRaw(this RenderManager3D renderManager3D, MaterialAsset materialAsset, ShaderAsset shaderAsset) {
            if (renderManager3D == null) {
                throw new ArgumentNullException(nameof(renderManager3D));
            }
            if (renderManager3D is not IShaderRenderManager3D shaderRenderManager3D) {
                throw new InvalidOperationException("This renderer does not support shader-backed runtime materials.");
            }

            return shaderRenderManager3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
        }

        /// <summary>
        /// Invalidates runtime shader resources associated with one compiled shader asset.
        /// </summary>
        /// <param name="renderManager3D">Renderer whose shader resources should be invalidated.</param>
        /// <param name="shaderAssetId">Shader asset identifier to invalidate.</param>
        /// <param name="shaderAsset">Updated shader asset payload.</param>
        public static void InvalidateShaderResources(this RenderManager3D renderManager3D, string shaderAssetId, ShaderAsset shaderAsset) {
            if (renderManager3D == null) {
                throw new ArgumentNullException(nameof(renderManager3D));
            }
            if (renderManager3D is not IShaderRenderManager3D shaderRenderManager3D) {
                throw new InvalidOperationException("This renderer does not support shader resource invalidation.");
            }

            shaderRenderManager3D.InvalidateShaderResources(shaderAssetId, shaderAsset);
        }
    }
}

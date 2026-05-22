namespace helengine {
    /// <summary>
    /// Exposes shader-aware material creation and shader invalidation services for renderers that consume shader runtime metadata.
    /// </summary>
    public interface IShaderRenderManager3D : IShaderCompileTargetProvider {
        /// <summary>
        /// Builds one runtime material from a raw material asset and its resolved shader asset.
        /// </summary>
        /// <param name="materialAsset">Raw material asset definition.</param>
        /// <param name="shaderAsset">Resolved shader asset consumed by the material.</param>
        /// <returns>Runtime material instance.</returns>
        RuntimeMaterial BuildMaterialFromRaw(MaterialAsset materialAsset, ShaderAsset shaderAsset);

        /// <summary>
        /// Invalidates runtime shader resources associated with the supplied shader asset.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier to invalidate.</param>
        /// <param name="shaderAsset">Updated shader asset payload.</param>
        void InvalidateShaderResources(string shaderAssetId, ShaderAsset shaderAsset);
    }
}

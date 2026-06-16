namespace helengine.editor {
    /// <summary>
    /// Reads one GPU render target back into a raw texture asset that packaging can serialize and cook.
    /// </summary>
    public interface IRenderTargetTextureAssetReader {
        /// <summary>
        /// Reads the supplied render target into a raw texture asset using the requested asset id.
        /// </summary>
        /// <param name="renderTarget">Render target whose current color buffer should be captured.</param>
        /// <param name="assetId">Stable asset id that should be assigned to the returned texture asset.</param>
        /// <returns>Raw texture asset containing the render-target contents.</returns>
        TextureAsset ReadTextureAsset(RenderTarget renderTarget, string assetId);
    }
}

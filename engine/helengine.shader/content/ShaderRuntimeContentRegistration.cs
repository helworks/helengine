namespace helengine {
    /// <summary>
    /// Registers shader-runtime content processors onto a generic content manager without teaching the core content system about shader payloads.
    /// </summary>
    public static class ShaderRuntimeContentRegistration {
        /// <summary>
        /// File extension used by cooked shader package payloads.
        /// </summary>
        public const string ShaderPackageExtension = ".hasset";

        /// <summary>
        /// Ensures the shader asset processor is available on the supplied content manager.
        /// </summary>
        /// <param name="contentManager">Content manager to configure.</param>
        public static void Register(ContentManager contentManager) {
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }
            if (!contentManager.IsProcessorRegistered(ShaderRuntimeContentProcessorIds.ShaderAsset)) {
                contentManager.RegisterProcessor(
                    ShaderRuntimeContentProcessorIds.ShaderAsset,
                    new ShaderAssetContentProcessor(),
                    new[] { ShaderPackageExtension });
            }
            if (!contentManager.IsProcessorRegistered(ShaderRuntimeContentProcessorIds.ShaderMaterialAsset)) {
                contentManager.RegisterProcessor(
                    ShaderRuntimeContentProcessorIds.ShaderMaterialAsset,
                    new ShaderMaterialAssetContentProcessor(),
                    new[] { ShaderPackageExtension });
            }
        }
    }
}

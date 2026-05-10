namespace helengine {
    /// <summary>
    /// Registers the runtime content processors required by packaged player builds.
    /// </summary>
    public static class RuntimeContentManagerConfiguration {
        /// <summary>
        /// File extension used for serialized material assets.
        /// </summary>
        const string MaterialAssetExtension = ".helmat";

        /// <summary>
        /// File extension used for serialized shader packages.
        /// </summary>
        const string ShaderPackageExtension = ".shader.asset";

        /// <summary>
        /// File extension used for packaged font assets.
        /// </summary>
        const string FontAssetExtension = ".hefont";

        /// <summary>
        /// Ensures the shared runtime asset processors are registered on the supplied content manager.
        /// </summary>
        /// <param name="contentManager">Content manager to configure.</param>
        public static void ConfigureSharedAssetContentManager(ContentManager contentManager) {
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }

            RegisterProcessorIfMissing(
                contentManager,
                RuntimeContentProcessorIds.MaterialAsset,
#if PS2_PLATFORM
                new AssetContentProcessor<Ps2MaterialAsset>(),
#else
                new AssetContentProcessor<MaterialAsset>(),
#endif
                new[] { MaterialAssetExtension });
            RegisterProcessorIfMissing(
                contentManager,
                RuntimeContentProcessorIds.ModelAsset,
                new AssetContentProcessor<ModelAsset>());
            RegisterProcessorIfMissing(
                contentManager,
                RuntimeContentProcessorIds.TextureAsset,
                new AssetContentProcessor<TextureAsset>());
            RegisterProcessorIfMissing(
                contentManager,
                RuntimeContentProcessorIds.TextAsset,
                new AssetContentProcessor<TextAsset>());
            RegisterProcessorIfMissing(
                contentManager,
                RuntimeContentProcessorIds.ShaderAsset,
                new AssetContentProcessor<ShaderAsset>(),
                new[] { ShaderPackageExtension });
            RegisterProcessorIfMissing(
                contentManager,
                RuntimeContentProcessorIds.SceneAsset,
                new AssetContentProcessor<SceneAsset>(),
                new[] { SceneAsset.FileExtension });
            RegisterProcessorIfMissing(
                contentManager,
                RuntimeContentProcessorIds.FontAsset,
                new BinaryContentProcessor<FontAsset>(FontAssetBinarySerializer.Deserialize),
                new[] { FontAssetExtension });
        }

        /// <summary>
        /// Registers one processor when the target manager has not already been configured with the same processor id.
        /// </summary>
        /// <typeparam name="T">Type produced by the processor.</typeparam>
        /// <param name="contentManager">Content manager receiving the processor.</param>
        /// <param name="processorId">Stable processor identifier.</param>
        /// <param name="processor">Processor implementation to register.</param>
        /// <param name="extensions">Optional default extensions for the processor.</param>
        static void RegisterProcessorIfMissing<T>(
            ContentManager contentManager,
            string processorId,
            IContentProcessor<T> processor,
            string[] extensions = null) {
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }
            if (string.IsNullOrWhiteSpace(processorId)) {
                throw new ArgumentException("Processor id must be provided.", nameof(processorId));
            }
            if (processor == null) {
                throw new ArgumentNullException(nameof(processor));
            }

            if (contentManager.IsProcessorRegistered(processorId)) {
                return;
            }

            contentManager.RegisterProcessor(processorId, processor, extensions);
        }
    }
}

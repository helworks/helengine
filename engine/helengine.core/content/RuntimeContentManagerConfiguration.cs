namespace helengine {
    /// <summary>
    /// Registers the runtime content processors required by packaged player builds.
    /// </summary>
    public static class RuntimeContentManagerConfiguration {
        /// <summary>
        /// File extension used for serialized material assets.
        /// </summary>
        const string MaterialAssetExtension = ".hasset";

        /// <summary>
        /// File extension used for packaged font assets.
        /// </summary>
        const string FontAssetExtension = ".hefont";

        /// <summary>
        /// Ensures the shared runtime asset processors are registered on the supplied content manager.
        /// </summary>
        /// <param name="contentManager">Content manager to configure.</param>
        /// <param name="renderManager2D">Renderer that owns fonts and their generated atlas textures.</param>
        public static void ConfigureSharedAssetContentManager(ContentManager contentManager, RenderManager2D renderManager2D) {
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }
            RegisterProcessorIfMissing(
                contentManager,
                RuntimeContentProcessorIds.MaterialAsset,
#if HELENGINE_RUNTIME_MATERIAL_RESOLUTION_COOKED_PLATFORM_OWNED
                new AssetContentProcessor<PlatformMaterialAsset>(),
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
                RuntimeContentProcessorIds.SceneAsset,
                new BinaryContentProcessor<SceneAsset>(PackagedAssetBinarySerializer.DeserializeSceneAsset),
                new[] { SceneAsset.FileExtension });
            RegisterProcessorIfMissing(
                contentManager,
                RuntimeContentProcessorIds.AnimationClipAsset,
                new AssetContentProcessor<AnimationClipAsset>());
            RegisterProcessorIfMissing(
                contentManager,
                RuntimeContentProcessorIds.AudioAsset,
                new AssetContentProcessor<AudioAsset>());
            // Headless runtime cores do not own a 2D renderer. They can still
            // load every non-font runtime asset; a font processor is registered
            // only by a composition root that explicitly supplies its renderer.
            if (renderManager2D != null) {
                RegisterProcessorIfMissing(
                    contentManager,
                    RuntimeContentProcessorIds.FontAsset,
                    new BinaryContentProcessor<FontAsset>(stream => FontAssetBinarySerializer.Deserialize(stream, renderManager2D)),
                    new[] { FontAssetExtension });
            }
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

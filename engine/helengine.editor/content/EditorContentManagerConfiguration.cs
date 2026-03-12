namespace helengine.editor {
    /// <summary>
    /// Registers the editor's shared content processors onto a reusable core-owned content manager.
    /// </summary>
    public static class EditorContentManagerConfiguration {
        /// <summary>
        /// Ensures the processors needed for shared editor asset loading are registered.
        /// </summary>
        /// <param name="contentManager">Content manager to configure.</param>
        public static void ConfigureSharedAssetContentManager(ContentManager contentManager) {
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }

            RegisterProcessorIfMissing(
                contentManager,
                EditorContentProcessorIds.MaterialAsset,
                new AssetContentProcessor<MaterialAsset>(),
                new[] { EditorFileTemplateRegistry.MaterialExtension });
            RegisterProcessorIfMissing(
                contentManager,
                EditorContentProcessorIds.ModelAsset,
                new AssetContentProcessor<ModelAsset>());
            RegisterProcessorIfMissing(
                contentManager,
                EditorContentProcessorIds.TextureAsset,
                new AssetContentProcessor<TextureAsset>());
            RegisterProcessorIfMissing(
                contentManager,
                EditorContentProcessorIds.TextAsset,
                new AssetContentProcessor<TextAsset>());
            RegisterProcessorIfMissing(
                contentManager,
                EditorContentProcessorIds.ShaderAsset,
                new AssetContentProcessor<ShaderAsset>(),
                new[] { ShaderPackagePaths.PackageExtension });
        }

        /// <summary>
        /// Ensures the processors needed for project asset importing are registered.
        /// </summary>
        /// <param name="contentManager">Content manager to configure.</param>
        public static void ConfigureProjectContentManager(ContentManager contentManager) {
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }

            ConfigureSharedAssetContentManager(contentManager);
            RegisterProcessorIfMissing(
                contentManager,
                EditorContentProcessorIds.AssetImportSettings,
                new BinaryContentProcessor<AssetImportSettings>(AssetImportSettingsBinarySerializer.Deserialize),
                new[] { AssetImportManager.SettingsExtension });
        }

        /// <summary>
        /// Registers a processor when the target manager has not already been configured with the same processor id.
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
            IReadOnlyList<string> extensions = null) {
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

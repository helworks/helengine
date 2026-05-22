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

            ShaderRuntimeContentRegistration.Register(contentManager);
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
                new ShaderAssetContentProcessor(),
                new[] { ShaderPackagePaths.PackageExtension });
            RegisterProcessorIfMissing(
                contentManager,
                EditorContentProcessorIds.SceneAsset,
                new AssetContentProcessor<SceneAsset>(),
                new[] { SceneAsset.FileExtension });
        }

        /// <summary>
        /// Ensures the editor-specific asset processors needed by the properties panel and asset browser are registered.
        /// </summary>
        /// <param name="contentManager">Content manager to configure.</param>
        public static void ConfigureEditorContentManager(ContentManager contentManager) {
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }

            ConfigureSharedAssetContentManager(contentManager);
            RegisterProcessorIfMissing(
                contentManager,
                RuntimeContentProcessorIds.FontAsset,
                new BinaryContentProcessor<global::helengine.FontAsset>(global::helengine.FontAssetBinarySerializer.Deserialize),
                new[] { ".hefont" });
        }

        /// <summary>
        /// Ensures the project-specific processors needed for asset importing are registered.
        /// </summary>
        /// <param name="contentManager">Content manager to configure.</param>
        public static void ConfigureProjectContentManager(ContentManager contentManager) {
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }

            RegisterProcessorIfMissing(
                contentManager,
                EditorContentProcessorIds.AssetImportSettings,
                new BinaryContentProcessor<AssetImportSettings>(AssetImportSettingsBinarySerializer.Deserialize),
                new[] { AssetImportManager.SettingsExtension });
            RegisterProcessorIfMissing(
                contentManager,
                EditorContentProcessorIds.TextureAssetImportSettings,
                new BinaryContentProcessor<TextureAssetImportSettings>(TextureAssetImportSettingsBinarySerializer.Deserialize),
                new[] { AssetImportManager.SettingsExtension });
            RegisterProcessorIfMissing(
                contentManager,
                EditorContentProcessorIds.ModelAssetImportSettings,
                new BinaryContentProcessor<ModelAssetImportSettings>(ModelAssetImportSettingsBinarySerializer.Deserialize),
                new[] { AssetImportManager.SettingsExtension });
            RegisterProcessorIfMissing(
                contentManager,
                EditorContentProcessorIds.MaterialAssetImportSettings,
                new BinaryContentProcessor<MaterialAssetImportSettings>(MaterialAssetImportSettingsBinarySerializer.Deserialize),
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

namespace helengine.core.tests.content {
    /// <summary>
    /// Test configurator that registers one texture processor on the shared asset content manager and counts how often the core invokes it.
    /// </summary>
    public sealed class RecordingConfigurator : IAssetContentManagerConfigurator {
        /// <summary>
        /// Creates a configurator that will register the supplied processor under the shared texture processor id.
        /// </summary>
        /// <param name="processor">Texture processor the configurator registers when invoked.</param>
        public RecordingConfigurator(RecordingTextureProcessor processor) {
            if (processor == null) {
                throw new ArgumentNullException(nameof(processor));
            }
            Processor = processor;
        }

        /// <summary>
        /// Gets the texture processor registered by <see cref="Configure"/>.
        /// </summary>
        public RecordingTextureProcessor Processor { get; }

        /// <summary>
        /// Gets the number of times the core invoked <see cref="Configure"/>.
        /// </summary>
        public int CallCount { get; private set; }

        /// <summary>
        /// Records the call and registers the recording texture processor under <see cref="RuntimeContentProcessorIds.TextureAsset"/>.
        /// </summary>
        /// <param name="contentManager">Shared asset content manager owned by the initializing core.</param>
        /// <param name="renderManager2D">2D renderer of the initializing core; null for headless cores.</param>
        public void Configure(ContentManager contentManager, RenderManager2D renderManager2D) {
            CallCount++;
            contentManager.RegisterProcessor<TextureAsset>(RuntimeContentProcessorIds.TextureAsset, Processor);
        }
    }
}

namespace helengine.core.tests.content {
    /// <summary>
    /// Verifies that a host-supplied content manager configurator runs before the shared runtime processors are registered.
    /// </summary>
    public sealed class AssetContentManagerConfiguratorTests {
        /// <summary>
        /// A processor registered by the configurator for a shared processor id must be the one the content manager uses.
        /// </summary>
        [Fact]
        public void Initialize_WhenConfiguratorRegistersTextureProcessor_ConfiguratorProcessorWins() {
            RecordingTextureProcessor processor = new RecordingTextureProcessor();
            RecordingConfigurator configurator = new RecordingConfigurator(processor);
            MemoryContentSource source = new MemoryContentSource(new Dictionary<string, byte[]> { ["cooked/imported/a"] = new byte[] { 1 } });
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = source, AssetContentManagerConfigurator = configurator });
            core.Initialize(null, null, null, new PlatformInfo("test", "1"), core.InitializationOptions);

            TextureAsset texture = core.GetContentManager().Load<TextureAsset>("cooked/imported/a", RuntimeContentProcessorIds.TextureAsset);

            Assert.Equal(1, configurator.CallCount);
            Assert.Same(processor.LastResult, texture);
        }
    }
}

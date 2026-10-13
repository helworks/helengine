using helengine.baseplatform.Player;

namespace helengine.player {
    /// <summary>
    /// Lets the platform player adapter register its content processors on the core's content manager before the shared
    /// processors are added, so platform-cooked assets are decoded by the platform's own processors.
    /// </summary>
    public sealed class PlayerAdapterContentManagerConfigurator : IAssetContentManagerConfigurator {
        /// <summary>
        /// Creates a configurator that delegates processor registration to one initialized adapter.
        /// </summary>
        /// <param name="adapter">Initialized platform player adapter.</param>
        public PlayerAdapterContentManagerConfigurator(IPlatformPlayerAdapter adapter) {
            Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        /// <summary>Gets the platform player adapter that registers the content processors.</summary>
        public IPlatformPlayerAdapter Adapter { get; }

        /// <summary>
        /// Registers the adapter's content processors on the core's content manager.
        /// </summary>
        /// <param name="contentManager">Content manager owned by the core.</param>
        /// <param name="renderManager2D">2D renderer of the core; the adapter's processors do not need it.</param>
        public void Configure(ContentManager contentManager, RenderManager2D renderManager2D) {
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }

            Adapter.RegisterContentProcessors(contentManager);
        }
    }
}

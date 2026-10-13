namespace helengine {
    /// <summary>
    /// Lets a host register its own content processors on the core's shared asset content manager before the
    /// engine registers the shared runtime processors, so host processors win for the same processor id.
    /// </summary>
    public interface IAssetContentManagerConfigurator {
        /// <summary>
        /// Registers host-owned processors on the shared asset content manager.
        /// </summary>
        /// <param name="contentManager">Shared asset content manager owned by the initializing core.</param>
        /// <param name="renderManager2D">2D renderer of the initializing core; null for headless cores.</param>
        void Configure(ContentManager contentManager, RenderManager2D renderManager2D);
    }
}

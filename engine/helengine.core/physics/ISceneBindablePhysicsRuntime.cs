namespace helengine {
    /// <summary>
    /// Extends the base physics runtime contract with scene binding and runtime body-count reporting needed by host integrations.
    /// </summary>
    public interface ISceneBindablePhysicsRuntime : IPhysicsRuntime {
        /// <summary>
        /// Binds the active scene hierarchy to the runtime.
        /// </summary>
        /// <param name="rootEntities">Root entities that define the active scene.</param>
        void BindScene(IReadOnlyList<Entity> rootEntities);

        /// <summary>
        /// Gets the number of registered runtime bodies currently bound to the scene.
        /// </summary>
        int RegisteredBodyCount { get; }
    }
}

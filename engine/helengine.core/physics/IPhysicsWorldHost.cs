namespace helengine {
    /// <summary>
    /// Exposes the core-owned runtime services that one physics world can rely on without taking ownership of engine state.
    /// </summary>
    public interface IPhysicsWorldHost {
        /// <summary>
        /// Gets the core instance that owns the active runtime world.
        /// </summary>
        Core Core { get; }
    }
}
